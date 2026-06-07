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

            foreach (var stereotype in GetStereotypes(type))
            {
                builder.AppendLine($"        <<{stereotype}>>");
            }

            foreach (var member in type.Members)
            {
                builder.AppendLine($"        {RenderMember(member)}");
            }

            builder.AppendLine("    }");
            foreach (var note in GetNotes(type))
            {
                builder.AppendLine($"    note for {type.Id} \"{note}\"");
            }

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

    private static IReadOnlyList<string> GetStereotypes(DiagramType type)
    {
        var stereotypes = new List<string>();
        var kindStereotype = type.Kind switch
        {
            DiagramTypeKind.Interface => "interface",
            DiagramTypeKind.Struct => "struct",
            DiagramTypeKind.Record => "record",
            DiagramTypeKind.Enum => "enumeration",
            DiagramTypeKind.RazorPage => "razor page",
            DiagramTypeKind.Delegate => "delegate",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(kindStereotype))
        {
            stereotypes.Add(kindStereotype);
        }

        stereotypes.AddRange(type.Modifiers);
        return stereotypes;
    }

    private static IReadOnlyList<string> GetNotes(DiagramType type)
    {
        var notes = new List<string>();
        notes.AddRange(type.TypeParameterConstraints);

        foreach (var member in type.Members)
        {
            if (member.Modifiers.Count > 0)
            {
                notes.Add($"{member.Name} modifiers: {string.Join(" ", member.Modifiers)}");
            }

            var constraints = ExtractConstraints(member.Signature).Constraints;
            if (!string.IsNullOrWhiteSpace(constraints))
            {
                notes.Add($"{member.Name} constraints: {constraints}");
            }
        }

        return notes.Count == 0
            ? Array.Empty<string>()
            : new[] { string.Join("\\n", notes.Select(EscapeNoteLine)) };
    }

    private static string RenderMember(DiagramMember member)
    {
        return member.Kind switch
        {
            DiagramMemberKind.Field or DiagramMemberKind.Property or DiagramMemberKind.Event => RenderAttribute(member),
            DiagramMemberKind.Method or DiagramMemberKind.Constructor => RenderOperation(member),
            DiagramMemberKind.Indexer => RenderIndexer(member),
            DiagramMemberKind.EnumValue => ToMermaidIdentifier(member.Name),
            _ => EscapeMemberText(member.Signature)
        };
    }

    private static string RenderAttribute(DiagramMember member)
    {
        var type = ToMermaidType(member.Type);
        var name = ToMermaidIdentifier(member.Name);
        var classifier = member.IsStatic ? "$" : string.Empty;
        return string.IsNullOrWhiteSpace(type)
            ? $"{member.Visibility}{name}{classifier}"
            : $"{member.Visibility}{type} {name}{classifier}";
    }

    private static string RenderOperation(DiagramMember member)
    {
        var signature = StripVisibilityAndModifiers(member.Signature);
        signature = ExtractConstraints(signature).Signature;

        var openParenIndex = signature.IndexOf('(', StringComparison.Ordinal);
        var closeParenIndex = signature.LastIndexOf(')');
        if (openParenIndex < 0 || closeParenIndex < openParenIndex)
        {
            return EscapeMemberText(member.Signature);
        }

        var name = ToMermaidOperationName(signature[..openParenIndex]);
        var parameters = RenderParameters(signature[(openParenIndex + 1)..closeParenIndex]);
        var returnType = ExtractReturnType(signature[(closeParenIndex + 1)..]);
        var classifier = GetOperationClassifiers(member);
        var rendered = $"{member.Visibility}{name}({parameters})";
        if (!string.IsNullOrWhiteSpace(returnType))
        {
            rendered = $"{rendered} {returnType}";
        }

        return $"{rendered}{classifier}";
    }

    private static string RenderIndexer(DiagramMember member)
    {
        var signature = StripVisibilityAndModifiers(member.Signature);
        var openBracketIndex = signature.IndexOf('[', StringComparison.Ordinal);
        var closeBracketIndex = signature.LastIndexOf(']');
        if (openBracketIndex < 0 || closeBracketIndex < openBracketIndex)
        {
            return RenderOperation(member);
        }

        var parameters = RenderParameters(signature[(openBracketIndex + 1)..closeBracketIndex]);
        var returnType = ExtractReturnType(signature[(closeBracketIndex + 1)..]);
        var classifier = GetOperationClassifiers(member);
        return string.IsNullOrWhiteSpace(returnType)
            ? $"{member.Visibility}this({parameters}){classifier}"
            : $"{member.Visibility}this({parameters}) {returnType}{classifier}";
    }

    private static string StripVisibilityAndModifiers(string signature)
    {
        var value = signature.Trim();
        if (value.Length > 0 && "+-#~".IndexOf(value[0]) >= 0)
        {
            value = value[1..].TrimStart();
        }

        if (value.StartsWith('{'))
        {
            var closeBraceIndex = value.IndexOf('}', StringComparison.Ordinal);
            if (closeBraceIndex >= 0)
            {
                value = value[(closeBraceIndex + 1)..].TrimStart();
            }
        }

        return value;
    }

    private static (string Signature, string? Constraints) ExtractConstraints(string signature)
    {
        var whereIndex = signature.IndexOf(" where ", StringComparison.Ordinal);
        if (whereIndex < 0)
        {
            return (signature, null);
        }

        return (signature[..whereIndex].TrimEnd(), signature[(whereIndex + 1)..].Trim());
    }

    private static string ExtractReturnType(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith(':'))
        {
            return string.Empty;
        }

        return ToMermaidType(trimmed[1..]);
    }

    private static string RenderParameters(string value)
    {
        return string.Join(", ", SplitParameters(value).Select(RenderParameter));
    }

    private static string RenderParameter(string value)
    {
        var colonIndex = value.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex < 0)
        {
            return ToMermaidType(value);
        }

        var name = ToMermaidIdentifier(value[..colonIndex].Trim());
        var type = ToMermaidType(value[(colonIndex + 1)..].Trim());
        return string.IsNullOrWhiteSpace(type) ? name : $"{type} {name}";
    }

    private static IEnumerable<string> SplitParameters(string value)
    {
        var start = 0;
        var genericDepth = 0;
        var tupleDepth = 0;
        var bracketDepth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '<':
                    genericDepth++;
                    break;
                case '>':
                    genericDepth = Math.Max(0, genericDepth - 1);
                    break;
                case '(':
                    tupleDepth++;
                    break;
                case ')':
                    tupleDepth = Math.Max(0, tupleDepth - 1);
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                    break;
                case ',' when genericDepth == 0 && tupleDepth == 0 && bracketDepth == 0:
                    yield return value[start..index].Trim();
                    start = index + 1;
                    break;
            }
        }

        var last = value[start..].Trim();
        if (!string.IsNullOrWhiteSpace(last))
        {
            yield return last;
        }
    }

    private static string GetOperationClassifiers(DiagramMember member)
    {
        var builder = new StringBuilder();
        if (member.IsStatic || member.Modifiers.Contains("static", StringComparer.Ordinal))
        {
            builder.Append('$');
        }

        if (member.Modifiers.Contains("abstract", StringComparer.Ordinal))
        {
            builder.Append('*');
        }

        return builder.ToString();
    }

    private static string ToMermaidOperationName(string value)
    {
        return ToMermaidType(value.Trim());
    }

    private static string ToMermaidType(string value)
    {
        var text = Regex.Replace(value, @"\s+", " ")
            .Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace("<", "~", StringComparison.Ordinal)
            .Replace(">", "~", StringComparison.Ordinal);
        text = Regex.Replace(text, @"[^A-Za-z0-9_\.\?~]", "_");
        text = Regex.Replace(text, "_+", "_").Trim('_');
        return text;
    }

    private static string ToMermaidIdentifier(string value)
    {
        var identifier = Regex.Replace(value.Trim(), @"[^A-Za-z0-9_]", "_");
        identifier = Regex.Replace(identifier, "_+", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return "member";
        }

        return char.IsDigit(identifier[0]) ? $"m_{identifier}" : identifier;
    }

    private static string EscapeNoteLine(string value)
    {
        return Regex.Replace(value, @"\s+", " ")
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Trim();
    }

    private static string EscapeMemberText(string value)
    {
        return Regex.Replace(value, @"\s+", " ")
            .Replace("<", "~", StringComparison.Ordinal)
            .Replace(">", "~", StringComparison.Ordinal)
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
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
