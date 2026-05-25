#!/usr/bin/env bash
set -euo pipefail

TARGET_DIR="${1:-ClassDiagramMaker}"
mkdir -p "$TARGET_DIR"

mkdir -p "$(dirname "$TARGET_DIR/.gitignore")"
cat > "$TARGET_DIR/.gitignore" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
bin/
obj/
.vs/
.idea/
.vscode/
*.user
*.suo
*.swp
.DS_Store

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/global.json")"
cat > "$TARGET_DIR/global.json" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
{
  "sdk": {
    "version": "9.0.202",
    "rollForward": "latestFeature"
  }
}

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/ClassDiagramMaker.sln")"
cat > "$TARGET_DIR/ClassDiagramMaker.sln" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
﻿
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{827E0CD3-B72D-47B6-A68D-7590B98EB39B}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ClassDiagramMaker", "src\ClassDiagramMaker\ClassDiagramMaker.csproj", "{87BBB3F1-1552-4D14-A60B-D34B3133984F}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{87BBB3F1-1552-4D14-A60B-D34B3133984F}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{87BBB3F1-1552-4D14-A60B-D34B3133984F}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{87BBB3F1-1552-4D14-A60B-D34B3133984F}.Debug|x64.ActiveCfg = Debug|Any CPU
		{87BBB3F1-1552-4D14-A60B-D34B3133984F}.Debug|x64.Build.0 = Debug|Any CPU
		{87BBB3F1-1552-4D14-A60B-D34B3133984F}.Debug|x86.ActiveCfg = Debug|Any CPU
		{87BBB3F1-1552-4D14-A60B-D34B3133984F}.Debug|x86.Build.0 = Debug|Any CPU
		{87BBB3F1-1552-4D14-A60B-D34B3133984F}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{87BBB3F1-1552-4D14-A60B-D34B3133984F}.Release|Any CPU.Build.0 = Release|Any CPU
		{87BBB3F1-1552-4D14-A60B-D34B3133984F}.Release|x64.ActiveCfg = Release|Any CPU
		{87BBB3F1-1552-4D14-A60B-D34B3133984F}.Release|x64.Build.0 = Release|Any CPU
		{87BBB3F1-1552-4D14-A60B-D34B3133984F}.Release|x86.ActiveCfg = Release|Any CPU
		{87BBB3F1-1552-4D14-A60B-D34B3133984F}.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		{87BBB3F1-1552-4D14-A60B-D34B3133984F} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
	EndGlobalSection
EndGlobal

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/README.md")"
cat > "$TARGET_DIR/README.md" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
# ClassDiagramMaker

C# source analyzer for generating Mermaid class diagrams from selected files and directories.

## Requirements

- .NET SDK 9.0

The repository includes `global.json` to use the .NET 9 SDK even when newer SDKs are installed.

## Run

```bash
dotnet restore
dotnet run --project src/ClassDiagramMaker/ClassDiagramMaker.csproj
```

Open the URL printed by `dotnet run`, then fill in:

- Target project folder
- Search folder
- Search file, optional
- Output path for the generated `.mmd` file

When the search file is empty, the tool recursively analyzes `.cs` files under the search folder. The GUI shows parsing and rendering progress while the Mermaid file is generated.

## Output

The first supported output format is Mermaid `classDiagram`.

```mermaid
classDiagram
    direction LR
    UserRepository <|.. UserService
    UserService --> UserRepository : repository
```

## Bootstrap

For users who cannot download the repository, this project provides a generated single-file bootstrap script:

```bash
./bootstrap/ClassDiagramMaker.bootstrap.sh ./ClassDiagramMaker
```

The script recreates the source tree locally. Regenerate it after source changes with:

```bash
./tools/generate-bootstrap.sh
```

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/ClassDiagramMaker.csproj")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/ClassDiagramMaker.csproj" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>ClassDiagramMaker</RootNamespace>
    <AssemblyName>ClassDiagramMaker</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
  </ItemGroup>
