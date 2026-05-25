using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClassDiagramMaker.Analysis;

internal static class SyntaxTypeCollector
{
    public static IReadOnlyList<DiagramType> Collect(CompilationUnitSyntax root, string sourceFile)
    {
        return root.DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Select(declaration => CreateType(declaration, sourceFile))
            .ToArray();
    }

    private static DiagramType CreateType(BaseTypeDeclarationSyntax declaration, string sourceFile)
    {
        var typeParameters = GetTypeParameters(declaration);
        var simpleName = declaration.Identifier.ValueText;
        var displayName = typeParameters.Count == 0
            ? simpleName
            : $"{simpleName}<{string.Join(", ", typeParameters)}>";
        var namespaceName = GetNamespace(declaration);
        var containingTypes = declaration.Ancestors()
            .OfType<BaseTypeDeclarationSyntax>()
            .Reverse()
            .Select(GetDisplayName)
            .ToArray();
        var nestedName = string.Join(".", containingTypes.Concat(new[] { displayName }));
        var fullName = string.IsNullOrWhiteSpace(namespaceName)
            ? nestedName
            : $"{namespaceName}.{nestedName}";

        return new DiagramType
        {
            Id = MermaidNames.ToId(fullName),
            SimpleName = simpleName,
            DisplayName = displayName,
            FullName = fullName,
            Namespace = namespaceName,
            SourceFile = sourceFile,
            Kind = GetKind(declaration),
            Accessibility = GetAccessibility(declaration.Modifiers, isTypeDeclaration: true),
            Modifiers = GetNonAccessibilityModifiers(declaration.Modifiers),
            TypeParameters = typeParameters,
            TypeParameterConstraints = GetTypeParameterConstraints(declaration),
            BaseTypes = GetBaseTypes(declaration),
            Members = GetMembers(declaration)
        };
    }

    private static string GetDisplayName(BaseTypeDeclarationSyntax declaration)
    {
        var typeParameters = GetTypeParameters(declaration);
        return typeParameters.Count == 0
            ? declaration.Identifier.ValueText
            : $"{declaration.Identifier.ValueText}<{string.Join(", ", typeParameters)}>";
    }

