#!/bin/sh
set -eu

TARGET_DIR="${1:-ClassDiagramMaker}"
mkdir -p "$TARGET_DIR"

mkdir -p "$(dirname "$TARGET_DIR/.gitignore")"
cat > "$TARGET_DIR/.gitignore" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
bin/
obj/
artifacts/
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

指定した C# のファイルやディレクトリを解析し、Mermaid のクラス図を生成するツールです。

GUI は Windows 向けの WinForms アプリケーションです。

## 必要環境

- .NET SDK 9.0
- WinForms GUI を実行する場合は Windows

このリポジトリには `global.json` が含まれているため、新しい SDK がインストールされている環境でも .NET 9 SDK を使用します。

## 実行方法

PowerShell では次のように実行します。

```powershell
dotnet restore src/ClassDiagramMaker/ClassDiagramMaker.csproj
dotnet run --project src/ClassDiagramMaker/ClassDiagramMaker.csproj
```

WinForms 画面で次の項目を指定します。

- 対象プロジェクトフォルダ
- 検索対象フォルダ
- 検索対象ファイル、任意
- 生成する `.mmd` ファイルの出力先

検索対象ファイルが空の場合は、検索対象フォルダ配下の `.cs`、`.cshtml.cs`、`.cshtml` ファイルを再帰的に解析します。GUI では解析中とレンダリング中の進捗を確認できます。

## 表示オプション

GUI では巨大なプロジェクトでも見やすくするため、出力内容を調整できます。

- 表示モード: 型だけ、主要メンバー、全メンバー
- 関係線: 継承、interface 実装、フィールド/プロパティ関連、メソッド依存を個別に切り替え
- 分割出力: namespace 単位またはフォルダ単位で Mermaid ファイルを分割し、任意で `index.md` と全体図を生成

## Razor 対応

Razor の `.cshtml` ファイルは Razor ページのノードとして表現されます。解析対象は次の要素です。

- `@model`
- `@inject`
- `@functions` / `@code` ブロックに定義されたメンバー
- マークアップ内の tag helper、view component、partial view 参照

`.cshtml.cs` の code-behind ファイルは通常の C# ソースとして解析します。

単一の Razor ファイルを指定した場合は、対応するペアも一緒に解析します。

- `Page.cshtml` を選択すると、存在する場合は `Page.cshtml.cs` も解析します。
- `Page.cshtml.cs` を選択すると、存在する場合は `Page.cshtml` も解析します。

## テスト

Core の解析処理は xUnit でテストしています。

```bash
dotnet test ClassDiagramMaker.sln
```

## リリース

PowerShell で Windows 用の単一 exe を生成できます。

```powershell
./tools/publish-single-exe.ps1
```

既定の出力先は次の通りです。

```text
artifacts/win-x64-single-file/ClassDiagramMaker.exe
```

起動に失敗した場合は、エラーダイアログを表示し、次のログファイルに詳細を書き込みます。

```text
%LOCALAPPDATA%\ClassDiagramMaker\ClassDiagramMaker.error.log
```

別の Windows runtime 向けに publish する場合は、`-Runtime` を指定します。

```powershell
./tools/publish-single-exe.ps1 -Runtime win-arm64
./tools/publish-single-exe.ps1 -Runtime win-x86
```

同等の `dotnet publish` コマンドは次の通りです。

```bash
dotnet publish src/ClassDiagramMaker/ClassDiagramMaker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false -p:CopyOutputSymbolsToPublishDirectory=false -o artifacts/win-x64-single-file
```

publish profile `win-x64-single-file` も利用できます。

```bash
dotnet publish src/ClassDiagramMaker/ClassDiagramMaker.csproj -p:PublishProfile=win-x64-single-file
```

## 分割出力

分割出力を有効にすると、指定した出力先のファイル名がプレフィックスとして使われます。
たとえば `diagram.mmd` を指定すると、次のようなファイルを生成できます。

```text
diagram.index.md
diagram.all.mmd
diagram.Demo.Services.mmd
diagram.Demo.Models.mmd
```

namespace 分割では C# の namespace ごとに型をまとめます。フォルダ分割では、対象プロジェクトフォルダから見たソースファイルの配置ごとに型をまとめます。

