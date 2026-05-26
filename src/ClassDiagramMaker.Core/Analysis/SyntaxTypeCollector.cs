using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClassDiagramMaker.Analysis;

internal static class SyntaxTypeCollector
{
    public static IReadOnlyList<DiagramType> Collect(
        CompilationUnitSyntax root,
        string sourceFile,
        SemanticModel? semanticModel = null)
    {
        return root.DescendantNodes()
            .Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
            .Select(declaration => CreateType(declaration, sourceFile, semanticModel))
            .ToArray();
    }

    private static DiagramType CreateType(
        SyntaxNode declaration,
        string sourceFile,
        SemanticModel? semanticModel)
    {
        return declaration switch
        {
            BaseTypeDeclarationSyntax typeDeclaration => CreateType(typeDeclaration, sourceFile, semanticModel),
            DelegateDeclarationSyntax delegateDeclaration => CreateDelegateType(delegateDeclaration, sourceFile, semanticModel),
            _ => throw new ArgumentOutOfRangeException(nameof(declaration), declaration, null)
        };
    }

    private static DiagramType CreateType(
        BaseTypeDeclarationSyntax declaration,
        string sourceFile,
        SemanticModel? semanticModel)
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
            Members = GetMembers(declaration, semanticModel),
            Dependencies = GetTypeDependencies(declaration, semanticModel)
        };
    }

    private static DiagramType CreateDelegateType(
        DelegateDeclarationSyntax declaration,
        string sourceFile,
        SemanticModel? semanticModel)
    {
        var typeParameters = declaration.TypeParameterList is null
            ? Array.Empty<string>()
            : declaration.TypeParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText).ToArray();
        var simpleName = declaration.Identifier.ValueText;
        var displayName = typeParameters.Length == 0
            ? simpleName
            : $"{simpleName}<{string.Join(", ", typeParameters)}>";
        var namespaceName = GetNamespace(declaration);
        var fullName = string.IsNullOrWhiteSpace(namespaceName)
            ? displayName
            : $"{namespaceName}.{displayName}";
        var parameters = FormatParameters(declaration.ParameterList.Parameters);
        var constraints = FormatConstraintClauses(declaration.ConstraintClauses);
        var signature = $"Invoke({parameters}): {declaration.ReturnType}";
        if (constraints.Count > 0)
        {
            signature = $"{signature} {string.Join(" ", constraints)}";
        }

        var references = TypeReferenceCollector.Collect(declaration.ReturnType, semanticModel)
            .Concat(declaration.ParameterList.Parameters.SelectMany(parameter => TypeReferenceCollector.Collect(parameter.Type, semanticModel)))
            .Concat(CollectConstraintReferences(declaration.ConstraintClauses, semanticModel))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new DiagramType
        {
            Id = MermaidNames.ToId(fullName),
            SimpleName = simpleName,
            DisplayName = displayName,
            FullName = fullName,
            Namespace = namespaceName,
            SourceFile = sourceFile,
            Kind = DiagramTypeKind.Delegate,
            Accessibility = GetAccessibility(declaration.Modifiers, isTypeDeclaration: true),
            Modifiers = GetNonAccessibilityModifiers(declaration.Modifiers),
            TypeParameters = typeParameters,
            TypeParameterConstraints = constraints,
            Members = new[]
            {
                new DiagramMember
                {
                    Kind = DiagramMemberKind.Method,
                    Name = "Invoke",
                    Type = declaration.ReturnType.ToString(),
                    Visibility = "+",
                    Signature = $"+{signature}",
                    ReferencedTypes = references
                }
            },
            Dependencies = CollectAttributeDependencies(declaration.AttributeLists, semanticModel, "attribute")
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

    private static IReadOnlyList<DiagramDependency> GetTypeDependencies(
        BaseTypeDeclarationSyntax declaration,
        SemanticModel? semanticModel)
    {
        var dependencies = new List<DiagramDependency>();
        dependencies.AddRange(CollectAttributeDependencies(declaration.AttributeLists, semanticModel, "attribute"));

        if (declaration is not TypeDeclarationSyntax typeDeclaration)
        {
            return DistinctDependencies(dependencies);
        }

        dependencies.AddRange(CollectUsingStaticDependencies(typeDeclaration, semanticModel));
        dependencies.AddRange(CollectConstraintDependencies(typeDeclaration.ConstraintClauses, semanticModel));

        if (typeDeclaration.BaseList is not null)
        {
            foreach (var baseType in typeDeclaration.BaseList.Types)
            {
                dependencies.AddRange(CollectBaseTypeArgumentReferences(baseType.Type, semanticModel)
                    .Select(reference => new DiagramDependency
                    {
                        TypeName = reference,
                        Label = "base"
                    }));
            }
        }

        return DistinctDependencies(dependencies);
    }

    private static IReadOnlyList<DiagramMember> GetMembers(
        BaseTypeDeclarationSyntax declaration,
        SemanticModel? semanticModel)
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
        members.AddRange(CreateRecordPrimaryConstructorMembers(typeDeclaration, semanticModel));
        members.AddRange(CreateClassPrimaryConstructorMembers(typeDeclaration, semanticModel));

        foreach (var member in typeDeclaration.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    members.AddRange(CreateFieldMembers(field, typeDeclaration, semanticModel));
                    break;
                case PropertyDeclarationSyntax property:
                    members.Add(CreatePropertyMember(property, typeDeclaration, semanticModel));
                    break;
                case MethodDeclarationSyntax method:
                    members.Add(CreateMethodMember(method, typeDeclaration, semanticModel));
                    break;
                case ConstructorDeclarationSyntax constructor:
                    members.Add(CreateConstructorMember(constructor, semanticModel));
                    break;
                case EventDeclarationSyntax eventDeclaration:
                    members.Add(CreateEventMember(eventDeclaration, typeDeclaration, semanticModel));
                    break;
                case EventFieldDeclarationSyntax eventField:
                    members.AddRange(CreateEventFieldMembers(eventField, typeDeclaration, semanticModel));
                    break;
                case IndexerDeclarationSyntax indexer:
                    members.Add(CreateIndexerMember(indexer, typeDeclaration, semanticModel));
                    break;
            }
        }

        return members;
    }

    private static IEnumerable<DiagramMember> CreateRecordPrimaryConstructorMembers(
        TypeDeclarationSyntax typeDeclaration,
        SemanticModel? semanticModel)
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
                ReferencedTypes = TypeReferenceCollector.Collect(parameter.Type, semanticModel)
            };
        });
    }

    private static IEnumerable<DiagramMember> CreateClassPrimaryConstructorMembers(
        TypeDeclarationSyntax typeDeclaration,
        SemanticModel? semanticModel)
    {
        if (typeDeclaration is RecordDeclarationSyntax || typeDeclaration.ParameterList is null)
        {
            return Array.Empty<DiagramMember>();
        }

        return new[]
        {
            new DiagramMember
            {
                Kind = DiagramMemberKind.Constructor,
                Name = typeDeclaration.Identifier.ValueText,
                Type = string.Empty,
                Visibility = GetVisibilitySymbol(typeDeclaration.Modifiers),
                Signature = CreateMemberSignature(typeDeclaration.Modifiers, $"{typeDeclaration.Identifier.ValueText}({FormatParameters(typeDeclaration.ParameterList.Parameters)})"),
                ReferencedTypes = typeDeclaration.ParameterList.Parameters
                    .SelectMany(parameter => TypeReferenceCollector.Collect(parameter.Type, semanticModel))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            }
        };
    }

    private static IEnumerable<DiagramMember> CreateFieldMembers(
        FieldDeclarationSyntax field,
        TypeDeclarationSyntax containingType,
        SemanticModel? semanticModel)
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
                ReferencedTypes = TypeReferenceCollector.Collect(field.Declaration.Type, semanticModel)
                    .Concat(CollectAttributeReferences(field.AttributeLists, semanticModel))
                    .Concat(CollectMemberBodyReferences(field, semanticModel))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            };
        }
    }

    private static DiagramMember CreatePropertyMember(
        PropertyDeclarationSyntax property,
        TypeDeclarationSyntax containingType,
        SemanticModel? semanticModel)
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
            ReferencedTypes = TypeReferenceCollector.Collect(property.Type, semanticModel)
                .Concat(CollectAttributeReferences(property.AttributeLists, semanticModel))
                .Concat(CollectMemberBodyReferences(property, semanticModel))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static DiagramMember CreateMethodMember(
        MethodDeclarationSyntax method,
        TypeDeclarationSyntax containingType,
        SemanticModel? semanticModel)
    {
        var returnType = method.ReturnType.ToString();
        var defaultPublic = IsInterfaceMember(containingType);
        var typeParameters = method.TypeParameterList is null
            ? string.Empty
            : $"<{string.Join(", ", method.TypeParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText))}>";
        var parameters = FormatParameters(method.ParameterList.Parameters);
        var constraints = FormatConstraintClauses(method.ConstraintClauses);
        var references = TypeReferenceCollector.Collect(method.ReturnType, semanticModel)
            .Concat(method.ParameterList.Parameters.SelectMany(parameter => TypeReferenceCollector.Collect(parameter.Type, semanticModel)))
            .Concat(CollectConstraintReferences(method.ConstraintClauses, semanticModel))
            .Concat(CollectAttributeReferences(method.AttributeLists, semanticModel))
            .Concat(CollectMemberBodyReferences(method, semanticModel))
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

    private static DiagramMember CreateConstructorMember(
        ConstructorDeclarationSyntax constructor,
        SemanticModel? semanticModel)
    {
        var parameters = FormatParameters(constructor.ParameterList.Parameters);
        var references = constructor.ParameterList.Parameters
            .SelectMany(parameter => TypeReferenceCollector.Collect(parameter.Type, semanticModel))
            .Concat(CollectAttributeReferences(constructor.AttributeLists, semanticModel))
            .Concat(CollectMemberBodyReferences(constructor, semanticModel))
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
        TypeDeclarationSyntax containingType,
        SemanticModel? semanticModel)
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
            ReferencedTypes = TypeReferenceCollector.Collect(eventDeclaration.Type, semanticModel)
                .Concat(CollectAttributeReferences(eventDeclaration.AttributeLists, semanticModel))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static IEnumerable<DiagramMember> CreateEventFieldMembers(
        EventFieldDeclarationSyntax eventField,
        TypeDeclarationSyntax containingType,
        SemanticModel? semanticModel)
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
                ReferencedTypes = TypeReferenceCollector.Collect(eventField.Declaration.Type, semanticModel)
                    .Concat(CollectAttributeReferences(eventField.AttributeLists, semanticModel))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            };
        }
    }

    private static DiagramMember CreateIndexerMember(
        IndexerDeclarationSyntax indexer,
        TypeDeclarationSyntax containingType,
        SemanticModel? semanticModel)
    {
        var type = indexer.Type.ToString();
        var defaultPublic = IsInterfaceMember(containingType);
        var parameters = FormatParameters(indexer.ParameterList.Parameters);
        var references = TypeReferenceCollector.Collect(indexer.Type, semanticModel)
            .Concat(indexer.ParameterList.Parameters.SelectMany(parameter => TypeReferenceCollector.Collect(parameter.Type, semanticModel)))
            .Concat(CollectAttributeReferences(indexer.AttributeLists, semanticModel))
            .Concat(CollectMemberBodyReferences(indexer, semanticModel))
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

    private static IReadOnlyList<DiagramDependency> CollectUsingStaticDependencies(
        SyntaxNode declaration,
        SemanticModel? semanticModel)
    {
        var root = declaration.SyntaxTree.GetCompilationUnitRoot();
        return root.Usings
            .Where(usingDirective => !usingDirective.StaticKeyword.IsKind(SyntaxKind.None) && usingDirective.Name is not null)
            .SelectMany(usingDirective => TypeReferenceCollector.Collect(usingDirective.Name, semanticModel))
            .Select(reference => new DiagramDependency
            {
                TypeName = reference,
                Label = "using static"
            })
            .ToArray();
    }

    private static IReadOnlyList<DiagramDependency> CollectConstraintDependencies(
        SyntaxList<TypeParameterConstraintClauseSyntax> clauses,
        SemanticModel? semanticModel)
    {
        return clauses
            .SelectMany(clause => CollectConstraintReferences(clause, semanticModel)
                .Select(reference => new DiagramDependency
                {
                    TypeName = reference,
                    Label = $"where {clause.Name}"
                }))
            .ToArray();
    }

    private static IReadOnlyList<string> CollectConstraintReferences(
        SyntaxList<TypeParameterConstraintClauseSyntax> clauses,
        SemanticModel? semanticModel)
    {
        return clauses
            .SelectMany(clause => CollectConstraintReferences(clause, semanticModel))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> CollectConstraintReferences(
        TypeParameterConstraintClauseSyntax clause,
        SemanticModel? semanticModel)
    {
        return clause.Constraints
            .OfType<TypeConstraintSyntax>()
            .SelectMany(constraint => TypeReferenceCollector.Collect(constraint.Type, semanticModel));
    }

    private static IReadOnlyList<string> CollectBaseTypeArgumentReferences(
        TypeSyntax baseType,
        SemanticModel? semanticModel)
    {
        var primary = TypeReferenceCollector.GetPrimaryTypeName(baseType, semanticModel);
        return TypeReferenceCollector.Collect(baseType, semanticModel)
            .Where(reference => !string.Equals(NormalizeTypeName(reference), NormalizeTypeName(primary), StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<DiagramDependency> CollectAttributeDependencies(
        SyntaxList<AttributeListSyntax> attributeLists,
        SemanticModel? semanticModel,
        string label)
    {
        return CollectAttributeReferences(attributeLists, semanticModel)
            .Select(reference => new DiagramDependency
            {
                TypeName = reference,
                Label = label
            })
            .ToArray();
    }

    private static IReadOnlyList<string> CollectAttributeReferences(
        SyntaxList<AttributeListSyntax> attributeLists,
        SemanticModel? semanticModel)
    {
        var references = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attribute in attributeLists.SelectMany(list => list.Attributes))
        {
            if (semanticModel is not null)
            {
                AddSymbolReference(semanticModel.GetSymbolInfo(attribute).Symbol, references);
            }

            foreach (var typeOfExpression in attribute.DescendantNodes().OfType<TypeOfExpressionSyntax>())
            {
                AddReferences(TypeReferenceCollector.Collect(typeOfExpression.Type, semanticModel), references);
            }
        }

        return references.ToArray();
    }

    private static IReadOnlyList<string> CollectMemberBodyReferences(
        MemberDeclarationSyntax member,
        SemanticModel? semanticModel)
    {
        var references = new HashSet<string>(StringComparer.Ordinal);

        foreach (var typeSyntax in member.DescendantNodes().OfType<TypeSyntax>())
        {
            AddReferences(TypeReferenceCollector.Collect(typeSyntax, semanticModel), references);
        }

        if (semanticModel is null)
        {
            return references.ToArray();
        }

        foreach (var invocation in member.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            AddSymbolReference(semanticModel.GetSymbolInfo(invocation).Symbol, references);
        }

        foreach (var creation in member.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            AddTypeReference(semanticModel.GetTypeInfo(creation).Type, references);
        }

        foreach (var creation in member.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
        {
            AddTypeReference(semanticModel.GetTypeInfo(creation).Type, references);
        }

        foreach (var memberAccess in member.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            AddSymbolReference(semanticModel.GetSymbolInfo(memberAccess).Symbol, references);
        }

        foreach (var identifier in member.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol is IMethodSymbol { IsStatic: true } or IPropertySymbol { IsStatic: true } or IFieldSymbol { IsStatic: true } or IEventSymbol { IsStatic: true })
            {
                AddSymbolReference(symbol, references);
            }
        }

        return references.ToArray();
    }

    private static void AddReferences(IEnumerable<string> values, HashSet<string> references)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                references.Add(value);
            }
        }
    }

    private static void AddSymbolReference(ISymbol? symbol, HashSet<string> references)
    {
        switch (symbol)
        {
            case IMethodSymbol method:
                AddTypeReference(method.ContainingType, references);
                AddTypeReference(method.ReturnType, references);
                AddReferences(SymbolTypeReferences.ToReferenceNames(method.Parameters.Select(parameter => parameter.Type)), references);
                AddReferences(SymbolTypeReferences.ToReferenceNames(method.TypeArguments), references);
                break;
            case IPropertySymbol property:
                AddTypeReference(property.ContainingType, references);
                AddTypeReference(property.Type, references);
                break;
            case IFieldSymbol field:
                AddTypeReference(field.ContainingType, references);
                AddTypeReference(field.Type, references);
                break;
            case IEventSymbol eventSymbol:
                AddTypeReference(eventSymbol.ContainingType, references);
                AddTypeReference(eventSymbol.Type, references);
                break;
            case ILocalSymbol local:
                AddTypeReference(local.Type, references);
                break;
            case IParameterSymbol parameter:
                AddTypeReference(parameter.Type, references);
                break;
            case INamedTypeSymbol namedType:
                AddTypeReference(namedType, references);
                break;
        }
    }

    private static void AddTypeReference(ITypeSymbol? symbol, HashSet<string> references)
    {
        var reference = SymbolTypeReferences.ToReferenceName(symbol);
        if (!string.IsNullOrWhiteSpace(reference))
        {
            references.Add(reference);
        }
    }

    private static IReadOnlyList<DiagramDependency> DistinctDependencies(IEnumerable<DiagramDependency> dependencies)
    {
        return dependencies
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency.TypeName))
            .DistinctBy(dependency => $"{NormalizeTypeName(dependency.TypeName)}:{dependency.Label}")
            .ToArray();
    }

    private static string NormalizeTypeName(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return string.Empty;
        }

        var value = typeName
            .Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace("?", string.Empty, StringComparison.Ordinal)
            .Trim();
        var genericStart = value.IndexOf('<', StringComparison.Ordinal);
        return genericStart >= 0 ? value[..genericStart] : value;
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