    private static IReadOnlyList<string> GetTypeParameters(BaseTypeDeclarationSyntax declaration)
    {
        return declaration is TypeDeclarationSyntax typeDeclaration && typeDeclaration.TypeParameterList is not null
            ? typeDeclaration.TypeParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText).ToArray()
            : Array.Empty<string>();
    }

    private static IReadOnlyList<string> GetTypeParameterConstraints(BaseTypeDeclarationSyntax declaration)
    {
        return declaration is TypeDeclarationSyntax typeDeclaration
            ? FormatConstraintClauses(typeDeclaration.ConstraintClauses)
            : Array.Empty<string>();
    }

    private static string GetNamespace(SyntaxNode node)
    {
        return string.Join(
            ".",
            node.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Reverse()
                .Select(namespaceDeclaration => namespaceDeclaration.Name.ToString()));
    }

    private static DiagramTypeKind GetKind(BaseTypeDeclarationSyntax declaration)
    {
        return declaration switch
        {
            InterfaceDeclarationSyntax => DiagramTypeKind.Interface,
            StructDeclarationSyntax => DiagramTypeKind.Struct,
            RecordDeclarationSyntax => DiagramTypeKind.Record,
            EnumDeclarationSyntax => DiagramTypeKind.Enum,
            _ => DiagramTypeKind.Class
        };
    }

    private static IReadOnlyList<string> GetBaseTypes(BaseTypeDeclarationSyntax declaration)
    {
        return declaration is TypeDeclarationSyntax { BaseList: { } baseList }
            ? baseList.Types.Select(baseType => baseType.Type.ToString()).ToArray()
            : Array.Empty<string>();
    }

    private static IReadOnlyList<DiagramMember> GetMembers(BaseTypeDeclarationSyntax declaration)
    {
        if (declaration is EnumDeclarationSyntax enumDeclaration)
        {
            return enumDeclaration.Members
                .Select(member => new DiagramMember
                {
                    Kind = DiagramMemberKind.EnumValue,
                    Name = member.Identifier.ValueText,
                    Type = string.Empty,
                    Visibility = string.Empty,
                    Signature = member.Identifier.ValueText
                })
                .ToArray();
        }

        if (declaration is not TypeDeclarationSyntax typeDeclaration)
        {
            return Array.Empty<DiagramMember>();
        }

        var members = new List<DiagramMember>();
        members.AddRange(CreateRecordPrimaryConstructorMembers(typeDeclaration));

        foreach (var member in typeDeclaration.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    members.AddRange(CreateFieldMembers(field, typeDeclaration));
                    break;
                case PropertyDeclarationSyntax property:
                    members.Add(CreatePropertyMember(property, typeDeclaration));
                    break;
                case MethodDeclarationSyntax method:
                    members.Add(CreateMethodMember(method, typeDeclaration));
                    break;
                case ConstructorDeclarationSyntax constructor:
                    members.Add(CreateConstructorMember(constructor));
                    break;
                case EventDeclarationSyntax eventDeclaration:
                    members.Add(CreateEventMember(eventDeclaration, typeDeclaration));
                    break;
                case EventFieldDeclarationSyntax eventField:
                    members.AddRange(CreateEventFieldMembers(eventField, typeDeclaration));
                    break;
                case IndexerDeclarationSyntax indexer:
                    members.Add(CreateIndexerMember(indexer, typeDeclaration));
                    break;
            }
        }

        return members;
    }

    private static IEnumerable<DiagramMember> CreateRecordPrimaryConstructorMembers(TypeDeclarationSyntax typeDeclaration)
    {
        if (typeDeclaration is not RecordDeclarationSyntax { ParameterList: { } parameterList })
        {
            return Array.Empty<DiagramMember>();
        }

        return parameterList.Parameters.Select(parameter =>
        {
            var type = parameter.Type?.ToString() ?? "var";
            var name = parameter.Identifier.ValueText;
            return new DiagramMember
            {
                Kind = DiagramMemberKind.Property,
                Name = name,
                Type = type,
                Visibility = "+",
                Signature = $"+{name}: {type}",
                ReferencedTypes = TypeReferenceCollector.Collect(parameter.Type)
            };
        });
    }

    private static IEnumerable<DiagramMember> CreateFieldMembers(
        FieldDeclarationSyntax field,
        TypeDeclarationSyntax containingType)
    {
        var type = field.Declaration.Type.ToString();
        var defaultPublic = IsInterfaceMember(containingType);
        foreach (var variable in field.Declaration.Variables)
        {
            yield return new DiagramMember
            {
                Kind = DiagramMemberKind.Field,
                Name = variable.Identifier.ValueText,
                Type = type,
                Visibility = GetVisibilitySymbol(field.Modifiers, defaultPublic),
                Signature = CreateMemberSignature(field.Modifiers, $"{variable.Identifier.ValueText}: {type}", defaultPublic),
                IsStatic = HasModifier(field.Modifiers, SyntaxKind.StaticKeyword),
                Modifiers = GetNonAccessibilityModifiers(field.Modifiers),
                ReferencedTypes = TypeReferenceCollector.Collect(field.Declaration.Type)
            };
        }
    }

    private static DiagramMember CreatePropertyMember(
        PropertyDeclarationSyntax property,
        TypeDeclarationSyntax containingType)
    {
        var type = property.Type.ToString();
        var defaultPublic = IsInterfaceMember(containingType);
        return new DiagramMember
        {
            Kind = DiagramMemberKind.Property,
            Name = property.Identifier.ValueText,
            Type = type,
            Visibility = GetVisibilitySymbol(property.Modifiers, defaultPublic),
            Signature = CreateMemberSignature(property.Modifiers, $"{property.Identifier.ValueText}: {type}", defaultPublic),
            IsStatic = HasModifier(property.Modifiers, SyntaxKind.StaticKeyword),
            Modifiers = GetNonAccessibilityModifiers(property.Modifiers),
            ReferencedTypes = TypeReferenceCollector.Collect(property.Type)
        };
    }

    private static DiagramMember CreateMethodMember(
        MethodDeclarationSyntax method,
        TypeDeclarationSyntax containingType)
    {
        var returnType = method.ReturnType.ToString();
        var defaultPublic = IsInterfaceMember(containingType);
        var typeParameters = method.TypeParameterList is null
            ? string.Empty
            : $"<{string.Join(", ", method.TypeParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText))}>";
        var parameters = FormatParameters(method.ParameterList.Parameters);
        var constraints = FormatConstraintClauses(method.ConstraintClauses);
        var references = TypeReferenceCollector.Collect(method.ReturnType)
            .Concat(method.ParameterList.Parameters.SelectMany(parameter => TypeReferenceCollector.Collect(parameter.Type)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var coreSignature = $"{method.Identifier.ValueText}{typeParameters}({parameters}): {returnType}";
        if (constraints.Count > 0)
        {
            coreSignature = $"{coreSignature} {string.Join(" ", constraints)}";
        }

        return new DiagramMember
        {
            Kind = DiagramMemberKind.Method,
            Name = method.Identifier.ValueText,
            Type = returnType,
            Visibility = GetVisibilitySymbol(method.Modifiers, defaultPublic),
            Signature = CreateMemberSignature(method.Modifiers, coreSignature, defaultPublic),
            IsStatic = HasModifier(method.Modifiers, SyntaxKind.StaticKeyword),
            Modifiers = GetNonAccessibilityModifiers(method.Modifiers),
            TypeParameterConstraints = constraints,
            ReferencedTypes = references
        };
    }

    private static DiagramMember CreateConstructorMember(ConstructorDeclarationSyntax constructor)
    {
        var parameters = FormatParameters(constructor.ParameterList.Parameters);
        var references = constructor.ParameterList.Parameters
            .SelectMany(parameter => TypeReferenceCollector.Collect(parameter.Type))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new DiagramMember
        {
            Kind = DiagramMemberKind.Constructor,
            Name = constructor.Identifier.ValueText,
            Type = string.Empty,
            Visibility = GetVisibilitySymbol(constructor.Modifiers),
            Signature = CreateMemberSignature(constructor.Modifiers, $"{constructor.Identifier.ValueText}({parameters})"),
            IsStatic = HasModifier(constructor.Modifiers, SyntaxKind.StaticKeyword),
            Modifiers = GetNonAccessibilityModifiers(constructor.Modifiers),
            ReferencedTypes = references
        };
    }

    private static DiagramMember CreateEventMember(
        EventDeclarationSyntax eventDeclaration,
        TypeDeclarationSyntax containingType)
    {
        var type = eventDeclaration.Type.ToString();
        var defaultPublic = IsInterfaceMember(containingType);
        return new DiagramMember
        {
            Kind = DiagramMemberKind.Event,
            Name = eventDeclaration.Identifier.ValueText,
            Type = type,
            Visibility = GetVisibilitySymbol(eventDeclaration.Modifiers, defaultPublic),
            Signature = CreateMemberSignature(eventDeclaration.Modifiers, $"{eventDeclaration.Identifier.ValueText}: {type}", defaultPublic),
            IsStatic = HasModifier(eventDeclaration.Modifiers, SyntaxKind.StaticKeyword),
            Modifiers = GetNonAccessibilityModifiers(eventDeclaration.Modifiers),
            ReferencedTypes = TypeReferenceCollector.Collect(eventDeclaration.Type)
        };
    }

    private static IEnumerable<DiagramMember> CreateEventFieldMembers(
        EventFieldDeclarationSyntax eventField,
        TypeDeclarationSyntax containingType)
    {
        var type = eventField.Declaration.Type.ToString();
        var defaultPublic = IsInterfaceMember(containingType);
        foreach (var variable in eventField.Declaration.Variables)
        {
            yield return new DiagramMember
            {
                Kind = DiagramMemberKind.Event,
                Name = variable.Identifier.ValueText,
                Type = type,
                Visibility = GetVisibilitySymbol(eventField.Modifiers, defaultPublic),
                Signature = CreateMemberSignature(eventField.Modifiers, $"{variable.Identifier.ValueText}: {type}", defaultPublic),
                IsStatic = HasModifier(eventField.Modifiers, SyntaxKind.StaticKeyword),
                Modifiers = GetNonAccessibilityModifiers(eventField.Modifiers),
                ReferencedTypes = TypeReferenceCollector.Collect(eventField.Declaration.Type)
            };
        }
    }

    private static DiagramMember CreateIndexerMember(
        IndexerDeclarationSyntax indexer,
        TypeDeclarationSyntax containingType)
    {
        var type = indexer.Type.ToString();
        var defaultPublic = IsInterfaceMember(containingType);
        var parameters = FormatParameters(indexer.ParameterList.Parameters);
        var references = TypeReferenceCollector.Collect(indexer.Type)
            .Concat(indexer.ParameterList.Parameters.SelectMany(parameter => TypeReferenceCollector.Collect(parameter.Type)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new DiagramMember
        {
            Kind = DiagramMemberKind.Indexer,
            Name = "this",
            Type = type,
            Visibility = GetVisibilitySymbol(indexer.Modifiers, defaultPublic),
            Signature = CreateMemberSignature(indexer.Modifiers, $"this[{parameters}]: {type}", defaultPublic),
            IsStatic = HasModifier(indexer.Modifiers, SyntaxKind.StaticKeyword),
            Modifiers = GetNonAccessibilityModifiers(indexer.Modifiers),
            ReferencedTypes = references
        };
    }

    private static string FormatParameters(SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        return string.Join(", ", parameters.Select(parameter =>
        {
            var type = parameter.Type?.ToString() ?? "var";
            return $"{parameter.Identifier.ValueText}: {type}";
        }));
    }

    private static IReadOnlyList<string> FormatConstraintClauses(SyntaxList<TypeParameterConstraintClauseSyntax> clauses)
    {
        return clauses
            .Select(clause => $"where {clause.Name} : {string.Join(", ", clause.Constraints.Select(constraint => constraint.ToString()))}")
            .ToArray();
    }

    private static string CreateMemberSignature(
        SyntaxTokenList modifiers,
        string signature,
        bool defaultPublic = false)
    {
        var visibility = GetVisibilitySymbol(modifiers, defaultPublic);
        var nonAccessibilityModifiers = GetNonAccessibilityModifiers(modifiers);
        var modifierText = nonAccessibilityModifiers.Count == 0
            ? string.Empty
            : $"{{{string.Join(" ", nonAccessibilityModifiers)}}} ";

        return $"{visibility}{modifierText}{signature}";
    }

    private static string GetAccessibility(
        SyntaxTokenList modifiers,
        bool isTypeDeclaration,
        bool defaultPublic = false)
    {
        if (modifiers.Any(SyntaxKind.PublicKeyword))
        {
            return "public";
        }

        if (modifiers.Any(SyntaxKind.PrivateKeyword))
        {
            return "private";
        }

        if (modifiers.Any(SyntaxKind.ProtectedKeyword) && modifiers.Any(SyntaxKind.InternalKeyword))
        {
            return "protected internal";
        }

        if (modifiers.Any(SyntaxKind.ProtectedKeyword))
        {
            return "protected";
        }

        if (modifiers.Any(SyntaxKind.InternalKeyword))
        {
            return "internal";
        }

        if (modifiers.Any(token => string.Equals(token.ValueText, "file", StringComparison.Ordinal)))
        {
            return "file";
        }

        if (defaultPublic)
        {
            return "public";
        }

        return isTypeDeclaration ? "internal" : "private";
    }

    private static string GetVisibilitySymbol(
        SyntaxTokenList modifiers,
        bool defaultPublic = false)
    {
        return GetAccessibility(modifiers, isTypeDeclaration: false, defaultPublic) switch
        {
            "public" => "+",
            "protected" => "#",
            "protected internal" => "#",
            "internal" => "~",
            _ => "-"
        };
    }

    private static IReadOnlyList<string> GetNonAccessibilityModifiers(SyntaxTokenList modifiers)
    {
        return modifiers
            .Select(modifier => modifier.ValueText)
            .Where(modifier => modifier is not "public" and not "private" and not "protected" and not "internal" and not "file")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasModifier(SyntaxTokenList modifiers, SyntaxKind kind)
    {
        return modifiers.Any(kind);
    }

    private static bool IsInterfaceMember(TypeDeclarationSyntax containingType)
    {
        return containingType is InterfaceDeclarationSyntax;
    }
}