分割された図では、同じ分割ファイル内にある型同士の関係だけを出力します。任意で生成できる `*.all.mmd` には全体図を保持します。

## 解析できる依存関係

C# ソースは Roslyn の AST と SemanticModel を使って解析します。主な取得対象は次の通りです。

- 継承、interface 実装、フィールド、プロパティ、イベント、メソッド、コンストラクタ、インデクサ
- `abstract`、`sealed`、`static`、`readonly` などの修飾子
- generic 型引数、`where T : class` などの generic 制約
- 属性、`typeof(...)`、base 型の generic 引数
- メソッド本体内の `new`、cast、pattern matching、`var` 推論型、static メンバー呼び出し、generic メソッド型引数、呼び出し先の戻り値型
- `using static` と using alias 経由で参照した型
- delegate と class primary constructor

## 出力形式

最初に対応している出力形式は Mermaid の `classDiagram` です。

```mermaid
classDiagram
    direction LR
    class Repository_T {
        <<abstract>>
        #string CacheKey$
        +Create~TArg~(TArg arg) T*
    }
    note for Repository_T "where T : class, new()\\nCacheKey modifiers: static readonly\\nCreate constraints: where TArg : struct"
    class Pages_Users_Index {
        <<razor page>>
        +Demo.Pages.Users.IndexModel Model
        +Demo.Services.IUserRepository Repository
    }
    UserRepository <|.. UserService
    UserService --> UserRepository : repository
```

## Bootstrap

リポジトリをダウンロードできないユーザー向けに、単一ファイルの bootstrap スクリプトを用意しています。

Windows / PowerShell では次のコマンドを使います。

```powershell
powershell -ExecutionPolicy Bypass -File .\bootstrap\ClassDiagramMaker.bootstrap.ps1 .\ClassDiagramMaker
```

PowerShell の実行ポリシーでブロックされない環境では、次のように直接実行できます。

```powershell
.\bootstrap\ClassDiagramMaker.bootstrap.ps1 .\ClassDiagramMaker
```

macOS / Linux など `sh` が使える環境では、次のコマンドでも生成できます。

```bash
sh ./bootstrap/ClassDiagramMaker.bootstrap.sh ./ClassDiagramMaker
```

生成後、Windows では次のように起動または publish できます。

```powershell
cd .\ClassDiagramMaker
dotnet run --project .\src\ClassDiagramMaker\ClassDiagramMaker.csproj
.\tools\publish-single-exe.ps1
```

このスクリプトはアプリ本体と Core のソースツリーをローカルに再作成します。xUnit のテストコードは意図的に含めていません。

ソース変更後に bootstrap を再生成する場合は、次のコマンドを実行します。

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

  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
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
        var csharpDocuments = new List<CSharpDocument>();

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
                var tree = CSharpSyntaxTree.ParseText(
                    text,
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                    path: file,
                    cancellationToken: cancellationToken);
                csharpDocuments.Add(new CSharpDocument(file, tree));
            }

            var percent = 10 + (int)Math.Round(((index + 1) / (double)files.Count) * 55);
            progress.Report(new GenerationProgress(
                "Parsing",
                $"Parsed {Path.GetRelativePath(options.ProjectFolder, file)}",
                percent,
                index + 1,
                files.Count));
        }

        if (csharpDocuments.Count > 0)
        {
            var compilation = CSharpCompilation.Create(
                "ClassDiagramMaker.Analysis.Workspace",
                csharpDocuments.Select(document => document.Tree),
                GetMetadataReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            foreach (var document in csharpDocuments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var root = (CompilationUnitSyntax)await document.Tree.GetRootAsync(cancellationToken);
                var semanticModel = compilation.GetSemanticModel(document.Tree);
                collectedTypes.AddRange(SyntaxTypeCollector.Collect(root, document.File, semanticModel));
            }
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
                    Dependencies = group.SelectMany(type => type.Dependencies)
                        .DistinctBy(dependency => $"{NormalizeDependencyName(dependency.TypeName)}:{dependency.Label}")
                        .ToArray(),
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

    private static IReadOnlyList<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            return trustedPlatformAssemblies
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(File.Exists)
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToArray();
        }

        return new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        };
    }

    private static string NormalizeDependencyName(string typeName)
    {
        var value = typeName
            .Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace("?", string.Empty, StringComparison.Ordinal)
            .Trim();
        var genericStart = value.IndexOf('<', StringComparison.Ordinal);
        return genericStart >= 0 ? value[..genericStart] : value;
    }

    private sealed record NormalizedGenerationRequest(
        string ProjectFolder,
        string SearchFolder,
        string? SearchFile,
        string OutputPath);

    private sealed record CSharpDocument(string File, SyntaxTree Tree);

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
    Enum,
    RazorPage,
    Delegate
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
    public IReadOnlyList<DiagramDependency> Dependencies { get; init; } = Array.Empty<DiagramDependency>();
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

