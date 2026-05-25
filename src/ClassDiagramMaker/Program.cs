using System.Text.Json.Serialization;
using ClassDiagramMaker.Analysis;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = ResolveContentRoot(),
    WebRootPath = "wwwroot"
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<ClassDiagramService>();
builder.Services.AddSingleton<DiagramJobStore>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/generate", (GenerationRequest request, DiagramJobStore jobs, ClassDiagramService service) =>
{
    var job = jobs.Create(request);

    _ = Task.Run(async () =>
    {
        try
        {
            jobs.Update(job.Id, snapshot => snapshot with
            {
                Status = JobStatus.Running,
                Message = "Starting analysis..."
            });

            var progress = new Progress<GenerationProgress>(update =>
            {
                jobs.Update(job.Id, snapshot => snapshot with
                {
                    Status = JobStatus.Running,
                    Percent = update.Percent,
                    Stage = update.Stage,
                    Message = update.Message,
                    ProcessedFiles = update.ProcessedFiles,
                    TotalFiles = update.TotalFiles,
                    Log = snapshot.Log.Append(update.Message).TakeLast(80).ToArray()
                });
            });

            var result = await service.GenerateAsync(request, progress, CancellationToken.None);

            jobs.Update(job.Id, snapshot => snapshot with
            {
                Status = JobStatus.Completed,
                Percent = 100,
                Stage = "Completed",
                Message = $"Generated {result.TypeCount} types and {result.RelationshipCount} relationships.",
                OutputPath = result.OutputPath,
                Mermaid = result.Mermaid,
                FinishedAt = DateTimeOffset.UtcNow,
                Log = snapshot.Log
                    .Append($"Wrote Mermaid output: {result.OutputPath}")
                    .Append($"Types: {result.TypeCount}, relationships: {result.RelationshipCount}")
                    .TakeLast(80)
                    .ToArray()
            });
        }
        catch (Exception ex)
        {
            jobs.Update(job.Id, snapshot => snapshot with
            {
                Status = JobStatus.Failed,
                Stage = "Failed",
                Message = ex.Message,
                Error = ex.ToString(),
                FinishedAt = DateTimeOffset.UtcNow,
                Log = snapshot.Log.Append(ex.Message).TakeLast(80).ToArray()
            });
        }
    });

    return Results.Accepted($"/api/jobs/{job.Id}", new { job.Id });
});

app.MapGet("/api/jobs/{id:guid}", (Guid id, DiagramJobStore jobs) =>
{
    var snapshot = jobs.Get(id);
    return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
});

app.MapGet("/api/jobs/{id:guid}/output", (Guid id, DiagramJobStore jobs) =>
{
    var snapshot = jobs.Get(id);
    if (snapshot is null)
    {
        return Results.NotFound();
    }

    if (snapshot.Status != JobStatus.Completed || string.IsNullOrWhiteSpace(snapshot.Mermaid))
    {
        return Results.BadRequest(new { error = "Output is not ready." });
    }

    return Results.Text(snapshot.Mermaid, "text/plain");
});

app.Run();

static string ResolveContentRoot()
{
    var publishedRoot = AppContext.BaseDirectory;
    if (Directory.Exists(Path.Combine(publishedRoot, "wwwroot")))
    {
        return publishedRoot;
    }

    var sourceRoot = Path.GetFullPath(Path.Combine(publishedRoot, "..", "..", ".."));
    if (Directory.Exists(Path.Combine(sourceRoot, "wwwroot")))
    {
        return sourceRoot;
    }

    return Directory.GetCurrentDirectory();
}
