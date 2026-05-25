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
