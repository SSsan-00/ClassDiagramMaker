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
