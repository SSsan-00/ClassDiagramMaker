using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClassDiagramMaker.Analysis;

public sealed class ClassDiagramService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".cshtml"
    };

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
            throw new InvalidOperationException("No supported source files were found for the requested input.");
        }

        progress.Report(new GenerationProgress(
            "Parsing",
            $"Found {files.Count} source file(s).",
            10,
            0,
            files.Count));

        var collectedTypes = new List<DiagramType>();

        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = files[index];
            var text = await File.ReadAllTextAsync(file, cancellationToken);
            if (IsRazorPageFile(file))
            {
                collectedTypes.AddRange(RazorPageCollector.Collect(text, file, options.ProjectFolder));
            }
            else
            {
                var tree = CSharpSyntaxTree.ParseText(text, path: file, cancellationToken: cancellationToken);
                var root = (CompilationUnitSyntax)await tree.GetRootAsync(cancellationToken);
                collectedTypes.AddRange(SyntaxTypeCollector.Collect(root, file));
            }

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

        var relationships = RelationshipBuilder.Build(types, request.Options);

        progress.Report(new GenerationProgress(
            "Rendering",
            request.Options.SplitOutput.Enabled
                ? "Rendering Mermaid class diagrams..."
                : "Rendering Mermaid class diagram...",
            90,
            files.Count,
            files.Count));

        var displayTypes = ApplyDisplayMode(types, request.Options.DisplayMode);
        var mermaid = MermaidRenderer.Render(displayTypes, relationships);
        var output = request.Options.SplitOutput.Enabled
            ? await WriteSplitOutputAsync(options, request.Options.SplitOutput, displayTypes, relationships, mermaid, cancellationToken)
            : await WriteSingleOutputAsync(options.OutputPath, mermaid, cancellationToken);

        progress.Report(new GenerationProgress(
            "Writing",
            $"Wrote {output.OutputPaths.Count} output file(s).",
            100,
            files.Count,
            files.Count));

        return new GenerationResult(
            output.PrimaryPath,
            output.PreviewMermaid,
            types.Count,
            relationships.Count)
        {
            OutputPaths = output.OutputPaths
        };
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

            if (!IsSupportedSourceFile(searchFile))
            {
                throw new ArgumentException("Search file must be a .cs, .cshtml.cs, or .cshtml file.");
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
            return ExpandSelectedSourceFile(request.SearchFile);
        }

        return Directory.EnumerateFiles(request.SearchFolder, "*", SearchOption.AllDirectories)
            .Where(path => IsSupportedSourceFile(path) && !IsIgnoredPath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ExpandSelectedSourceFile(string selectedFile)
    {
        var files = new List<string>();

        if (IsRazorCodeBehindFile(selectedFile))
        {
            var razorPageFile = selectedFile[..^".cs".Length];
            AddIfExists(files, razorPageFile);
            AddIfExists(files, selectedFile);
            return files;
        }

        AddIfExists(files, selectedFile);

        if (IsRazorPageFile(selectedFile))
        {
            AddIfExists(files, $"{selectedFile}.cs");
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddIfExists(List<string> files, string path)
    {
        if (File.Exists(path) && IsSupportedSourceFile(path) && !IsIgnoredPath(path))
        {
            files.Add(path);
        }
    }

    private static bool IsSupportedSourceFile(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path));
    }

    private static bool IsRazorPageFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".cshtml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRazorCodeBehindFile(string path)
    {
        return path.EndsWith(".cshtml.cs", StringComparison.OrdinalIgnoreCase);
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

    private static IReadOnlyList<DiagramType> ApplyDisplayMode(
        IReadOnlyList<DiagramType> types,
        DiagramDisplayMode displayMode)
    {
        return displayMode switch
        {
            DiagramDisplayMode.AllMembers => types,
            DiagramDisplayMode.TypeOnly => types
                .Select(type => type with { Members = Array.Empty<DiagramMember>() })
                .ToArray(),
            DiagramDisplayMode.KeyMembers => types
                .Select(type => type with
                {
                    Members = type.Members
                        .Where(member => member.Kind is
                            DiagramMemberKind.Field or
                            DiagramMemberKind.Property or
                            DiagramMemberKind.Event or
                            DiagramMemberKind.Indexer or
                            DiagramMemberKind.EnumValue)
                        .ToArray()
                })
                .ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(displayMode), displayMode, null)
        };
    }

    private static async Task<GeneratedOutput> WriteSingleOutputAsync(
        string outputPath,
        string mermaid,
        CancellationToken cancellationToken)
    {
        EnsureOutputDirectory(outputPath);
        await File.WriteAllTextAsync(outputPath, mermaid, new UTF8Encoding(false), cancellationToken);
        return new GeneratedOutput(outputPath, mermaid, new[] { outputPath });
    }

    private static async Task<GeneratedOutput> WriteSplitOutputAsync(
        NormalizedGenerationRequest request,
        DiagramSplitOptions splitOptions,
        IReadOnlyList<DiagramType> displayTypes,
        IReadOnlyList<DiagramRelationship> relationships,
        string overviewMermaid,
        CancellationToken cancellationToken)
    {
        EnsureOutputDirectory(request.OutputPath);

        var outputs = new List<string>();
        var splitDiagrams = CreateSplitDiagrams(request, splitOptions, displayTypes, relationships);
        var overviewPath = CreateSiblingOutputPath(request.OutputPath, "all", ".mmd");
        var indexPath = CreateSiblingOutputPath(request.OutputPath, "index", ".md");
        var fallbackPath = CreateSiblingOutputPath(request.OutputPath, "empty", ".mmd");

        if (splitOptions.IncludeOverview)
        {
            await File.WriteAllTextAsync(overviewPath, overviewMermaid, new UTF8Encoding(false), cancellationToken);
            outputs.Add(overviewPath);
        }

        if (splitDiagrams.Count == 0 && !splitOptions.IncludeOverview && !splitOptions.IncludeIndex)
        {
            await File.WriteAllTextAsync(fallbackPath, overviewMermaid, new UTF8Encoding(false), cancellationToken);
            outputs.Add(fallbackPath);
        }

        foreach (var diagram in splitDiagrams)
        {
            await File.WriteAllTextAsync(diagram.Path, diagram.Mermaid, new UTF8Encoding(false), cancellationToken);
            outputs.Add(diagram.Path);
        }

        if (splitOptions.IncludeIndex)
        {
            var index = RenderSplitIndex(indexPath, splitOptions, splitDiagrams, splitOptions.IncludeOverview ? overviewPath : null);
            await File.WriteAllTextAsync(indexPath, index, new UTF8Encoding(false), cancellationToken);
            outputs.Insert(0, indexPath);
        }

        var primaryPath = splitOptions.IncludeIndex
            ? indexPath
            : splitOptions.IncludeOverview
                ? overviewPath
                : splitDiagrams.Count > 0
                    ? splitDiagrams.First().Path
                    : fallbackPath;
        var previewMermaid = splitOptions.IncludeOverview || splitDiagrams.Count == 0
            ? overviewMermaid
            : splitDiagrams.First().Mermaid;

        return new GeneratedOutput(primaryPath, previewMermaid, outputs);
    }

    private static IReadOnlyList<SplitDiagram> CreateSplitDiagrams(
        NormalizedGenerationRequest request,
        DiagramSplitOptions splitOptions,
        IReadOnlyList<DiagramType> displayTypes,
        IReadOnlyList<DiagramRelationship> relationships)
    {
        var groups = displayTypes
            .GroupBy(type => GetSplitGroupName(type, request.ProjectFolder, splitOptions.Mode), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        var usedSuffixes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var diagrams = new List<SplitDiagram>();

        foreach (var group in groups)
        {
            var groupTypes = group
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
            var typeIds = groupTypes.Select(type => type.Id).ToHashSet(StringComparer.Ordinal);
            var groupRelationships = relationships
                .Where(relationship => typeIds.Contains(relationship.FromTypeId) && typeIds.Contains(relationship.ToTypeId))
                .ToArray();
            var fileSuffix = CreateUniqueFileSuffix(SanitizeFileSuffix(group.Key), usedSuffixes);
            var path = CreateSiblingOutputPath(request.OutputPath, fileSuffix, ".mmd");

            diagrams.Add(new SplitDiagram(
                group.Key,
                path,
                MermaidRenderer.Render(groupTypes, groupRelationships),
                groupTypes.Length,
                groupRelationships.Length));
        }

        return diagrams;
    }

    private static string GetSplitGroupName(
        DiagramType type,
        string projectFolder,
        DiagramSplitMode mode)
    {
        return mode switch
        {
            DiagramSplitMode.Namespace => string.IsNullOrWhiteSpace(type.Namespace) ? "Global" : type.Namespace,
            DiagramSplitMode.Folder => GetFolderGroupName(type.SourceFile, projectFolder),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private static string GetFolderGroupName(string sourceFile, string projectFolder)
    {
        var primarySourceFile = sourceFile
            .Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? sourceFile;
        var relativePath = Path.GetRelativePath(projectFolder, primarySourceFile);
        var relativeDirectory = Path.GetDirectoryName(relativePath);
        if (string.IsNullOrWhiteSpace(relativeDirectory) || relativeDirectory == ".")
        {
            return "Root";
        }

        return string.Join(
            ".",
            relativeDirectory
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string RenderSplitIndex(
        string indexPath,
        DiagramSplitOptions splitOptions,
        IReadOnlyList<SplitDiagram> splitDiagrams,
        string? overviewPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Class Diagram Index");
        builder.AppendLine();
        builder.AppendLine($"Split mode: `{splitOptions.Mode}`");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(overviewPath))
        {
            builder.AppendLine($"- [All]({ToMarkdownLinkTarget(indexPath, overviewPath)})");
        }

        foreach (var diagram in splitDiagrams)
        {
            builder.AppendLine($"- [{diagram.Name}]({ToMarkdownLinkTarget(indexPath, diagram.Path)}) - {diagram.TypeCount} type(s), {diagram.RelationshipCount} relationship(s)");
        }

        return builder.ToString();
    }

    private static string ToMarkdownLinkTarget(string fromPath, string toPath)
    {
        var fromDirectory = Path.GetDirectoryName(fromPath);
        var relativePath = string.IsNullOrWhiteSpace(fromDirectory)
            ? Path.GetFileName(toPath)
            : Path.GetRelativePath(fromDirectory, toPath);

        return relativePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string CreateSiblingOutputPath(string outputPath, string suffix, string extension)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        var baseName = Path.GetFileNameWithoutExtension(outputPath);
        return string.IsNullOrWhiteSpace(outputDirectory)
            ? $"{baseName}.{suffix}{extension}"
            : Path.Combine(outputDirectory, $"{baseName}.{suffix}{extension}");
    }

    private static string SanitizeFileSuffix(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars()
            .Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' })
            .ToHashSet();
        var sanitized = new string(value
            .Select(character => char.IsWhiteSpace(character) || invalidCharacters.Contains(character) ? '_' : character)
            .ToArray())
            .Trim('_', '.');

        return string.IsNullOrWhiteSpace(sanitized) ? "Global" : sanitized;
    }

    private static string CreateUniqueFileSuffix(
        string preferredSuffix,
        Dictionary<string, int> usedSuffixes)
    {
        if (!usedSuffixes.TryGetValue(preferredSuffix, out var count))
        {
            usedSuffixes[preferredSuffix] = 1;
            return preferredSuffix;
        }

        count++;
        usedSuffixes[preferredSuffix] = count;
        return $"{preferredSuffix}_{count}";
    }

    private static void EnsureOutputDirectory(string outputPath)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
    }

    private sealed record NormalizedGenerationRequest(
        string ProjectFolder,
        string SearchFolder,
        string? SearchFile,
        string OutputPath);

    private sealed record GeneratedOutput(
        string PrimaryPath,
        string PreviewMermaid,
        IReadOnlyList<string> OutputPaths);

    private sealed record SplitDiagram(
        string Name,
        string Path,
        string Mermaid,
        int TypeCount,
        int RelationshipCount);
}
