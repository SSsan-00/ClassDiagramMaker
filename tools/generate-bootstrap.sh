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
