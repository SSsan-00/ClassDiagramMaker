using ClassDiagramMaker.Analysis;
using Xunit;

namespace ClassDiagramMaker.Core.Tests;

public sealed class ClassDiagramServiceTests
{
    [Fact]
    public async Task GenerateAsync_WhenSearchFileIsEmpty_RecursivelyAnalyzesSearchFolder()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Services/UserService.cs",
            """
            namespace Demo.Services;

            public interface IUserRepository
            {
                User? Find(int id);
            }

            public sealed class UserService : IUserRepository
            {
                private readonly UserRepository repository;

                public UserService(UserRepository repository)
                {
                    this.repository = repository;
                }

                public User? Find(int id)
                {
                    return repository.Find(id);
                }
            }
            """);
        workspace.WriteSource(
            "Services/UserRepository.cs",
            """
            namespace Demo.Services;

            public sealed class UserRepository
            {
                public User? Find(int id) => null;
            }

            public sealed record User(int Id, string Name);
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(
                workspace.Root,
                Path.Combine(workspace.Root, "Services"),
                SearchFile: null,
                workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Equal(4, result.TypeCount);
        Assert.Contains("Demo_Services_IUserRepository <|.. Demo_Services_UserService", result.Mermaid);
        Assert.Contains("Demo_Services_UserService --> Demo_Services_UserRepository : repository", result.Mermaid);
        Assert.Contains("+Id: int", result.Mermaid);
        Assert.True(File.Exists(workspace.OutputPath));
    }

    [Fact]
    public async Task GenerateAsync_WhenSearchFileIsSpecified_AnalyzesOnlyThatFile()
    {
        using var workspace = TestWorkspace.Create();
        var selectedFile = workspace.WriteSource(
            "Selected.cs",
            """
            namespace Demo;

            public sealed class Selected
            {
            }
            """);
        workspace.WriteSource(
            "Ignored.cs",
            """
            namespace Demo;

            public sealed class Ignored
            {
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(
                workspace.Root,
                workspace.Root,
                selectedFile,
                workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Equal(1, result.TypeCount);
        Assert.Contains("class Demo_Selected", result.Mermaid);
        Assert.DoesNotContain("Demo_Ignored", result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_IncludesRazorPagesAndCodeBehind()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Pages/Users/Index.cshtml",
            """
            @page
            @model Demo.Pages.Users.IndexModel
            @inject Demo.Services.IUserRepository Repository

            <h1>@Model.Title</h1>

            @functions {
                public string Heading => "Users";
            }
            """);
        workspace.WriteSource(
            "Pages/Users/Index.cshtml.cs",
            """
            namespace Demo.Pages.Users
            {
                public sealed class IndexModel
                {
                    public Demo.Services.IUserRepository Repository { get; }
                    public string Title { get; } = "Users";
                }
            }

            namespace Demo.Services
            {
                public interface IUserRepository
                {
                }
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Equal(3, result.TypeCount);
        Assert.Contains("class Pages_Users_Index", result.Mermaid);
        Assert.Contains("<<razor page>>", result.Mermaid);
        Assert.Contains("+Model: Demo.Pages.Users.IndexModel", result.Mermaid);
        Assert.Contains("+Repository: Demo.Services.IUserRepository", result.Mermaid);
        Assert.Contains("+Heading: string", result.Mermaid);
        Assert.Contains("Pages_Users_Index --> Demo_Pages_Users_IndexModel : Model", result.Mermaid);
        Assert.Contains("Pages_Users_Index --> Demo_Services_IUserRepository : Repository", result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_WhenSearchFileIsCshtml_AlsoAnalyzesCodeBehind()
    {
        using var workspace = TestWorkspace.Create();
        var selectedFile = workspace.WriteSource(
            "Pages/About.cshtml",
            """
            @page
            @model Demo.Pages.AboutModel
            """);
        workspace.WriteSource(
            "Pages/About.cshtml.cs",
            """
            namespace Demo.Pages;

            public sealed class AboutModel
            {
                public string Title { get; } = "About";
            }
            """);
        workspace.WriteSource(
            "Pages/Ignored.cshtml",
            """
            @page
            @model Demo.Pages.IgnoredModel
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, selectedFile, workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Equal(2, result.TypeCount);
        Assert.Contains("class Pages_About", result.Mermaid);
        Assert.Contains("class Demo_Pages_AboutModel", result.Mermaid);
        Assert.Contains("+Model: Demo.Pages.AboutModel", result.Mermaid);
        Assert.Contains("+Title: string", result.Mermaid);
        Assert.Contains("Pages_About --> Demo_Pages_AboutModel : Model", result.Mermaid);
        Assert.DoesNotContain("Pages_Ignored", result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_WhenSearchFileIsCshtmlCodeBehind_AlsoAnalyzesRazorPage()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Pages/About.cshtml",
            """
            @page
            @model Demo.Pages.AboutModel
            """);
        var selectedFile = workspace.WriteSource(
            "Pages/About.cshtml.cs",
            """
            namespace Demo.Pages;

            public sealed class AboutModel
            {
                public string Title { get; } = "About";
            }
            """);
        workspace.WriteSource(
            "Pages/Ignored.cshtml",
            """
            @page
            @model Demo.Pages.IgnoredModel
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, selectedFile, workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Equal(2, result.TypeCount);
        Assert.Contains("class Pages_About", result.Mermaid);
        Assert.Contains("class Demo_Pages_AboutModel", result.Mermaid);
        Assert.Contains("+Model: Demo.Pages.AboutModel", result.Mermaid);
        Assert.Contains("+Title: string", result.Mermaid);
        Assert.Contains("Pages_About --> Demo_Pages_AboutModel : Model", result.Mermaid);
        Assert.DoesNotContain("Pages_Ignored", result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_ReportsProgressThroughCompletion()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Sample.cs",
            """
            namespace Demo;

            public sealed class Sample
            {
            }
            """);
        var progress = new RecordingProgress();

        await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath),
            progress,
            CancellationToken.None);

        var updates = progress.Updates;
        Assert.Contains(updates, update => update.Stage == "Scanning");
        Assert.Contains(updates, update => update.Stage == "Writing" && update.Percent == 100);
        Assert.All(updates.Where(update => update.TotalFiles > 0), update => Assert.Equal(1, update.TotalFiles));
    }

    [Fact]
    public async Task GenerateAsync_RendersModifiersAndGenericConstraints()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Repository.cs",
            """
            namespace Demo;

            public abstract class Repository<T>
                where T : class, new()
            {
                protected static readonly string CacheKey = typeof(T).Name;

                public abstract T Create<TArg>(TArg arg)
                    where TArg : struct;
            }

            public readonly struct Snapshot<T>
                where T : unmanaged
            {
                public static int Count { get; }
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Contains("<<abstract>>", result.Mermaid);
        Assert.Contains("<<readonly>>", result.Mermaid);
        Assert.Contains("where T : class, new()", result.Mermaid);
        Assert.Contains("where T : unmanaged", result.Mermaid);
        Assert.Contains("#{static readonly} CacheKey: string", result.Mermaid);
        Assert.Contains("+{abstract} Create~TArg~(arg: TArg): T where TArg : struct", result.Mermaid);
        Assert.Contains("+{static} Count: int", result.Mermaid);
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string root)
        {
            Root = root;
            OutputPath = Path.Combine(root, "diagram.mmd");
        }

        public string Root { get; }

        public string OutputPath { get; }

        public static TestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"ClassDiagramMaker.Tests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new TestWorkspace(root);
        }

        public string WriteSource(string relativePath, string source)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, source);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class RecordingProgress : IProgress<GenerationProgress>
    {
        private readonly List<GenerationProgress> _updates = new();

        public IReadOnlyList<GenerationProgress> Updates => _updates;

        public void Report(GenerationProgress value)
        {
            _updates.Add(value);
        }
    }
}
