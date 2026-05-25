using System.Text;
using System.Text.RegularExpressions;

namespace ClassDiagramMaker.Analysis;

internal static class MermaidRenderer
{
    public static string Render(
        IReadOnlyList<DiagramType> types,
        IReadOnlyList<DiagramRelationship> relationships)
    {
        var builder = new StringBuilder();
        builder.AppendLine("classDiagram");
        builder.AppendLine("    direction LR");
        builder.AppendLine();

        foreach (var type in types)
        {
            builder.AppendLine($"    %% {type.FullName}");
            builder.AppendLine($"    class {type.Id} {{");

            var stereotype = GetStereotype(type);
            if (!string.IsNullOrWhiteSpace(stereotype))
            {
                builder.AppendLine($"        <<{stereotype}>>");
            }

            foreach (var member in type.Members)
            {
                builder.AppendLine($"        {EscapeMemberText(member.Signature)}");
            }

            builder.AppendLine("    }");
            builder.AppendLine();
        }

        foreach (var relationship in relationships)
        {
            builder.AppendLine(RenderRelationship(relationship));
        }

        return builder.ToString();
    }

    private static string RenderRelationship(DiagramRelationship relationship)
    {
        var label = string.IsNullOrWhiteSpace(relationship.Label)
            ? string.Empty
            : $" : {EscapeLabel(relationship.Label)}";

        return relationship.Kind switch
        {
            DiagramRelationshipKind.Inheritance => $"    {relationship.ToTypeId} <|-- {relationship.FromTypeId}",
            DiagramRelationshipKind.Realization => $"    {relationship.ToTypeId} <|.. {relationship.FromTypeId}",
            DiagramRelationshipKind.Association => $"    {relationship.FromTypeId} --> {relationship.ToTypeId}{label}",
            DiagramRelationshipKind.Dependency => $"    {relationship.FromTypeId} ..> {relationship.ToTypeId}{label}",
            _ => throw new ArgumentOutOfRangeException(nameof(relationship))
        };
    }

    private static string GetStereotype(DiagramType type)
    {
        return type.Kind switch
        {
            DiagramTypeKind.Interface => "interface",
            DiagramTypeKind.Struct => "struct",
            DiagramTypeKind.Record => "record",
            DiagramTypeKind.Enum => "enumeration",
            _ => string.Empty
        };
    }

    private static string EscapeMemberText(string value)
    {
        return Regex.Replace(value, @"\s+", " ")
            .Replace("<", "~", StringComparison.Ordinal)
            .Replace(">", "~", StringComparison.Ordinal)
            .Replace("{", "(", StringComparison.Ordinal)
            .Replace("}", ")", StringComparison.Ordinal)
            .Trim();
    }

    private static string EscapeLabel(string value)
    {
        return Regex.Replace(value, @"\s+", " ")
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Trim();
    }
}

internal static class MermaidNames
{
    private static readonly Regex InvalidCharacterPattern = new("[^A-Za-z0-9_]", RegexOptions.Compiled);

    public static string ToId(string value)
    {
        var id = InvalidCharacterPattern.Replace(value, "_");
        id = Regex.Replace(id, "_+", "_").Trim('_');
        return id.Length > 0 && char.IsDigit(id[0])
            ? $"T_{id}"
            : id;
    }
}