</Project>

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/Program.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/Program.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
using System.Text.Json.Serialization;
using ClassDiagramMaker.Analysis;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = ResolveContentRoot(),
    WebRootPath = "wwwroot"
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<ClassDiagramService>();
builder.Services.AddSingleton<DiagramJobStore>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/generate", (GenerationRequest request, DiagramJobStore jobs, ClassDiagramService service) =>
{
    var job = jobs.Create(request);

    _ = Task.Run(async () =>
    {
        try
        {
            jobs.Update(job.Id, snapshot => snapshot with
            {
                Status = JobStatus.Running,
                Message = "Starting analysis..."
            });

            var progress = new Progress<GenerationProgress>(update =>
            {
                jobs.Update(job.Id, snapshot => snapshot with
                {
                    Status = JobStatus.Running,
                    Percent = update.Percent,
                    Stage = update.Stage,
                    Message = update.Message,
                    ProcessedFiles = update.ProcessedFiles,
                    TotalFiles = update.TotalFiles,
                    Log = snapshot.Log.Append(update.Message).TakeLast(80).ToArray()
                });
            });

            var result = await service.GenerateAsync(request, progress, CancellationToken.None);

            jobs.Update(job.Id, snapshot => snapshot with
            {
                Status = JobStatus.Completed,
                Percent = 100,
                Stage = "Completed",
                Message = $"Generated {result.TypeCount} types and {result.RelationshipCount} relationships.",
                OutputPath = result.OutputPath,
                Mermaid = result.Mermaid,
                FinishedAt = DateTimeOffset.UtcNow,
                Log = snapshot.Log
                    .Append($"Wrote Mermaid output: {result.OutputPath}")
                    .Append($"Types: {result.TypeCount}, relationships: {result.RelationshipCount}")
                    .TakeLast(80)
                    .ToArray()
            });
        }
        catch (Exception ex)
        {
            jobs.Update(job.Id, snapshot => snapshot with
            {
                Status = JobStatus.Failed,
                Stage = "Failed",
                Message = ex.Message,
                Error = ex.ToString(),
                FinishedAt = DateTimeOffset.UtcNow,
                Log = snapshot.Log.Append(ex.Message).TakeLast(80).ToArray()
            });
        }
    });

    return Results.Accepted($"/api/jobs/{job.Id}", new { job.Id });
});

app.MapGet("/api/jobs/{id:guid}", (Guid id, DiagramJobStore jobs) =>
{
    var snapshot = jobs.Get(id);
    return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
});

app.MapGet("/api/jobs/{id:guid}/output", (Guid id, DiagramJobStore jobs) =>
{
    var snapshot = jobs.Get(id);
    if (snapshot is null)
    {
        return Results.NotFound();
    }

    if (snapshot.Status != JobStatus.Completed || string.IsNullOrWhiteSpace(snapshot.Mermaid))
    {
        return Results.BadRequest(new { error = "Output is not ready." });
    }

    return Results.Text(snapshot.Mermaid, "text/plain");
});

app.Run();

static string ResolveContentRoot()
{
    var publishedRoot = AppContext.BaseDirectory;
    if (Directory.Exists(Path.Combine(publishedRoot, "wwwroot")))
    {
        return publishedRoot;
    }

    var sourceRoot = Path.GetFullPath(Path.Combine(publishedRoot, "..", "..", ".."));
    if (Directory.Exists(Path.Combine(sourceRoot, "wwwroot")))
    {
        return sourceRoot;
    }

    return Directory.GetCurrentDirectory();
}

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/Analysis/ClassDiagramService.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/Analysis/ClassDiagramService.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/Analysis/DiagramModel.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/Analysis/DiagramModel.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
namespace ClassDiagramMaker.Analysis;

public enum DiagramTypeKind
{
    Class,
    Interface,
    Struct,
    Record,
    Enum
}

public enum DiagramMemberKind
{
    Field,
    Property,
    Method,
    Constructor,
    Event,
    Indexer,
    EnumValue
}

public enum DiagramRelationshipKind
{
    Inheritance,
    Realization,
    Association,
    Dependency
}

