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

mkdir -p "$(dirname "$TARGET_DIR/README.md")"
cat > "$TARGET_DIR/README.md" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
# ClassDiagramMaker

C# source analyzer for generating Mermaid class diagrams from selected files and directories.

The GUI is a Windows-first WinForms application.

## Requirements

- .NET SDK 9.0
- Windows for running the WinForms GUI

The repository includes `global.json` to use the .NET 9 SDK even when newer SDKs are installed.

## Run

```bash
dotnet restore src/ClassDiagramMaker/ClassDiagramMaker.csproj
dotnet run --project src/ClassDiagramMaker/ClassDiagramMaker.csproj
```

Fill in the WinForms screen:

- Target project folder
- Search folder
- Search file, optional
- Output path for the generated `.mmd` file

When the search file is empty, the tool recursively analyzes `.cs` files under the search folder. The GUI shows parsing and rendering progress while the Mermaid file is generated.

## Tests

Core analysis behavior is covered with xUnit.

```bash
dotnet test ClassDiagramMaker.sln
```

## Output

The first supported output format is Mermaid `classDiagram`.

```mermaid
classDiagram
    direction LR
    class Repository_T {
        <<abstract>>
        where T : class, new()
        #{static readonly} CacheKey: string
        +{abstract} Create~TArg~(arg: TArg): T where TArg : struct
    }
    UserRepository <|.. UserService
    UserService --> UserRepository : repository
```

## Bootstrap

For users who cannot download the repository, this project provides a generated single-file bootstrap script:

```bash
./bootstrap/ClassDiagramMaker.bootstrap.sh ./ClassDiagramMaker
```

The script recreates the app and core source tree locally. It intentionally does not include xUnit test code. Regenerate it after source changes with:

```bash
./tools/generate-bootstrap.sh
```

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker.Core/ClassDiagramMaker.Core.csproj")"
cat > "$TARGET_DIR/src/ClassDiagramMaker.Core/ClassDiagramMaker.Core.csproj" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>ClassDiagramMaker</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
  </ItemGroup>
