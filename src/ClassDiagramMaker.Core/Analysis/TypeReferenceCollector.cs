using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClassDiagramMaker.Analysis;

internal static class TypeReferenceCollector
{
    public static IReadOnlyList<string> Collect(TypeSyntax? type)
    {
        if (type is null)
        {
            return Array.Empty<string>();
        }

        var references = new HashSet<string>(StringComparer.Ordinal);
        Add(type, references);
        return references.ToArray();
    }

    private static void Add(TypeSyntax type, HashSet<string> references)
    {
        switch (type)
        {
            case PredefinedTypeSyntax:
                return;

            case IdentifierNameSyntax identifier:
                references.Add(identifier.Identifier.ValueText);
                return;

            case GenericNameSyntax generic:
                references.Add(generic.Identifier.ValueText);
                foreach (var argument in generic.TypeArgumentList.Arguments)
                {
                    Add(argument, references);
                }
                return;

            case QualifiedNameSyntax qualified:
                references.Add(qualified.ToString());
                Add(qualified.Right, references);
                return;

            case AliasQualifiedNameSyntax aliasQualified:
                references.Add(aliasQualified.Name.ToString());
                Add(aliasQualified.Name, references);
                return;

            case NullableTypeSyntax nullable:
                Add(nullable.ElementType, references);
                return;

            case ArrayTypeSyntax array:
                Add(array.ElementType, references);
                return;

            case PointerTypeSyntax pointer:
                Add(pointer.ElementType, references);
                return;

            case TupleTypeSyntax tuple:
                foreach (var element in tuple.Elements)
                {
                    Add(element.Type, references);
                }
                return;
        }
    }
}
