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

    public RelatedTypeOptions RelatedTypes { get; init; } = RelatedTypeOptions.Disabled;
}

public sealed record RelatedTypeOptions(
    bool Enabled = false,
    int Depth = 1,
    bool Unlimited = false)
{
    public static RelatedTypeOptions Disabled { get; } = new();
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
