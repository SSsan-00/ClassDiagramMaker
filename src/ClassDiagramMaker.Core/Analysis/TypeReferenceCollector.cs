using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

namespace ClassDiagramMaker.Analysis;

internal static class TypeReferenceCollector
{
    public static IReadOnlyList<string> Collect(TypeSyntax? type, SemanticModel? semanticModel = null)
    {
        if (type is null)
        {
            return Array.Empty<string>();
        }

        var references = new HashSet<string>(StringComparer.Ordinal);
        Add(type, references, semanticModel);
        return references.ToArray();
    }

    public static string? GetPrimaryTypeName(TypeSyntax? type, SemanticModel? semanticModel = null)
    {
        if (type is null)
        {
            return null;
        }

        return TryGetSemanticTypeName(type, semanticModel) ?? type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.ToString(),
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.ToString(),
            NullableTypeSyntax nullable => GetPrimaryTypeName(nullable.ElementType, semanticModel),
            ArrayTypeSyntax array => GetPrimaryTypeName(array.ElementType, semanticModel),
            PointerTypeSyntax pointer => GetPrimaryTypeName(pointer.ElementType, semanticModel),
            _ => null
        };
    }

    private static void Add(TypeSyntax type, HashSet<string> references, SemanticModel? semanticModel)
    {
        var semanticTypeName = TryGetSemanticTypeName(type, semanticModel);
        if (!string.IsNullOrWhiteSpace(semanticTypeName))
        {
            references.Add(semanticTypeName);
        }

        switch (type)
        {
            case PredefinedTypeSyntax:
                return;

            case IdentifierNameSyntax identifier:
                if (semanticTypeName is null)
                {
                    references.Add(identifier.Identifier.ValueText);
                }
                return;

            case GenericNameSyntax generic:
                if (semanticTypeName is null)
                {
                    references.Add(generic.Identifier.ValueText);
                }
                foreach (var argument in generic.TypeArgumentList.Arguments)
                {
                    Add(argument, references, semanticModel);
                }
                return;

            case QualifiedNameSyntax qualified:
                if (semanticTypeName is null)
                {
                    references.Add(qualified.ToString());
                }
                Add(qualified.Right, references, semanticModel);
                return;

            case AliasQualifiedNameSyntax aliasQualified:
                if (semanticTypeName is null)
                {
                    references.Add(aliasQualified.Name.ToString());
                }
                Add(aliasQualified.Name, references, semanticModel);
                return;

            case NullableTypeSyntax nullable:
                Add(nullable.ElementType, references, semanticModel);
                return;

            case ArrayTypeSyntax array:
                Add(array.ElementType, references, semanticModel);
                return;

            case PointerTypeSyntax pointer:
                Add(pointer.ElementType, references, semanticModel);
                return;

            case TupleTypeSyntax tuple:
                foreach (var element in tuple.Elements)
                {
                    Add(element.Type, references, semanticModel);
                }
                return;
        }
    }

    private static string? TryGetSemanticTypeName(TypeSyntax type, SemanticModel? semanticModel)
    {
        if (semanticModel is null)
        {
            return null;
        }

        var typeInfo = semanticModel.GetTypeInfo(type);
        var symbol = typeInfo.Type ?? typeInfo.ConvertedType;
        return SymbolTypeReferences.ToReferenceName(symbol);
    }
}

internal static class SymbolTypeReferences
{
    private static readonly SymbolDisplayFormat ReferenceFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static string? ToReferenceName(ITypeSymbol? symbol)
    {
        return symbol switch
        {
            null => null,
            IArrayTypeSymbol array => ToReferenceName(array.ElementType),
            IPointerTypeSymbol pointer => ToReferenceName(pointer.PointedAtType),
            INamedTypeSymbol named when named.SpecialType != SpecialType.None => null,
            INamedTypeSymbol named => named.ToDisplayString(ReferenceFormat),
            _ => null
        };
    }

    public static IEnumerable<string> ToReferenceNames(IEnumerable<ITypeSymbol> symbols)
    {
        return symbols
            .Select(ToReferenceName)
            .Where(name => !string.IsNullOrWhiteSpace(name))!;
    }
}
