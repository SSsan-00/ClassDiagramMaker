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

The GUI also provides output options for large projects:

- Display mode: type only, key members, or all members
- Relationships: inheritance, interface implementation, field/property association, and method dependency can be toggled independently
- Split output: generate separate Mermaid files by namespace or folder, with optional `index.md` and all-in-one diagram files

When the search file is empty, the tool recursively analyzes `.cs`, `.cshtml.cs`, and `.cshtml` files under the search folder. The GUI shows parsing and rendering progress while the Mermaid file is generated.

Razor `.cshtml` files are represented as Razor page nodes. The analyzer includes `@model`, `@inject`, and members declared in `@functions` / `@code` blocks. `.cshtml.cs` code-behind files are parsed as normal C# source.

When a single Razor file is selected, the pair is analyzed together:

- Selecting `Page.cshtml` also analyzes `Page.cshtml.cs` when it exists.
- Selecting `Page.cshtml.cs` also analyzes `Page.cshtml` when it exists.

## Tests

Core analysis behavior is covered with xUnit.

```bash
dotnet test ClassDiagramMaker.sln
```

## Release

Build a single Windows executable with PowerShell:

```powershell
./tools/publish-single-exe.ps1
```

The default output is:

```text
artifacts/win-x64-single-file/ClassDiagramMaker.exe
```

To publish another Windows runtime:

```powershell
./tools/publish-single-exe.ps1 -Runtime win-arm64
./tools/publish-single-exe.ps1 -Runtime win-x86
```

The equivalent `dotnet publish` command is:

```bash
dotnet publish src/ClassDiagramMaker/ClassDiagramMaker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false -p:CopyOutputSymbolsToPublishDirectory=false -o artifacts/win-x64-single-file
```

The publish profile `win-x64-single-file` is also available:

```bash
dotnet publish src/ClassDiagramMaker/ClassDiagramMaker.csproj -p:PublishProfile=win-x64-single-file
```

## Split Output

When split output is enabled, the selected output path is used as a file name prefix.
For example, `diagram.mmd` can generate:

```text
diagram.index.md
diagram.all.mmd
diagram.Demo.Services.mmd
diagram.Demo.Models.mmd
```

Namespace splitting groups types by C# namespace. Folder splitting groups types by their source folder relative to the target project folder. Relationships in split diagrams are limited to types inside the same split file, while the optional `*.all.mmd` keeps the full diagram.

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
    class Pages_Users_Index {
        <<razor page>>
        +Model: Demo.Pages.Users.IndexModel
        +Repository: Demo.Services.IUserRepository
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
