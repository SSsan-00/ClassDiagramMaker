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
