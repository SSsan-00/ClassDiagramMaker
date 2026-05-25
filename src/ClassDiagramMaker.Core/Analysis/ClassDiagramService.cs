using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClassDiagramMaker.Analysis;

public sealed class ClassDiagramService
{
    public async Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        IProgress<GenerationProgress> progress,
        CancellationToken cancellationToken)
    {
        var options = NormalizeRequest(request);

        progress.Report(new GenerationProgress(
            "Scanning",
            "Resolving input files...",
            5,
            0,
            0));

        var files = ResolveFiles(options);
        if (files.Count == 0)
        {
            throw new InvalidOperationException("No C# files were found for the requested input.");
        }

        progress.Report(new GenerationProgress(
            "Parsing",
            $"Found {files.Count} C# file(s).",
            10,
            0,
            files.Count));

        var collectedTypes = new List<DiagramType>();

        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = files[index];
            var text = await File.ReadAllTextAsync(file, cancellationToken);
            var tree = CSharpSyntaxTree.ParseText(text, path: file, cancellationToken: cancellationToken);
            var root = (CompilationUnitSyntax)await tree.GetRootAsync(cancellationToken);

            collectedTypes.AddRange(SyntaxTypeCollector.Collect(root, file));

            var percent = 10 + (int)Math.Round(((index + 1) / (double)files.Count) * 55);
            progress.Report(new GenerationProgress(
                "Parsing",
                $"Parsed {Path.GetRelativePath(options.ProjectFolder, file)}",
                percent,
                index + 1,
                files.Count));
        }

        progress.Report(new GenerationProgress(
            "Merging",
            "Merging partial type declarations...",
            70,
            files.Count,
            files.Count));

        var types = MergePartialTypes(collectedTypes);

        progress.Report(new GenerationProgress(
            "Relationships",
            "Resolving inheritance and member relationships...",
            80,
            files.Count,
            files.Count));

        var relationships = RelationshipBuilder.Build(types);

        progress.Report(new GenerationProgress(
            "Rendering",
            "Rendering Mermaid class diagram...",
            90,
            files.Count,
            files.Count));

        var mermaid = MermaidRenderer.Render(types, relationships);

        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await File.WriteAllTextAsync(options.OutputPath, mermaid, new UTF8Encoding(false), cancellationToken);

        progress.Report(new GenerationProgress(
            "Writing",
            $"Wrote {options.OutputPath}",
            100,
            files.Count,
            files.Count));

        return new GenerationResult(
            options.OutputPath,
            mermaid,
            types.Count,
            relationships.Count);
    }

    private static NormalizedGenerationRequest NormalizeRequest(GenerationRequest request)
    {
        var projectFolder = NormalizePath(request.ProjectFolder);
        var searchFolder = NormalizePath(request.SearchFolder);
        var searchFile = NormalizeOptionalPath(request.SearchFile);
        var outputPath = NormalizePath(request.OutputPath);

        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            throw new ArgumentException("Project folder is required.");
        }

        if (!Directory.Exists(projectFolder))
        {
            throw new DirectoryNotFoundException($"Project folder does not exist: {projectFolder}");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.");
        }

        if (!string.IsNullOrWhiteSpace(searchFile))
        {
            if (!File.Exists(searchFile))
            {
                throw new FileNotFoundException($"Search file does not exist: {searchFile}", searchFile);
            }

            if (!string.Equals(Path.GetExtension(searchFile), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Search file must be a .cs file.");
            }

            searchFolder = Path.GetDirectoryName(searchFile) ?? projectFolder;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(searchFolder))
            {
                throw new ArgumentException("Search folder is required when search file is empty.");
            }

            if (!Directory.Exists(searchFolder))
            {
                throw new DirectoryNotFoundException($"Search folder does not exist: {searchFolder}");
            }
        }

        return new NormalizedGenerationRequest(projectFolder, searchFolder, searchFile, outputPath);
    }

    private static List<string> ResolveFiles(NormalizedGenerationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SearchFile))
        {
            return new List<string> { request.SearchFile };
        }

        return Directory.EnumerateFiles(request.SearchFolder, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsIgnoredPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".vs", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "node_modules", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (expanded.StartsWith("~/", StringComparison.Ordinal) || expanded == "~")
        {
            expanded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                expanded.Length == 1 ? string.Empty : expanded[2..]);
        }

        return Path.GetFullPath(expanded);
    }

    private static string? NormalizeOptionalPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : NormalizePath(path);
    }

    private static List<DiagramType> MergePartialTypes(IReadOnlyCollection<DiagramType> types)
    {
        return types
            .GroupBy(type => type.FullName, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                return first with
                {
                    SourceFile = string.Join(", ", group.Select(type => type.SourceFile).Distinct(StringComparer.OrdinalIgnoreCase)),
                    Modifiers = group.SelectMany(type => type.Modifiers).Distinct(StringComparer.Ordinal).OrderBy(value => value).ToArray(),
                    TypeParameterConstraints = group.SelectMany(type => type.TypeParameterConstraints).Distinct(StringComparer.Ordinal).ToArray(),
                    BaseTypes = group.SelectMany(type => type.BaseTypes).Distinct(StringComparer.Ordinal).ToArray(),
                    Members = group.SelectMany(type => type.Members)
                        .DistinctBy(member => $"{member.Kind}:{member.Signature}")
                        .OrderBy(member => member.Kind)
                        .ThenBy(member => member.Name, StringComparer.Ordinal)
                        .ToArray()
                };
            })
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
    }

    private sealed record NormalizedGenerationRequest(
        string ProjectFolder,
        string SearchFolder,
        string? SearchFile,
        string OutputPath);
}
