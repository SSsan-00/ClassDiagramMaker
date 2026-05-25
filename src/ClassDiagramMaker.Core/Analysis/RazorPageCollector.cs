using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClassDiagramMaker.Analysis;

internal static partial class RazorPageCollector
{
    public static IReadOnlyList<DiagramType> Collect(string source, string sourceFile, string projectFolder)
    {
        var fullName = GetPageFullName(sourceFile, projectFolder);
        var namespaceName = GetPageNamespace(fullName);
        var members = new List<DiagramMember>();

        var modelType = FindModelType(source);
        if (!string.IsNullOrWhiteSpace(modelType))
        {
            members.Add(CreateRazorMember("Model", modelType));
        }

        members.AddRange(FindInjectedServices(source).Select(injection => CreateRazorMember(injection.Name, injection.Type)));
        members.AddRange(CollectFunctionMembers(source, sourceFile));

        var simpleName = fullName.Split('.').Last();
        return new[]
        {
            new DiagramType
            {
                Id = MermaidNames.ToId(fullName),
                SimpleName = simpleName,
                DisplayName = simpleName,
                FullName = fullName,
                Namespace = namespaceName,
                SourceFile = sourceFile,
                Kind = DiagramTypeKind.RazorPage,
                Accessibility = "public",
                Members = members
                    .DistinctBy(member => $"{member.Kind}:{member.Signature}")
                    .OrderBy(member => member.Kind)
                    .ThenBy(member => member.Name, StringComparer.Ordinal)
                    .ToArray()
            }
        };
    }

    private static string? FindModelType(string source)
    {
        var match = ModelDirectivePattern().Match(source);
        return match.Success ? match.Groups["type"].Value.Trim() : null;
    }

    private static IEnumerable<RazorInjection> FindInjectedServices(string source)
    {
        return InjectDirectivePattern()
            .Matches(source)
            .Select(match => new RazorInjection(
                match.Groups["type"].Value.Trim(),
                match.Groups["name"].Value.Trim()));
    }

    private static DiagramMember CreateRazorMember(string name, string type)
    {
        return new DiagramMember
        {
            Kind = DiagramMemberKind.Property,
            Name = name,
            Type = type,
            Visibility = "+",
            Signature = $"+{name}: {type}",
            ReferencedTypes = TypeReferenceCollector.Collect(SyntaxFactory.ParseTypeName(type))
        };
    }

    private static IEnumerable<DiagramMember> CollectFunctionMembers(string source, string sourceFile)
    {
        foreach (var block in ExtractDirectiveBlocks(source, "@functions").Concat(ExtractDirectiveBlocks(source, "@code")))
        {
            var tree = CSharpSyntaxTree.ParseText($"public class RazorPageMembers {{\n{block}\n}}", path: sourceFile);
            var root = (CompilationUnitSyntax)tree.GetRoot();
            var wrapperType = SyntaxTypeCollector.Collect(root, sourceFile).FirstOrDefault();
            if (wrapperType is not null)
            {
                foreach (var member in wrapperType.Members)
                {
                    yield return member;
                }
            }
        }
    }

    private static IEnumerable<string> ExtractDirectiveBlocks(string source, string directive)
    {
        var searchIndex = 0;
        while (searchIndex < source.Length)
        {
            var directiveIndex = source.IndexOf(directive, searchIndex, StringComparison.Ordinal);
            if (directiveIndex < 0)
            {
                yield break;
            }

            var openBraceIndex = source.IndexOf('{', directiveIndex + directive.Length);
            if (openBraceIndex < 0)
            {
                yield break;
            }

            var closeBraceIndex = FindMatchingBrace(source, openBraceIndex);
            if (closeBraceIndex < 0)
            {
                yield break;
            }

            yield return source[(openBraceIndex + 1)..closeBraceIndex];
            searchIndex = closeBraceIndex + 1;
        }
    }

    private static int FindMatchingBrace(string source, int openBraceIndex)
    {
        var depth = 0;
        for (var index = openBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static string GetPageFullName(string sourceFile, string projectFolder)
    {
        var relativePath = Path.GetRelativePath(projectFolder, sourceFile);
        var withoutExtension = relativePath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
            ? relativePath[..^".cshtml".Length]
            : Path.ChangeExtension(relativePath, null);
        var parts = withoutExtension
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(ToIdentifierPart)
            .ToArray();

        return parts.Length == 0 ? ToIdentifierPart(Path.GetFileNameWithoutExtension(sourceFile)) : string.Join(".", parts);
    }

    private static string GetPageNamespace(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot < 0 ? string.Empty : fullName[..lastDot];
    }

    private static string ToIdentifierPart(string value)
    {
        var sanitized = InvalidIdentifierCharacterPattern().Replace(value, "_").Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "RazorPage" : sanitized;
    }

    private sealed record RazorInjection(string Type, string Name);

    [GeneratedRegex(@"^\s*@model\s+(?<type>[^\r\n]+)\s*$", RegexOptions.Multiline)]
    private static partial Regex ModelDirectivePattern();

    [GeneratedRegex(@"^\s*@inject\s+(?<type>.+?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*$", RegexOptions.Multiline)]
    private static partial Regex InjectDirectivePattern();

    [GeneratedRegex(@"[^A-Za-z0-9_\.]")]
    private static partial Regex InvalidIdentifierCharacterPattern();
}
