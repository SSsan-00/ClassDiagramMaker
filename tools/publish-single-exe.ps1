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