public sealed record DiagramType
{
    public required string Id { get; init; }
    public required string SimpleName { get; init; }
    public required string DisplayName { get; init; }
    public required string FullName { get; init; }
    public required string Namespace { get; init; }
    public required string SourceFile { get; init; }
    public required DiagramTypeKind Kind { get; init; }
    public required string Accessibility { get; init; }
    public IReadOnlyList<string> Modifiers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TypeParameters { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BaseTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<DiagramMember> Members { get; init; } = Array.Empty<DiagramMember>();
}

public sealed record DiagramMember
{
    public required DiagramMemberKind Kind { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Visibility { get; init; }
    public required string Signature { get; init; }
    public bool IsStatic { get; init; }
    public IReadOnlyList<string> ReferencedTypes { get; init; } = Array.Empty<string>();
}

public sealed record DiagramRelationship
{
    public required DiagramRelationshipKind Kind { get; init; }
    public required string FromTypeId { get; init; }
    public required string ToTypeId { get; init; }
    public string? Label { get; init; }
}

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/Analysis/GenerationContracts.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/Analysis/GenerationContracts.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
namespace ClassDiagramMaker.Analysis;

public sealed record GenerationRequest(
    string ProjectFolder,
    string SearchFolder,
    string? SearchFile,
    string OutputPath);

public sealed record GenerationProgress(
    string Stage,
    string Message,
    int Percent,
    int ProcessedFiles,
    int TotalFiles);

public sealed record GenerationResult(
    string OutputPath,
    string Mermaid,
    int TypeCount,
    int RelationshipCount);

public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Failed
}

public sealed record DiagramJobSnapshot
{
    public required Guid Id { get; init; }
    public required JobStatus Status { get; init; }
    public required GenerationRequest Request { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public string Stage { get; init; } = "Queued";
    public string Message { get; init; } = "Waiting to start...";
    public int Percent { get; init; }
    public int ProcessedFiles { get; init; }
    public int TotalFiles { get; init; }
    public string? OutputPath { get; init; }
    public string? Mermaid { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();
}

public sealed class DiagramJobStore
{
    private readonly Dictionary<Guid, DiagramJobSnapshot> _jobs = new();
    private readonly object _gate = new();

    public DiagramJobSnapshot Create(GenerationRequest request)
    {
        var snapshot = new DiagramJobSnapshot
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Queued,
            Request = request,
            CreatedAt = DateTimeOffset.UtcNow
        };

        lock (_gate)
        {
            _jobs[snapshot.Id] = snapshot;
        }

        return snapshot;
    }

    public DiagramJobSnapshot? Get(Guid id)
    {
        lock (_gate)
        {
            return _jobs.TryGetValue(id, out var snapshot) ? snapshot : null;
        }
    }

    public void Update(Guid id, Func<DiagramJobSnapshot, DiagramJobSnapshot> update)
    {
        lock (_gate)
        {
            if (_jobs.TryGetValue(id, out var snapshot))
            {
                _jobs[id] = update(snapshot);
            }
        }
    }
}

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/Analysis/MermaidRenderer.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/Analysis/MermaidRenderer.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/Analysis/RelationshipBuilder.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/Analysis/RelationshipBuilder.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
namespace ClassDiagramMaker.Analysis;

internal static class RelationshipBuilder
{
    public static IReadOnlyList<DiagramRelationship> Build(IReadOnlyList<DiagramType> types)
    {
        var index = TypeIndex.Create(types);
        var relationships = new List<DiagramRelationship>();

        foreach (var type in types)
        {
            foreach (var baseTypeName in type.BaseTypes)
            {
                var target = index.Resolve(baseTypeName, type);
                if (target is null || target.Id == type.Id)
                {
                    continue;
                }

                relationships.Add(new DiagramRelationship
                {
                    Kind = target.Kind == DiagramTypeKind.Interface && type.Kind != DiagramTypeKind.Interface
                        ? DiagramRelationshipKind.Realization
                        : DiagramRelationshipKind.Inheritance,
                    FromTypeId = type.Id,
                    ToTypeId = target.Id
                });
            }

            foreach (var member in type.Members)
            {
                foreach (var referencedTypeName in member.ReferencedTypes)
                {
                    var target = index.Resolve(referencedTypeName, type);
                    if (target is null || target.Id == type.Id)
                    {
                        continue;
                    }

                    relationships.Add(new DiagramRelationship
                    {
                        Kind = member.Kind is DiagramMemberKind.Field or DiagramMemberKind.Property or DiagramMemberKind.Event
                            ? DiagramRelationshipKind.Association
                            : DiagramRelationshipKind.Dependency,
                        FromTypeId = type.Id,
                        ToTypeId = target.Id,
                        Label = member.Name
                    });
                }
            }
        }

        return relationships
            .DistinctBy(relationship => $"{relationship.Kind}:{relationship.FromTypeId}:{relationship.ToTypeId}:{relationship.Label}")
            .OrderBy(relationship => relationship.Kind)
            .ThenBy(relationship => relationship.FromTypeId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.ToTypeId, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed class TypeIndex
    {
        private readonly Dictionary<string, DiagramType> _byFullName;
        private readonly ILookup<string, DiagramType> _bySimpleName;

        private TypeIndex(IReadOnlyList<DiagramType> types)
        {
            _byFullName = types
                .GroupBy(type => NormalizeTypeName(type.FullName), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            _bySimpleName = types.ToLookup(type => type.SimpleName, StringComparer.Ordinal);
        }

        public static TypeIndex Create(IReadOnlyList<DiagramType> types)
        {
            return new TypeIndex(types);
        }

        public DiagramType? Resolve(string typeName, DiagramType context)
        {
            var normalized = NormalizeTypeName(typeName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (_byFullName.TryGetValue(normalized, out var exact))
            {
                return exact;
            }

            if (normalized.Contains('.', StringComparison.Ordinal))
            {
                var suffixMatch = _byFullName.Values
                    .Where(type => NormalizeTypeName(type.FullName).EndsWith($".{normalized}", StringComparison.Ordinal))
                    .ToArray();
                if (suffixMatch.Length == 1)
                {
                    return suffixMatch[0];
                }
            }

            var simpleName = normalized.Split('.').Last();
            var matches = _bySimpleName[simpleName].ToArray();
            if (matches.Length == 0)
            {
                return null;
            }

            var sameNamespace = matches
                .Where(type => string.Equals(type.Namespace, context.Namespace, StringComparison.Ordinal))
                .ToArray();
            if (sameNamespace.Length == 1)
            {
                return sameNamespace[0];
            }

            return matches.Length == 1 ? matches[0] : null;
        }

        private static string NormalizeTypeName(string typeName)
        {
            var value = typeName
                .Replace("global::", string.Empty, StringComparison.Ordinal)
                .Replace("?", string.Empty, StringComparison.Ordinal)
                .Trim();

            var genericStart = value.IndexOf('<', StringComparison.Ordinal);
            if (genericStart >= 0)
            {
                value = value[..genericStart];
            }

            return value;
        }
    }
}

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/Analysis/SyntaxTypeCollector.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/Analysis/SyntaxTypeCollector.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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
                    members.AddRange(CreateFieldMembers(field));
                    break;
                case PropertyDeclarationSyntax property:
                    members.Add(CreatePropertyMember(property));
                    break;
                case MethodDeclarationSyntax method:
                    members.Add(CreateMethodMember(method));
                    break;
                case ConstructorDeclarationSyntax constructor:
                    members.Add(CreateConstructorMember(constructor));
                    break;
                case EventDeclarationSyntax eventDeclaration:
                    members.Add(CreateEventMember(eventDeclaration));
                    break;
                case EventFieldDeclarationSyntax eventField:
                    members.AddRange(CreateEventFieldMembers(eventField));
                    break;
                case IndexerDeclarationSyntax indexer:
                    members.Add(CreateIndexerMember(indexer));
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

    private static IEnumerable<DiagramMember> CreateFieldMembers(FieldDeclarationSyntax field)
    {
        var type = field.Declaration.Type.ToString();
        foreach (var variable in field.Declaration.Variables)
        {
            yield return new DiagramMember
            {
                Kind = DiagramMemberKind.Field,
                Name = variable.Identifier.ValueText,
                Type = type,
                Visibility = GetVisibilitySymbol(field.Modifiers),
                Signature = $"{GetVisibilitySymbol(field.Modifiers)}{variable.Identifier.ValueText}: {type}",
                IsStatic = HasModifier(field.Modifiers, SyntaxKind.StaticKeyword),
                ReferencedTypes = TypeReferenceCollector.Collect(field.Declaration.Type)
            };
        }
    }

    private static DiagramMember CreatePropertyMember(PropertyDeclarationSyntax property)
    {
        var type = property.Type.ToString();
        return new DiagramMember
        {
            Kind = DiagramMemberKind.Property,
            Name = property.Identifier.ValueText,
            Type = type,
            Visibility = GetVisibilitySymbol(property.Modifiers),
            Signature = $"{GetVisibilitySymbol(property.Modifiers)}{property.Identifier.ValueText}: {type}",
            IsStatic = HasModifier(property.Modifiers, SyntaxKind.StaticKeyword),
            ReferencedTypes = TypeReferenceCollector.Collect(property.Type)
        };
    }

    private static DiagramMember CreateMethodMember(MethodDeclarationSyntax method)
    {
        var returnType = method.ReturnType.ToString();
        var parameters = FormatParameters(method.ParameterList.Parameters);
        var references = TypeReferenceCollector.Collect(method.ReturnType)
            .Concat(method.ParameterList.Parameters.SelectMany(parameter => TypeReferenceCollector.Collect(parameter.Type)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new DiagramMember
        {
            Kind = DiagramMemberKind.Method,
            Name = method.Identifier.ValueText,
            Type = returnType,
            Visibility = GetVisibilitySymbol(method.Modifiers),
            Signature = $"{GetVisibilitySymbol(method.Modifiers)}{method.Identifier.ValueText}({parameters}): {returnType}",
            IsStatic = HasModifier(method.Modifiers, SyntaxKind.StaticKeyword),
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
            Signature = $"{GetVisibilitySymbol(constructor.Modifiers)}{constructor.Identifier.ValueText}({parameters})",
            IsStatic = HasModifier(constructor.Modifiers, SyntaxKind.StaticKeyword),
            ReferencedTypes = references
        };
    }

    private static DiagramMember CreateEventMember(EventDeclarationSyntax eventDeclaration)
    {
        var type = eventDeclaration.Type.ToString();
        return new DiagramMember
        {
            Kind = DiagramMemberKind.Event,
            Name = eventDeclaration.Identifier.ValueText,
            Type = type,
            Visibility = GetVisibilitySymbol(eventDeclaration.Modifiers),
            Signature = $"{GetVisibilitySymbol(eventDeclaration.Modifiers)}{eventDeclaration.Identifier.ValueText}: {type}",
            IsStatic = HasModifier(eventDeclaration.Modifiers, SyntaxKind.StaticKeyword),
            ReferencedTypes = TypeReferenceCollector.Collect(eventDeclaration.Type)
        };
    }

    private static IEnumerable<DiagramMember> CreateEventFieldMembers(EventFieldDeclarationSyntax eventField)
    {
        var type = eventField.Declaration.Type.ToString();
        foreach (var variable in eventField.Declaration.Variables)
        {
            yield return new DiagramMember
            {
                Kind = DiagramMemberKind.Event,
                Name = variable.Identifier.ValueText,
                Type = type,
                Visibility = GetVisibilitySymbol(eventField.Modifiers),
                Signature = $"{GetVisibilitySymbol(eventField.Modifiers)}{variable.Identifier.ValueText}: {type}",
                IsStatic = HasModifier(eventField.Modifiers, SyntaxKind.StaticKeyword),
                ReferencedTypes = TypeReferenceCollector.Collect(eventField.Declaration.Type)
            };
        }
    }

    private static DiagramMember CreateIndexerMember(IndexerDeclarationSyntax indexer)
    {
        var type = indexer.Type.ToString();
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
            Visibility = GetVisibilitySymbol(indexer.Modifiers),
            Signature = $"{GetVisibilitySymbol(indexer.Modifiers)}this[{parameters}]: {type}",
            IsStatic = HasModifier(indexer.Modifiers, SyntaxKind.StaticKeyword),
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

    private static string GetAccessibility(SyntaxTokenList modifiers, bool isTypeDeclaration)
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

        return isTypeDeclaration ? "internal" : "private";
    }

    private static string GetVisibilitySymbol(SyntaxTokenList modifiers)
    {
        return GetAccessibility(modifiers, isTypeDeclaration: false) switch
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
}

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/Analysis/TypeReferenceCollector.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/Analysis/TypeReferenceCollector.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/wwwroot/app.js")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/wwwroot/app.js" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
const form = document.querySelector("#generateForm");
const generateButton = document.querySelector("#generateButton");
const resetButton = document.querySelector("#resetButton");
const statusPill = document.querySelector("#statusPill");
const stageLabel = document.querySelector("#stageLabel");
const messageLabel = document.querySelector("#messageLabel");
const percentLabel = document.querySelector("#percentLabel");
const progressBar = document.querySelector("#progressBar");
const fileMetric = document.querySelector("#fileMetric");
const outputMetric = document.querySelector("#outputMetric");
const logOutput = document.querySelector("#logOutput");
const mermaidOutput = document.querySelector("#mermaidOutput");

let pollHandle = null;

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  stopPolling();

  const data = new FormData(form);
  const payload = {
    projectFolder: data.get("projectFolder")?.trim() ?? "",
    searchFolder: data.get("searchFolder")?.trim() ?? "",
    searchFile: data.get("searchFile")?.trim() || null,
    outputPath: data.get("outputPath")?.trim() ?? ""
  };

  setBusy(true);
  setSnapshot({
    status: "Queued",
    stage: "Queued",
    message: "ジョブを作成しています...",
    percent: 0,
    processedFiles: 0,
    totalFiles: 0,
    log: []
  });
  mermaidOutput.value = "";

  try {
    const response = await fetch("/api/generate", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      throw new Error(await response.text());
    }

    const result = await response.json();
    pollJob(result.id);
  } catch (error) {
    setBusy(false);
    setFailed(error.message);
  }
});

resetButton.addEventListener("click", () => {
  stopPolling();
  form.reset();
  setBusy(false);
  setSnapshot({
    status: "Queued",
    stage: "待機中",
    message: "入力して生成を開始してください。",
    percent: 0,
    processedFiles: 0,
    totalFiles: 0,
    log: []
  });
  mermaidOutput.value = "";
});

async function pollJob(id) {
  const update = async () => {
    try {
      const response = await fetch(`/api/jobs/${id}`);
      if (!response.ok) {
        throw new Error(await response.text());
      }

      const snapshot = await response.json();
      setSnapshot(snapshot);

      if (snapshot.status === "Completed") {
        stopPolling();
        setBusy(false);
        mermaidOutput.value = snapshot.mermaid ?? "";
      } else if (snapshot.status === "Failed") {
        stopPolling();
        setBusy(false);
      }
    } catch (error) {
      stopPolling();
      setBusy(false);
      setFailed(error.message);
    }
  };

  await update();
  pollHandle = window.setInterval(update, 600);
}

function stopPolling() {
  if (pollHandle !== null) {
    window.clearInterval(pollHandle);
    pollHandle = null;
  }
}

function setBusy(isBusy) {
  generateButton.disabled = isBusy;
  generateButton.textContent = isBusy ? "処理中" : "生成";
}

function setFailed(message) {
  setSnapshot({
    status: "Failed",
    stage: "Failed",
    message,
    percent: 0,
    processedFiles: 0,
    totalFiles: 0,
    log: [message]
  });
}

function setSnapshot(snapshot) {
  const percent = clamp(Number(snapshot.percent ?? 0), 0, 100);
  const status = snapshot.status ?? "Queued";

  stageLabel.textContent = snapshot.stage ?? status;
  messageLabel.textContent = snapshot.message ?? "";
  percentLabel.textContent = `${percent}%`;
  progressBar.style.width = `${percent}%`;
  fileMetric.textContent = `${snapshot.processedFiles ?? 0} / ${snapshot.totalFiles ?? 0} files`;
  outputMetric.textContent = snapshot.outputPath ? `出力: ${snapshot.outputPath}` : "出力なし";
  logOutput.textContent = Array.isArray(snapshot.log) ? snapshot.log.join("\n") : "";

  statusPill.className = "status-pill";
  if (status === "Running") {
    statusPill.classList.add("running");
    statusPill.textContent = "処理中";
  } else if (status === "Completed") {
    statusPill.classList.add("running");
    statusPill.textContent = "完了";
  } else if (status === "Failed") {
    statusPill.classList.add("failed");
    statusPill.textContent = "失敗";
  } else {
    statusPill.textContent = "待機中";
  }
}

function clamp(value, min, max) {
  return Math.min(Math.max(value, min), max);
}

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/wwwroot/index.html")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/wwwroot/index.html" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
<!doctype html>
<html lang="ja">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>ClassDiagramMaker</title>
  <link rel="stylesheet" href="/styles.css">
</head>
<body>
  <main class="app-shell">
    <header class="top-bar">
      <div>
        <h1>ClassDiagramMaker</h1>
        <p>C# AST 解析から Mermaid クラス図を生成します。</p>
      </div>
      <div class="status-pill" id="statusPill">待機中</div>
    </header>

    <section class="workspace">
      <form id="generateForm" class="control-panel">
        <div class="field">
          <label for="projectFolder">対象プロジェクトフォルダ</label>
          <input id="projectFolder" name="projectFolder" type="text" autocomplete="off" placeholder="/path/to/project" required>
        </div>

        <div class="field">
          <label for="searchFolder">検索対象フォルダ</label>
          <input id="searchFolder" name="searchFolder" type="text" autocomplete="off" placeholder="/path/to/project/src">
        </div>

        <div class="field">
          <label for="searchFile">検索対象ファイル</label>
          <input id="searchFile" name="searchFile" type="text" autocomplete="off" placeholder="/path/to/project/src/Foo.cs">
          <span class="hint">空の場合は検索対象フォルダ配下の .cs ファイルを再帰的に解析します。</span>
        </div>

        <div class="field">
          <label for="outputPath">出力先</label>
          <input id="outputPath" name="outputPath" type="text" autocomplete="off" placeholder="/path/to/output/class-diagram.mmd" required>
        </div>

        <div class="actions">
          <button id="generateButton" type="submit">生成</button>
          <button id="resetButton" type="button" class="secondary">クリア</button>
        </div>
      </form>

      <section class="progress-panel" aria-live="polite">
        <div class="progress-head">
          <div>
            <h2 id="stageLabel">待機中</h2>
            <p id="messageLabel">入力して生成を開始してください。</p>
          </div>
          <strong id="percentLabel">0%</strong>
        </div>
        <div class="progress-track">
          <div id="progressBar" class="progress-bar"></div>
        </div>
        <div class="metrics">
          <span id="fileMetric">0 / 0 files</span>
          <span id="outputMetric">出力なし</span>
        </div>
        <pre id="logOutput" class="log-output"></pre>
        <textarea id="mermaidOutput" class="mermaid-output" spellcheck="false" readonly placeholder="生成された Mermaid がここに表示されます。"></textarea>
      </section>
    </section>
  </main>

  <script src="/app.js"></script>
</body>
</html>

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/wwwroot/styles.css")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/wwwroot/styles.css" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
:root {
  color-scheme: light;
  --bg: #f5f7f9;
  --surface: #ffffff;
  --surface-strong: #eef2f6;
  --text: #16202a;
  --muted: #667381;
  --line: #d8e0e7;
  --accent: #287c6b;
  --accent-strong: #1d6658;
  --danger: #b63d3d;
  --shadow: 0 10px 30px rgba(19, 31, 44, 0.08);
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
  min-height: 100vh;
  background: var(--bg);
  color: var(--text);
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
}

button,
input,
textarea {
  font: inherit;
}

.app-shell {
  width: min(1180px, calc(100vw - 32px));
  margin: 0 auto;
  padding: 28px 0;
}

.top-bar {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  margin-bottom: 20px;
}

.top-bar h1 {
  margin: 0;
  font-size: 28px;
  line-height: 1.15;
}

.top-bar p {
  margin: 6px 0 0;
  color: var(--muted);
}

.status-pill {
  min-width: 96px;
  padding: 8px 12px;
  border: 1px solid var(--line);
  border-radius: 6px;
  background: var(--surface);
  color: var(--muted);
  text-align: center;
  font-size: 14px;
}

.status-pill.running {
  border-color: rgba(40, 124, 107, 0.45);
  color: var(--accent-strong);
}

.status-pill.failed {
  border-color: rgba(182, 61, 61, 0.45);
  color: var(--danger);
}

.workspace {
  display: grid;
  grid-template-columns: minmax(320px, 420px) minmax(0, 1fr);
  gap: 20px;
  align-items: start;
}

.control-panel,
.progress-panel {
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--surface);
  box-shadow: var(--shadow);
}

.control-panel {
  display: grid;
  gap: 18px;
  padding: 20px;
}

.field {
  display: grid;
  gap: 7px;
}

label {
  font-weight: 700;
  font-size: 14px;
}

input {
  width: 100%;
  min-height: 40px;
  border: 1px solid var(--line);
  border-radius: 6px;
  padding: 8px 10px;
  background: #ffffff;
  color: var(--text);
}

input:focus,
textarea:focus {
  outline: 2px solid rgba(40, 124, 107, 0.22);
  border-color: var(--accent);
}

.hint {
  color: var(--muted);
  font-size: 12px;
  line-height: 1.45;
}

.actions {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

button {
  min-height: 40px;
  border: 1px solid var(--accent);
  border-radius: 6px;
  padding: 8px 16px;
  background: var(--accent);
  color: #ffffff;
  cursor: pointer;
  font-weight: 700;
}

button:hover {
  background: var(--accent-strong);
}

button:disabled {
  cursor: not-allowed;
  opacity: 0.62;
}

button.secondary {
  border-color: var(--line);
  background: var(--surface-strong);
  color: var(--text);
}

.progress-panel {
  padding: 20px;
}

.progress-head {
  display: flex;
  justify-content: space-between;
  gap: 18px;
  align-items: flex-start;
}

.progress-head h2 {
  margin: 0;
  font-size: 18px;
}

.progress-head p {
  margin: 6px 0 0;
  color: var(--muted);
  line-height: 1.45;
}

#percentLabel {
  font-size: 24px;
  line-height: 1;
  white-space: nowrap;
}

.progress-track {
  height: 12px;
  margin-top: 18px;
  overflow: hidden;
  border-radius: 999px;
  background: var(--surface-strong);
}

.progress-bar {
  width: 0%;
  height: 100%;
  border-radius: inherit;
  background: var(--accent);
  transition: width 180ms ease;
}

.metrics {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  margin: 12px 0 16px;
  color: var(--muted);
  font-size: 13px;
}

.log-output,
.mermaid-output {
  width: 100%;
  border: 1px solid var(--line);
  border-radius: 6px;
  background: #fbfcfd;
  color: var(--text);
}

.log-output {
  min-height: 130px;
  max-height: 220px;
  margin: 0 0 14px;
  padding: 12px;
  overflow: auto;
  white-space: pre-wrap;
}

.mermaid-output {
  min-height: 300px;
  resize: vertical;
  padding: 12px;
  font-family: "SFMono-Regular", Consolas, "Liberation Mono", monospace;
  font-size: 13px;
  line-height: 1.5;
}

@media (max-width: 860px) {
  .app-shell {
    width: min(100vw - 20px, 680px);
    padding: 18px 0;
  }

  .top-bar,
  .workspace {
    display: grid;
    grid-template-columns: 1fr;
  }

  .status-pill {
    width: fit-content;
  }

  .metrics {
    display: grid;
  }
}

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/tools/generate-bootstrap.sh")"
cat > "$TARGET_DIR/tools/generate-bootstrap.sh" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_FILE="$ROOT_DIR/bootstrap/ClassDiagramMaker.bootstrap.sh"
MARKER="__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__"

FILES=(
  ".gitignore"
  "global.json"
  "ClassDiagramMaker.sln"
  "README.md"
  "src/ClassDiagramMaker/ClassDiagramMaker.csproj"
  "src/ClassDiagramMaker/Program.cs"
  "src/ClassDiagramMaker/Analysis/ClassDiagramService.cs"
  "src/ClassDiagramMaker/Analysis/DiagramModel.cs"
  "src/ClassDiagramMaker/Analysis/GenerationContracts.cs"
  "src/ClassDiagramMaker/Analysis/MermaidRenderer.cs"
  "src/ClassDiagramMaker/Analysis/RelationshipBuilder.cs"
  "src/ClassDiagramMaker/Analysis/SyntaxTypeCollector.cs"
  "src/ClassDiagramMaker/Analysis/TypeReferenceCollector.cs"
  "src/ClassDiagramMaker/wwwroot/app.js"
  "src/ClassDiagramMaker/wwwroot/index.html"
  "src/ClassDiagramMaker/wwwroot/styles.css"
  "tools/generate-bootstrap.sh"
)

{
  printf '%s\n' '#!/usr/bin/env bash'
  printf '%s\n' 'set -euo pipefail'
  printf '\n'
  printf '%s\n' 'TARGET_DIR="${1:-ClassDiagramMaker}"'
  printf '%s\n' 'mkdir -p "$TARGET_DIR"'
  printf '\n'
  for file in "${FILES[@]}"; do
    printf "mkdir -p \"\$(dirname \"\$TARGET_DIR/%s\")\"\n" "$file"
    printf "cat > \"\$TARGET_DIR/%s\" <<'%s'\n" "$file" "$MARKER"
    cat "$ROOT_DIR/$file"
    printf '\n%s\n\n' "$MARKER"
  done

  printf '%s\n' 'chmod +x "$TARGET_DIR/tools/generate-bootstrap.sh"'
  printf '%s\n' 'echo "Created ClassDiagramMaker source at $TARGET_DIR"'
  printf '%s\n' 'echo "Run: cd $TARGET_DIR && dotnet run --project src/ClassDiagramMaker/ClassDiagramMaker.csproj"'
} > "$OUTPUT_FILE"

chmod +x "$OUTPUT_FILE"
printf 'Generated %s\n' "$OUTPUT_FILE"

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

chmod +x "$TARGET_DIR/tools/generate-bootstrap.sh"
echo "Created ClassDiagramMaker source at $TARGET_DIR"
echo "Run: cd $TARGET_DIR && dotnet run --project src/ClassDiagramMaker/ClassDiagramMaker.csproj"