public sealed record DiagramDependency
{
    public required string TypeName { get; init; }
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
    string OutputPath)
{
    public DiagramGenerationOptions Options { get; init; } = DiagramGenerationOptions.Default;
}

public enum DiagramDisplayMode
{
    TypeOnly,
    KeyMembers,
    AllMembers
}

public sealed record DiagramGenerationOptions(
    DiagramDisplayMode DisplayMode = DiagramDisplayMode.AllMembers,
    bool IncludeInheritance = true,
    bool IncludeRealization = true,
    bool IncludeAssociation = true,
    bool IncludeDependency = true)
{
    public static DiagramGenerationOptions Default { get; } = new();

    public DiagramSplitOptions SplitOutput { get; init; } = DiagramSplitOptions.Disabled;
}

public enum DiagramSplitMode
{
    Namespace,
    Folder
}

public sealed record DiagramSplitOptions(
    bool Enabled = false,
    DiagramSplitMode Mode = DiagramSplitMode.Namespace,
    bool IncludeOverview = true,
    bool IncludeIndex = true)
{
    public static DiagramSplitOptions Disabled { get; } = new();
}

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
    int RelationshipCount)
{
    public IReadOnlyList<string> OutputPaths { get; init; } = Array.Empty<string>();
}

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

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/RazorPageCollector.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/RazorPageCollector.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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
        var dependencies = CollectMarkupDependencies(source);

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
                    .ToArray(),
                Dependencies = dependencies
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

    private static IReadOnlyList<DiagramDependency> CollectMarkupDependencies(string source)
    {
        var dependencies = new List<DiagramDependency>();

        dependencies.AddRange(CustomTagPattern()
            .Matches(source)
            .Select(match => CreateDependency(
                EnsureSuffix(ToPascalName(match.Groups["name"].Value), "TagHelper"),
                "tag helper")));

        dependencies.AddRange(ViewComponentStringPattern()
            .Matches(source)
            .Select(match => CreateDependency(
                EnsureSuffix(ToPascalName(match.Groups["name"].Value), "ViewComponent"),
                "view component")));

        dependencies.AddRange(ViewComponentTypePattern()
            .Matches(source)
            .Select(match => CreateDependency(
                EnsureSuffix(ToPascalName(match.Groups["type"].Value), "ViewComponent"),
                "view component")));

        dependencies.AddRange(ViewComponentTagPattern()
            .Matches(source)
            .Select(match => CreateDependency(
                EnsureSuffix(ToPascalName(match.Groups["name"].Value), "ViewComponent"),
                "view component")));

        dependencies.AddRange(PartialCallPattern()
            .Matches(source)
            .Select(match => CreateDependency(
                ToPascalName(match.Groups["name"].Value),
                "partial")));

        dependencies.AddRange(PartialTagPattern()
            .Matches(source)
            .Select(match => CreateDependency(
                ToPascalName(match.Groups["name"].Value),
                "partial")));

        return dependencies
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency.TypeName))
            .DistinctBy(dependency => $"{dependency.TypeName}:{dependency.Label}")
            .ToArray();
    }

    private static DiagramDependency CreateDependency(string typeName, string label)
    {
        return new DiagramDependency
        {
            TypeName = typeName,
            Label = label
        };
    }

    private static string EnsureSuffix(string typeName, string suffix)
    {
        if (string.IsNullOrWhiteSpace(typeName) || typeName.EndsWith(suffix, StringComparison.Ordinal))
        {
            return typeName;
        }

        return $"{typeName}{suffix}";
    }

    private static string ToPascalName(string value)
    {
        var normalized = value
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? string.Empty;

        if (normalized.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^".cshtml".Length];
        }

        normalized = normalized.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (normalized.Contains('.', StringComparison.Ordinal))
        {
            normalized = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? normalized;
        }

        if (!normalized.Contains('-', StringComparison.Ordinal) &&
            !normalized.Contains(' ', StringComparison.Ordinal) &&
            !normalized.Contains('_', StringComparison.Ordinal))
        {
            return normalized;
        }

        var keepLeadingUnderscore = normalized.StartsWith('_');
        var converted = string.Concat(NameTokenPattern()
            .Matches(normalized)
            .Select(match => match.Value)
            .Where(token => token.Length > 0)
            .Select(ToPascalToken));

        return keepLeadingUnderscore ? $"_{converted}" : converted;
    }

    private static string ToPascalToken(string token)
    {
        return token.Length switch
        {
            0 => string.Empty,
            1 => token.ToUpperInvariant(),
            _ => char.ToUpperInvariant(token[0]) + token[1..]
        };
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

    [GeneratedRegex(@"<\s*(?<name>[a-z][a-z0-9]*(?:-[a-z0-9]+)+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CustomTagPattern();

    [GeneratedRegex(@"Component\s*\.\s*InvokeAsync\s*\(\s*""(?<name>[^""]+)""")]
    private static partial Regex ViewComponentStringPattern();

    [GeneratedRegex(@"Component\s*\.\s*InvokeAsync\s*\(\s*typeof\s*\(\s*(?<type>[A-Za-z_][A-Za-z0-9_\.]*)\s*\)")]
    private static partial Regex ViewComponentTypePattern();

    [GeneratedRegex(@"<\s*vc\s*:\s*(?<name>[a-z][a-z0-9]*(?:-[a-z0-9]+)*)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ViewComponentTagPattern();

    [GeneratedRegex(@"(?:Html\s*\.\s*)?(?:PartialAsync|RenderPartialAsync)\s*\(\s*""(?<name>[^""]+)""")]
    private static partial Regex PartialCallPattern();

    [GeneratedRegex(@"<\s*partial\b[^>]*\bname\s*=\s*[""'](?<name>[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex PartialTagPattern();

    [GeneratedRegex(@"[A-Za-z0-9]+")]
    private static partial Regex NameTokenPattern();

    [GeneratedRegex(@"[^A-Za-z0-9_\.]")]
    private static partial Regex InvalidIdentifierCharacterPattern();
}

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/RelationshipBuilder.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/RelationshipBuilder.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
namespace ClassDiagramMaker.Analysis;

internal static class RelationshipBuilder
{
    public static IReadOnlyList<DiagramRelationship> Build(
        IReadOnlyList<DiagramType> types,
        DiagramGenerationOptions options)
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

                var kind = target.Kind == DiagramTypeKind.Interface && type.Kind != DiagramTypeKind.Interface
                    ? DiagramRelationshipKind.Realization
                    : DiagramRelationshipKind.Inheritance;

                if (!ShouldInclude(kind, options))
                {
                    continue;
                }

                relationships.Add(new DiagramRelationship
                {
                    Kind = kind,
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

                    var kind = member.Kind is DiagramMemberKind.Field or DiagramMemberKind.Property or DiagramMemberKind.Event
                        ? DiagramRelationshipKind.Association
                        : DiagramRelationshipKind.Dependency;

                    if (!ShouldInclude(kind, options))
                    {
                        continue;
                    }

                    relationships.Add(new DiagramRelationship
                    {
                        Kind = kind,
                        FromTypeId = type.Id,
                        ToTypeId = target.Id,
                        Label = member.Name
                    });
                }
            }

            foreach (var dependency in type.Dependencies)
            {
                var target = index.Resolve(dependency.TypeName, type);
                if (target is null || target.Id == type.Id || !ShouldInclude(DiagramRelationshipKind.Dependency, options))
                {
                    continue;
                }

                relationships.Add(new DiagramRelationship
                {
                    Kind = DiagramRelationshipKind.Dependency,
                    FromTypeId = type.Id,
                    ToTypeId = target.Id,
                    Label = dependency.Label
                });
            }
        }

        return relationships
            .DistinctBy(relationship => $"{relationship.Kind}:{relationship.FromTypeId}:{relationship.ToTypeId}:{relationship.Label}")
            .OrderBy(relationship => relationship.Kind)
            .ThenBy(relationship => relationship.FromTypeId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.ToTypeId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ShouldInclude(DiagramRelationshipKind kind, DiagramGenerationOptions options)
    {
        return kind switch
        {
            DiagramRelationshipKind.Inheritance => options.IncludeInheritance,
            DiagramRelationshipKind.Realization => options.IncludeRealization,
            DiagramRelationshipKind.Association => options.IncludeAssociation,
            DiagramRelationshipKind.Dependency => options.IncludeDependency,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
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

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/TypeReferenceCollector.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker.Core/Analysis/TypeReferenceCollector.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
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

  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ClassDiagramMaker.Core\ClassDiagramMaker.Core.csproj" />
  </ItemGroup>
</Project>

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/Properties/PublishProfiles/win-x64-single-file.pubxml")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/Properties/PublishProfiles/win-x64-single-file.pubxml" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <TargetFramework>net9.0-windows</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
    <CopyOutputSymbolsToPublishDirectory>false</CopyOutputSymbolsToPublishDirectory>
    <PublishDir>$(MSBuildProjectDirectory)/../../artifacts/win-x64-single-file/</PublishDir>
  </PropertyGroup>
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
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, args) => ReportFatalError(args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                {
                    ReportFatalError(exception);
                }
            };

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(new ClassDiagramService()));
        }
        catch (Exception exception)
        {
            ReportFatalError(exception);
        }
    }

    private static void ReportFatalError(Exception exception)
    {
        var logPath = WriteErrorLog(exception);
        var message = string.IsNullOrWhiteSpace(logPath)
            ? $"画面の起動に失敗しました。{Environment.NewLine}{Environment.NewLine}{exception.Message}"
            : $"画面の起動に失敗しました。{Environment.NewLine}{Environment.NewLine}{exception.Message}{Environment.NewLine}{Environment.NewLine}ログ: {logPath}";

        try
        {
            MessageBox.Show(
                message,
                "ClassDiagramMaker",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // If the UI subsystem itself is unavailable, the log is the fallback.
        }
    }

    private static string? WriteErrorLog(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClassDiagramMaker");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, "ClassDiagramMaker.error.log");
            File.AppendAllText(
                path,
                $"[{DateTimeOffset.Now:O}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");

            return path;
        }
        catch
        {
            return null;
        }
    }
}

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/src/ClassDiagramMaker/MainForm.cs")"
cat > "$TARGET_DIR/src/ClassDiagramMaker/MainForm.cs" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
using ClassDiagramMaker.Analysis;

namespace ClassDiagramMaker;

public sealed class MainForm : Form
{
    private const int PreferredLogPanelHeight = 190;
    private const int PreferredLogPanelMinHeight = 120;
    private const int PreferredMermaidPanelMinHeight = 220;

    private readonly ClassDiagramService _service;
    private readonly TextBox _projectFolderTextBox = new();
    private readonly TextBox _searchFolderTextBox = new();
    private readonly TextBox _searchFileTextBox = new();
    private readonly TextBox _outputPathTextBox = new();
    private readonly ComboBox _displayModeComboBox = new();
    private readonly CheckBox _includeInheritanceCheckBox = new();
    private readonly CheckBox _includeRealizationCheckBox = new();
    private readonly CheckBox _includeAssociationCheckBox = new();
    private readonly CheckBox _includeDependencyCheckBox = new();
    private readonly CheckBox _splitOutputCheckBox = new();
    private readonly ComboBox _splitModeComboBox = new();
    private readonly CheckBox _includeSplitOverviewCheckBox = new();
    private readonly CheckBox _includeSplitIndexCheckBox = new();
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
            RowCount = 4,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var inputPanel = BuildInputPanel();
        var optionsPanel = BuildOptionsPanel();
        var progressPanel = BuildProgressPanel();
        var outputSplit = BuildOutputSplit();

        root.Controls.Add(inputPanel, 0, 0);
        root.Controls.Add(optionsPanel, 0, 1);
        root.Controls.Add(progressPanel, 0, 2);
        root.Controls.Add(outputSplit, 0, 3);
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
            Text = "検索対象ファイルが空の場合は再帰解析します。Razor は .cshtml と .cshtml.cs をペアで解析します。"
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

    private Control BuildOptionsPanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "表示オプション",
            Padding = new Padding(10)
        };

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 5
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 5; row++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var displayLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "表示モード"
        };

        _displayModeComboBox.Dock = DockStyle.Left;
        _displayModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _displayModeComboBox.Width = 220;
        _displayModeComboBox.Items.AddRange(new object[]
        {
            "型だけ",
            "主要メンバー",
            "全メンバー"
        });
        _displayModeComboBox.SelectedIndex = 2;

        var relationshipLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "関係"
        };

        var relationshipPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        ConfigureRelationshipCheckBox(_includeInheritanceCheckBox, "継承", checkedByDefault: true);
        ConfigureRelationshipCheckBox(_includeRealizationCheckBox, "interface 実装", checkedByDefault: true);
        ConfigureRelationshipCheckBox(_includeAssociationCheckBox, "フィールド/プロパティ関連", checkedByDefault: true);
        ConfigureRelationshipCheckBox(_includeDependencyCheckBox, "メソッド依存", checkedByDefault: true);

        relationshipPanel.Controls.Add(_includeInheritanceCheckBox);
        relationshipPanel.Controls.Add(_includeRealizationCheckBox);
        relationshipPanel.Controls.Add(_includeAssociationCheckBox);
        relationshipPanel.Controls.Add(_includeDependencyCheckBox);

        var splitLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "分割出力"
        };

        ConfigureRelationshipCheckBox(_splitOutputCheckBox, "分割して出力", checkedByDefault: false);
        _splitOutputCheckBox.CheckedChanged += (_, _) => UpdateSplitOptionState();

        var splitModeLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "分割単位"
        };

        _splitModeComboBox.Dock = DockStyle.Left;
        _splitModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _splitModeComboBox.Width = 220;
        _splitModeComboBox.Items.AddRange(new object[]
        {
            "namespace",
            "フォルダ"
        });
        _splitModeComboBox.SelectedIndex = 0;

        var splitFileLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "分割ファイル"
        };

        var splitFilePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        ConfigureRelationshipCheckBox(_includeSplitOverviewCheckBox, "全体図も出力", checkedByDefault: true);
        ConfigureRelationshipCheckBox(_includeSplitIndexCheckBox, "index.md を出力", checkedByDefault: true);

        splitFilePanel.Controls.Add(_includeSplitOverviewCheckBox);
        splitFilePanel.Controls.Add(_includeSplitIndexCheckBox);

        panel.Controls.Add(displayLabel, 0, 0);
        panel.Controls.Add(_displayModeComboBox, 1, 0);
        panel.Controls.Add(relationshipLabel, 0, 1);
        panel.Controls.Add(relationshipPanel, 1, 1);
        panel.Controls.Add(splitLabel, 0, 2);
        panel.Controls.Add(_splitOutputCheckBox, 1, 2);
        panel.Controls.Add(splitModeLabel, 0, 3);
        panel.Controls.Add(_splitModeComboBox, 1, 3);
        panel.Controls.Add(splitFileLabel, 0, 4);
        panel.Controls.Add(splitFilePanel, 1, 4);

        UpdateSplitOptionState();
        group.Controls.Add(panel);
        return group;
    }

    private static void ConfigureRelationshipCheckBox(CheckBox checkBox, string text, bool checkedByDefault)
    {
        checkBox.Text = text;
        checkBox.Checked = checkedByDefault;
        checkBox.AutoSize = true;
        checkBox.Margin = new Padding(0, 4, 18, 4);
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
            Orientation = Orientation.Horizontal
        };
        var splitterInitialized = false;
        var configuringSplitter = false;

        split.HandleCreated += (_, _) =>
        {
            configuringSplitter = true;
            try
            {
                splitterInitialized = ConfigureOutputSplit(split, usePreferredDistance: !splitterInitialized) || splitterInitialized;
            }
            finally
            {
                configuringSplitter = false;
            }
        };
        split.SizeChanged += (_, _) =>
        {
            configuringSplitter = true;
            try
            {
                splitterInitialized = ConfigureOutputSplit(split, usePreferredDistance: !splitterInitialized) || splitterInitialized;
            }
            finally
            {
                configuringSplitter = false;
            }
        };
        split.SplitterMoved += (_, _) =>
        {
            if (!configuringSplitter)
            {
                splitterInitialized = true;
            }
        };

        split.Panel1.Controls.Add(BuildTextSection("ログ", _logTextBox, readOnly: true));
        split.Panel2.Controls.Add(BuildTextSection("Mermaid", _mermaidTextBox, readOnly: false));
        return split;
    }

    private static bool ConfigureOutputSplit(SplitContainer split, bool usePreferredDistance)
    {
        var availableHeight = split.Height - split.SplitterWidth;
        if (availableHeight <= 0)
        {
            return false;
        }

        var panel1MinSize = Math.Min(PreferredLogPanelMinHeight, Math.Max(0, availableHeight / 3));
        var panel2MinSize = Math.Min(PreferredMermaidPanelMinHeight, Math.Max(0, availableHeight - panel1MinSize));
        var minDistance = panel1MinSize;
        var maxDistance = availableHeight - panel2MinSize;
        if (maxDistance < minDistance)
        {
            panel2MinSize = Math.Max(0, availableHeight - panel1MinSize);
            maxDistance = availableHeight - panel2MinSize;
        }

        split.Panel1MinSize = 0;
        split.Panel2MinSize = 0;

        var preferredDistance = Math.Min(PreferredLogPanelHeight, availableHeight / 2);
        var distance = usePreferredDistance
            ? preferredDistance
            : split.SplitterDistance;
        split.SplitterDistance = Math.Clamp(distance, minDistance, maxDistance);
        split.Panel1MinSize = panel1MinSize;
        split.Panel2MinSize = panel2MinSize;
        return usePreferredDistance && preferredDistance >= minDistance && preferredDistance <= maxDistance;
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
            Filter = "Supported source files (*.cs;*.cshtml)|*.cs;*.cshtml|C# files (*.cs)|*.cs|Razor files (*.cshtml)|*.cshtml|All files (*.*)|*.*",
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
            _outputLabel.Text = result.OutputPaths.Count > 1
                ? $"出力: {result.OutputPath} ({result.OutputPaths.Count} files)"
                : $"出力: {result.OutputPath}";
            _stageLabel.Text = "完了";
            _messageLabel.Text = $"生成完了: {result.TypeCount} types, {result.RelationshipCount} relationships";
            AppendLog($"Wrote {result.OutputPath}");
            foreach (var outputPath in result.OutputPaths.Skip(1))
            {
                AppendLog($"Wrote {outputPath}");
            }
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
            outputPath)
        {
            Options = new DiagramGenerationOptions(
                DisplayMode: GetSelectedDisplayMode(),
                IncludeInheritance: _includeInheritanceCheckBox.Checked,
                IncludeRealization: _includeRealizationCheckBox.Checked,
                IncludeAssociation: _includeAssociationCheckBox.Checked,
                IncludeDependency: _includeDependencyCheckBox.Checked)
            {
                SplitOutput = new DiagramSplitOptions(
                    Enabled: _splitOutputCheckBox.Checked,
                    Mode: GetSelectedSplitMode(),
                    IncludeOverview: _includeSplitOverviewCheckBox.Checked,
                    IncludeIndex: _includeSplitIndexCheckBox.Checked)
            }
        };
        return true;
    }

    private DiagramDisplayMode GetSelectedDisplayMode()
    {
        return _displayModeComboBox.SelectedIndex switch
        {
            0 => DiagramDisplayMode.TypeOnly,
            1 => DiagramDisplayMode.KeyMembers,
            _ => DiagramDisplayMode.AllMembers
        };
    }

    private DiagramSplitMode GetSelectedSplitMode()
    {
        return _splitModeComboBox.SelectedIndex switch
        {
            1 => DiagramSplitMode.Folder,
            _ => DiagramSplitMode.Namespace
        };
    }

    private void UpdateSplitOptionState()
    {
        var enabled = _splitOutputCheckBox.Checked;
        _splitModeComboBox.Enabled = enabled;
        _includeSplitOverviewCheckBox.Enabled = enabled;
        _includeSplitIndexCheckBox.Enabled = enabled;
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
OUTPUT_SH_FILE="$ROOT_DIR/bootstrap/ClassDiagramMaker.bootstrap.sh"
OUTPUT_PS1_FILE="$ROOT_DIR/bootstrap/ClassDiagramMaker.bootstrap.ps1"
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
  "src/ClassDiagramMaker.Core/Analysis/RazorPageCollector.cs"
  "src/ClassDiagramMaker.Core/Analysis/RelationshipBuilder.cs"
  "src/ClassDiagramMaker.Core/Analysis/SyntaxTypeCollector.cs"
  "src/ClassDiagramMaker.Core/Analysis/TypeReferenceCollector.cs"
  "src/ClassDiagramMaker/ClassDiagramMaker.csproj"
  "src/ClassDiagramMaker/Properties/PublishProfiles/win-x64-single-file.pubxml"
  "src/ClassDiagramMaker/Program.cs"
  "src/ClassDiagramMaker/MainForm.cs"
  "tools/generate-bootstrap.sh"
  "tools/publish-single-exe.ps1"
)

