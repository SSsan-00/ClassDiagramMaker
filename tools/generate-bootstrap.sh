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