</Project>

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/ClassDiagramService.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/ClassDiagramService.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/DiagramModel.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/DiagramModel.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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
    public IReadOnlyList<string> TypeParameterConstraints { get; init; } = Array.Empty<string>();
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
    public IReadOnlyList<string> Modifiers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TypeParameterConstraints { get; init; } = Array.Empty<string>();
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

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/GenerationContracts.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/GenerationContracts.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/MermaidRenderer.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/MermaidRenderer.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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

            foreach (var constraint in type.TypeParameterConstraints)
            {
                builder.AppendLine($"        {EscapeMemberText(constraint)}");
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

    private static IReadOnlyList<string> GetStereotypes(DiagramType type)
    {
        var stereotypes = new List<string>();
        var kindStereotype = type.Kind switch
        {
            DiagramTypeKind.Interface => "interface",
            DiagramTypeKind.Struct => "struct",
            DiagramTypeKind.Record => "record",
            DiagramTypeKind.Enum => "enumeration",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(kindStereotype))
        {
            stereotypes.Add(kindStereotype);
        }

        stereotypes.AddRange(type.Modifiers);
        return stereotypes;
    }

    private static string EscapeMemberText(string value)
    {
        return Regex.Replace(value, @"\s+", " ")
            .Replace("<", "~", StringComparison.Ordinal)
            .Replace(">", "~", StringComparison.Ordinal)
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

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/RelationshipBuilder.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/RelationshipBuilder.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/SyntaxTypeCollector.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/SyntaxTypeCollector.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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
                Signature = CreateMemberSignature(field.Modifiers, $"{variable.Identifier.ValueText}: {type}"),
                IsStatic = HasModifier(field.Modifiers, SyntaxKind.StaticKeyword),
                Modifiers = GetNonAccessibilityModifiers(field.Modifiers),
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
            Signature = CreateMemberSignature(property.Modifiers, $"{property.Identifier.ValueText}: {type}"),
            IsStatic = HasModifier(property.Modifiers, SyntaxKind.StaticKeyword),
            Modifiers = GetNonAccessibilityModifiers(property.Modifiers),
            ReferencedTypes = TypeReferenceCollector.Collect(property.Type)
        };
    }

    private static DiagramMember CreateMethodMember(MethodDeclarationSyntax method)
    {
        var returnType = method.ReturnType.ToString();
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
            Visibility = GetVisibilitySymbol(method.Modifiers),
            Signature = CreateMemberSignature(method.Modifiers, coreSignature),
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

    private static DiagramMember CreateEventMember(EventDeclarationSyntax eventDeclaration)
    {
        var type = eventDeclaration.Type.ToString();
        return new DiagramMember
        {
            Kind = DiagramMemberKind.Event,
            Name = eventDeclaration.Identifier.ValueText,
            Type = type,
            Visibility = GetVisibilitySymbol(eventDeclaration.Modifiers),
            Signature = CreateMemberSignature(eventDeclaration.Modifiers, $"{eventDeclaration.Identifier.ValueText}: {type}"),
            IsStatic = HasModifier(eventDeclaration.Modifiers, SyntaxKind.StaticKeyword),
            Modifiers = GetNonAccessibilityModifiers(eventDeclaration.Modifiers),
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
                Signature = CreateMemberSignature(eventField.Modifiers, $"{variable.Identifier.ValueText}: {type}"),
                IsStatic = HasModifier(eventField.Modifiers, SyntaxKind.StaticKeyword),
                Modifiers = GetNonAccessibilityModifiers(eventField.Modifiers),
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
            Signature = CreateMemberSignature(indexer.Modifiers, $"this[{parameters}]: {type}"),
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

    private static string CreateMemberSignature(SyntaxTokenList modifiers, string signature)
    {
        var visibility = GetVisibilitySymbol(modifiers);
        var nonAccessibilityModifiers = GetNonAccessibilityModifiers(modifiers);
        var modifierText = nonAccessibilityModifiers.Count == 0
            ? string.Empty
            : $"{{{string.Join(" ", nonAccessibilityModifiers)}}} ";

        return $"{visibility}{modifierText}{signature}";
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

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/TypeReferenceCollector.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/TypeReferenceCollector.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/ClassDiagramMaker.csproj")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/ClassDiagramMaker.csproj" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>ClassDiagramMaker</RootNamespace>
    <AssemblyName>ClassDiagramMaker</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ClassDiagramMaker.Core\ClassDiagramMaker.Core.csproj" />
  </ItemGroup>
</Project>

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/Program.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/Program.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
using ClassDiagramMaker.Analysis;

namespace ClassDiagramMaker;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(new ClassDiagramService()));
    }
}

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/MainForm.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/MainForm.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
using ClassDiagramMaker.Analysis;

namespace ClassDiagramMaker;

public sealed class MainForm : Form
{
    private readonly ClassDiagramService _service;
    private readonly TextBox _projectFolderTextBox = new();
    private readonly TextBox _searchFolderTextBox = new();
    private readonly TextBox _searchFileTextBox = new();
    private readonly TextBox _outputPathTextBox = new();
    private readonly Button _generateButton = new();
    private readonly Button _cancelButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _stageLabel = new();
    private readonly Label _messageLabel = new();
    private readonly Label _fileCountLabel = new();
    private readonly Label _outputLabel = new();
    private readonly TextBox _logTextBox = new();
    private readonly TextBox _mermaidTextBox = new();
    private CancellationTokenSource? _generationCancellation;

    public MainForm(ClassDiagramService service)
    {
        _service = service;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "ClassDiagramMaker";
        MinimumSize = new Size(980, 720);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var inputPanel = BuildInputPanel();
        var progressPanel = BuildProgressPanel();
        var outputSplit = BuildOutputSplit();

        root.Controls.Add(inputPanel, 0, 0);
        root.Controls.Add(progressPanel, 0, 1);
        root.Controls.Add(outputSplit, 0, 2);
        Controls.Add(root);

        _generateButton.Click += GenerateButton_Click;
        _cancelButton.Click += CancelButton_Click;
    }

    private Control BuildInputPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 5,
            Padding = new Padding(0, 0, 0, 10)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));

        AddPathRow(panel, 0, "対象プロジェクト", _projectFolderTextBox, "参照...", BrowseProjectFolder);
        AddPathRow(panel, 1, "検索対象フォルダ", _searchFolderTextBox, "参照...", BrowseSearchFolder);
        AddPathRow(panel, 2, "検索対象ファイル", _searchFileTextBox, "参照...", BrowseSearchFile);
        AddPathRow(panel, 3, "出力先", _outputPathTextBox, "参照...", BrowseOutputPath);

        var helpLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            Text = "検索対象ファイルが空の場合は、検索対象フォルダ配下の .cs ファイルを再帰的に解析します。"
        };
        panel.Controls.Add(helpLabel, 1, 4);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };

        _generateButton.Text = "生成";
        _generateButton.AutoSize = true;
        _generateButton.MinimumSize = new Size(96, 34);

        _cancelButton.Text = "キャンセル";
        _cancelButton.AutoSize = true;
        _cancelButton.MinimumSize = new Size(96, 34);
        _cancelButton.Enabled = false;

        buttonPanel.Controls.Add(_generateButton);
        buttonPanel.Controls.Add(_cancelButton);
        panel.Controls.Add(buttonPanel, 2, 4);

        return panel;
    }

    private static void AddPathRow(
        TableLayoutPanel panel,
        int row,
        string label,
        TextBox textBox,
        string buttonText,
        EventHandler browseHandler)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var labelControl = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = label
        };

        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 4, 8, 4);

        var button = new Button
        {
            Text = buttonText,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4)
        };
        button.Click += browseHandler;

        panel.Controls.Add(labelControl, 0, row);
        panel.Controls.Add(textBox, 1, row);
        panel.Controls.Add(button, 2, row);
    }

    private Control BuildProgressPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(0, 0, 0, 10)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

        _stageLabel.Text = "待機中";
        _stageLabel.Font = new Font(Font, FontStyle.Bold);
        _stageLabel.AutoSize = true;
        _stageLabel.Dock = DockStyle.Fill;

        _fileCountLabel.Text = "0 / 0 files";
        _fileCountLabel.AutoSize = true;
        _fileCountLabel.Dock = DockStyle.Fill;
        _fileCountLabel.TextAlign = ContentAlignment.MiddleRight;

        _messageLabel.Text = "入力して生成を開始してください。";
        _messageLabel.AutoSize = true;
        _messageLabel.Dock = DockStyle.Fill;
        _messageLabel.ForeColor = SystemColors.GrayText;

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Height = 18;
        _progressBar.Style = ProgressBarStyle.Continuous;

        _outputLabel.Text = "出力なし";
        _outputLabel.AutoSize = true;
        _outputLabel.Dock = DockStyle.Fill;
        _outputLabel.ForeColor = SystemColors.GrayText;

        panel.Controls.Add(_stageLabel, 0, 0);
        panel.Controls.Add(_fileCountLabel, 1, 0);
        panel.Controls.Add(_messageLabel, 0, 1);
        panel.SetColumnSpan(_messageLabel, 2);
        panel.Controls.Add(_progressBar, 0, 2);
        panel.SetColumnSpan(_progressBar, 2);
        panel.Controls.Add(_outputLabel, 0, 3);
        panel.SetColumnSpan(_outputLabel, 2);

        return panel;
    }

    private Control BuildOutputSplit()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 190,
            Panel1MinSize = 120,
            Panel2MinSize = 220
        };

        split.Panel1.Controls.Add(BuildTextSection("ログ", _logTextBox, readOnly: true));
        split.Panel2.Controls.Add(BuildTextSection("Mermaid", _mermaidTextBox, readOnly: false));
        return split;
    }

    private static Control BuildTextSection(string title, TextBox textBox, bool readOnly)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = title,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 4)
        };

        textBox.Dock = DockStyle.Fill;
        textBox.Multiline = true;
        textBox.ScrollBars = ScrollBars.Both;
        textBox.WordWrap = false;
        textBox.ReadOnly = readOnly;
        textBox.Font = new Font("Consolas", 10);

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(textBox, 0, 1);
        return panel;
    }

    private void BrowseProjectFolder(object? sender, EventArgs e)
    {
        if (TrySelectFolder("対象プロジェクトフォルダを選択", _projectFolderTextBox.Text, out var folder))
        {
            _projectFolderTextBox.Text = folder;
            if (string.IsNullOrWhiteSpace(_searchFolderTextBox.Text))
            {
                _searchFolderTextBox.Text = folder;
            }
        }
    }

    private void BrowseSearchFolder(object? sender, EventArgs e)
    {
        if (TrySelectFolder("検索対象フォルダを選択", _searchFolderTextBox.Text, out var folder))
        {
            _searchFolderTextBox.Text = folder;
        }
    }

    private void BrowseSearchFile(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "検索対象ファイルを選択",
            Filter = "C# files (*.cs)|*.cs|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(_searchFolderTextBox.Text) && Directory.Exists(_searchFolderTextBox.Text))
        {
            dialog.InitialDirectory = _searchFolderTextBox.Text;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _searchFileTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseOutputPath(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "出力先を選択",
            Filter = "Mermaid files (*.mmd)|*.mmd|Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt = "mmd",
            OverwritePrompt = true
        };

        if (!string.IsNullOrWhiteSpace(_outputPathTextBox.Text))
        {
            var directory = Path.GetDirectoryName(_outputPathTextBox.Text);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
            dialog.FileName = Path.GetFileName(_outputPathTextBox.Text);
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _outputPathTextBox.Text = dialog.FileName;
        }
    }

    private static bool TrySelectFolder(string description, string currentValue, out string folder)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrWhiteSpace(currentValue) && Directory.Exists(currentValue))
        {
            dialog.SelectedPath = currentValue;
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            folder = dialog.SelectedPath;
            return true;
        }

        folder = string.Empty;
        return false;
    }

    private async void GenerateButton_Click(object? sender, EventArgs e)
    {
        if (!TryCreateRequest(out var request))
        {
            return;
        }

        _generationCancellation = new CancellationTokenSource();
        SetRunning(true);
        ResetProgress();
        AppendLog("Generating Mermaid class diagram...");

        try
        {
            var progress = new Progress<GenerationProgress>(ReportProgress);
            var result = await Task.Run(
                () => _service.GenerateAsync(request, progress, _generationCancellation.Token));

            _mermaidTextBox.Text = result.Mermaid;
            _outputLabel.Text = $"出力: {result.OutputPath}";
            _stageLabel.Text = "完了";
            _messageLabel.Text = $"生成完了: {result.TypeCount} types, {result.RelationshipCount} relationships";
            AppendLog($"Wrote {result.OutputPath}");
        }
        catch (OperationCanceledException)
        {
            _stageLabel.Text = "キャンセル";
            _messageLabel.Text = "処理をキャンセルしました。";
            AppendLog("Canceled.");
        }
        catch (Exception ex)
        {
            _stageLabel.Text = "失敗";
            _messageLabel.Text = ex.Message;
            AppendLog(ex.ToString());
            MessageBox.Show(this, ex.Message, "生成に失敗しました", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _generationCancellation.Dispose();
            _generationCancellation = null;
            SetRunning(false);
        }
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        _generationCancellation?.Cancel();
    }

    private bool TryCreateRequest(out GenerationRequest request)
    {
        var projectFolder = _projectFolderTextBox.Text.Trim();
        var searchFolder = _searchFolderTextBox.Text.Trim();
        var searchFile = _searchFileTextBox.Text.Trim();
        var outputPath = _outputPathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            ShowValidationError("対象プロジェクトフォルダを入力してください。");
            request = default!;
            return false;
        }

        if (string.IsNullOrWhiteSpace(searchFile) && string.IsNullOrWhiteSpace(searchFolder))
        {
            ShowValidationError("検索対象ファイルが空の場合は、検索対象フォルダを入力してください。");
            request = default!;
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            ShowValidationError("出力先を入力してください。");
            request = default!;
            return false;
        }

        request = new GenerationRequest(
            projectFolder,
            searchFolder,
            string.IsNullOrWhiteSpace(searchFile) ? null : searchFile,
            outputPath);
        return true;
    }

    private void ShowValidationError(string message)
    {
        MessageBox.Show(this, message, "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ReportProgress(GenerationProgress progress)
    {
        _stageLabel.Text = progress.Stage;
        _messageLabel.Text = progress.Message;
        _fileCountLabel.Text = $"{progress.ProcessedFiles} / {progress.TotalFiles} files";
        _progressBar.Value = Math.Clamp(progress.Percent, _progressBar.Minimum, _progressBar.Maximum);
        AppendLog(progress.Message);
    }

    private void ResetProgress()
    {
        _progressBar.Value = 0;
        _stageLabel.Text = "処理中";
        _messageLabel.Text = "開始しています...";
        _fileCountLabel.Text = "0 / 0 files";
        _outputLabel.Text = "出力なし";
        _logTextBox.Clear();
        _mermaidTextBox.Clear();
    }

    private void SetRunning(bool running)
    {
        _generateButton.Enabled = !running;
        _cancelButton.Enabled = running;
        Cursor = running ? Cursors.WaitCursor : Cursors.Default;
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
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
  "README.md"
  "src/ClassDiagramMaker.Core/ClassDiagramMaker.Core.csproj"
  "src/ClassDiagramMaker.Core/Analysis/ClassDiagramService.cs"
  "src/ClassDiagramMaker.Core/Analysis/DiagramModel.cs"
  "src/ClassDiagramMaker.Core/Analysis/GenerationContracts.cs"
  "src/ClassDiagramMaker.Core/Analysis/MermaidRenderer.cs"
  "src/ClassDiagramMaker.Core/Analysis/RelationshipBuilder.cs"
  "src/ClassDiagramMaker.Core/Analysis/SyntaxTypeCollector.cs"
  "src/ClassDiagramMaker.Core/Analysis/TypeReferenceCollector.cs"
  "src/ClassDiagramMaker/ClassDiagramMaker.csproj"
  "src/ClassDiagramMaker/Program.cs"
  "src/ClassDiagramMaker/MainForm.cs"
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
  printf '%s\n' 'echo "Run on Windows: cd $TARGET_DIR && dotnet run --project src/ClassDiagramMaker/ClassDiagramMaker.csproj"'
} > "$OUTPUT_FILE"

chmod +x "$OUTPUT_FILE"
printf 'Generated %s\n' "$OUTPUT_FILE"

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

chmod +x "$TARGET_DIR/tools/generate-bootstrap.sh"
echo "Created ClassDiagramMaker source at $TARGET_DIR"
echo "Run on Windows: cd $TARGET_DIR && dotnet run --project src/ClassDiagramMaker/ClassDiagramMaker.csproj"