{
  printf '%s\n' '#!/bin/sh'
  printf '%s\n' 'set -eu'
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
  printf 'echo "Created ClassDiagramMaker source at $TARGET_DIR (%d files)"\n' "${#FILES[@]}"
  printf '%s\n' 'echo "Run on Windows: cd $TARGET_DIR && dotnet run --project src/ClassDiagramMaker/ClassDiagramMaker.csproj"'
} > "$OUTPUT_SH_FILE"

chmod +x "$OUTPUT_SH_FILE"

{
  printf '%s\n' 'param('
  printf '%s\n' '    [string]$TargetDir = "ClassDiagramMaker"'
  printf '%s\n' ')'
  printf '\n'
  printf '%s\n' '$ErrorActionPreference = "Stop"'
  printf '%s\n' '$files = @('

  for file in "${FILES[@]}"; do
    printf '%s\n' '    @{'
    printf "        Path = '%s'\n" "$file"
    printf '%s\n' '        Content = @"'
    base64 < "$ROOT_DIR/$file"
    printf '%s\n' '"@'
    printf '%s\n' '    }'
  done

  printf '%s\n' ')'
  printf '\n'
  printf '%s\n' 'New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null'
  printf '%s\n' 'foreach ($file in $files) {'
  printf '%s\n' '    $path = Join-Path $TargetDir $file.Path'
  printf '%s\n' '    $parent = Split-Path -Parent $path'
  printf '%s\n' '    if (-not [string]::IsNullOrWhiteSpace($parent)) {'
  printf '%s\n' '        New-Item -ItemType Directory -Force -Path $parent | Out-Null'
  printf '%s\n' '    }'
  printf '%s\n' '    [System.IO.File]::WriteAllBytes($path, [Convert]::FromBase64String($file.Content))'
  printf '%s\n' '}'
  printf '\n'
  printf 'Write-Host "Created ClassDiagramMaker source at $TargetDir (%d files)"\n' "${#FILES[@]}"
  printf '%s\n' 'Write-Host "Run on Windows: cd $TargetDir; dotnet run --project src/ClassDiagramMaker/ClassDiagramMaker.csproj"'
  printf '%s\n' 'Write-Host "Publish single exe: .\tools\publish-single-exe.ps1"'
} > "$OUTPUT_PS1_FILE"

printf 'Generated %s\n' "$OUTPUT_SH_FILE"
printf 'Generated %s\n' "$OUTPUT_PS1_FILE"

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

mkdir -p "$(dirname "$TARGET_DIR/tools/publish-single-exe.ps1")"
cat > "$TARGET_DIR/tools/publish-single-exe.ps1" <<'__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__'
param(
    [ValidateSet("win-x64", "win-arm64", "win-x86")]
    [string]$Runtime = "win-x64",

    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src/ClassDiagramMaker/ClassDiagramMaker.csproj"
$output = Join-Path $root "artifacts/$Runtime-single-file"

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:CopyOutputSymbolsToPublishDirectory=false `
    -o $output

Write-Host "Published single-file executable:"
Write-Host (Join-Path $output "ClassDiagramMaker.exe")

__CLASSDIAGRAMMAKER_BOOTSTRAP_FILE__

chmod +x "$TARGET_DIR/tools/generate-bootstrap.sh"
echo "Created ClassDiagramMaker source at $TARGET_DIR (18 files)"
echo "Run on Windows: cd $TARGET_DIR && dotnet run --project src/ClassDiagramMaker/ClassDiagramMaker.csproj"
