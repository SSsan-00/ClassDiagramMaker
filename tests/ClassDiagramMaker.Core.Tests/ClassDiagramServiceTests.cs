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

    [Fact]
    public async Task GenerateAsync_WhenDisplayModeIsTypeOnly_HidesMembersButKeepsTypeMetadata()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Repository.cs",
            """
            namespace Demo;

            public abstract class Repository<T>
                where T : class
            {
                public string Name { get; }
                public T Create() => throw new System.NotImplementedException();
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath)
            {
                Options = new DiagramGenerationOptions(DisplayMode: DiagramDisplayMode.TypeOnly)
            },
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Contains("class Demo_Repository_T", result.Mermaid);
        Assert.Contains("<<abstract>>", result.Mermaid);
        Assert.Contains("where T : class", result.Mermaid);
        Assert.DoesNotContain("+Name: string", result.Mermaid);
        Assert.DoesNotContain("+Create(): T", result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_WhenDisplayModeIsKeyMembers_HidesMethodsAndConstructors()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "UserService.cs",
            """
            namespace Demo;

            public sealed class UserService
            {
                private readonly UserRepository repository;

                public UserService(UserRepository repository)
                {
                    this.repository = repository;
                }

                public User? Current { get; }

                public User? Find(int id) => Current;
            }

            public sealed class UserRepository
            {
            }

            public sealed class User
            {
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath)
            {
                Options = new DiagramGenerationOptions(DisplayMode: DiagramDisplayMode.KeyMembers)
            },
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Contains("-{readonly} repository: UserRepository", result.Mermaid);
        Assert.Contains("+Current: User?", result.Mermaid);
        Assert.DoesNotContain("+UserService(repository: UserRepository)", result.Mermaid);
        Assert.DoesNotContain("+Find(id: int): User?", result.Mermaid);
    }

    [Theory]
    [InlineData(true, false, false, false, "Demo_BaseService <|-- Demo_UserService", "<|..", "-->", "..>")]
    [InlineData(false, true, false, false, "Demo_IUserService <|.. Demo_UserService", "<|--", "-->", "..>")]
    [InlineData(false, false, true, false, "Demo_UserService --> Demo_UserRepository : repository", "<|--", "<|..", "..>")]
    [InlineData(false, false, false, true, "Demo_UserService ..> Demo_UserDto : Create", "<|--", "<|..", "-->")]
    public async Task GenerateAsync_FiltersRelationshipKinds(
        bool includeInheritance,
        bool includeRealization,
        bool includeAssociation,
        bool includeDependency,
        string expectedLine,
        string unexpectedToken1,
        string unexpectedToken2,
        string unexpectedToken3)
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "UserService.cs",
            """
            namespace Demo;

            public abstract class BaseService
            {
            }

            public interface IUserService
            {
            }

            public sealed class UserService : BaseService, IUserService
            {
                private readonly UserRepository repository;

                public UserDto Create(CreateUserCommand command) => new();
            }

            public sealed class UserRepository
            {
            }

            public sealed class UserDto
            {
            }

            public sealed class CreateUserCommand
            {
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath)
            {
                Options = new DiagramGenerationOptions(
                    IncludeInheritance: includeInheritance,
                    IncludeRealization: includeRealization,
                    IncludeAssociation: includeAssociation,
                    IncludeDependency: includeDependency)
            },
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Contains(expectedLine, result.Mermaid);
        Assert.DoesNotContain(unexpectedToken1, result.Mermaid);
        Assert.DoesNotContain(unexpectedToken2, result.Mermaid);
        Assert.DoesNotContain(unexpectedToken3, result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_WhenSplitOutputByNamespace_WritesNamespaceDiagramsAndIndex()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Services/UserService.cs",
            """
            namespace Demo.Services;

            public sealed class UserService
            {
                private readonly UserRepository repository;

                public Demo.Models.UserDto Create(Demo.Models.CreateUserCommand command) => new();
            }

            public sealed class UserRepository
            {
            }
            """);
        workspace.WriteSource(
            "Models/UserDto.cs",
            """
            namespace Demo.Models;

            public sealed class UserDto
            {
            }

            public sealed class CreateUserCommand
            {
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath)
            {
                Options = new DiagramGenerationOptions
                {
                    SplitOutput = new DiagramSplitOptions(
                        Enabled: true,
                        Mode: DiagramSplitMode.Namespace,
                        IncludeOverview: true,
                        IncludeIndex: true)
                }
            },
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        var indexPath = workspace.GetOutputPath(".index.md");
        var overviewPath = workspace.GetOutputPath(".all.mmd");
        var servicesPath = workspace.GetOutputPath(".Demo.Services.mmd");
        var modelsPath = workspace.GetOutputPath(".Demo.Models.mmd");

        Assert.Equal(indexPath, result.OutputPath);
        Assert.Contains(indexPath, result.OutputPaths);
        Assert.Contains(overviewPath, result.OutputPaths);
        Assert.Contains(servicesPath, result.OutputPaths);
        Assert.Contains(modelsPath, result.OutputPaths);

        var index = File.ReadAllText(indexPath);
        Assert.Contains("[All](diagram.all.mmd)", index);
        Assert.Contains("[Demo.Services](diagram.Demo.Services.mmd)", index);
        Assert.Contains("[Demo.Models](diagram.Demo.Models.mmd)", index);

        var servicesDiagram = File.ReadAllText(servicesPath);
        Assert.Contains("class Demo_Services_UserService", servicesDiagram);
        Assert.Contains("class Demo_Services_UserRepository", servicesDiagram);
        Assert.DoesNotContain("Demo_Models_UserDto", servicesDiagram);
        Assert.Contains("Demo_Services_UserService --> Demo_Services_UserRepository : repository", servicesDiagram);
        Assert.DoesNotContain("Demo_Services_UserService ..> Demo_Models_UserDto", servicesDiagram);

        var overviewDiagram = File.ReadAllText(overviewPath);
        Assert.Contains("class Demo_Models_UserDto", overviewDiagram);
        Assert.Contains("Demo_Services_UserService ..> Demo_Models_UserDto : Create", overviewDiagram);
    }

    [Fact]
    public async Task GenerateAsync_WhenSplitOutputByFolder_WritesFolderDiagrams()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Domain/User.cs",
            """
            namespace Demo.Domain;

            public sealed class User
            {
                public string Name { get; }
            }
            """);
        workspace.WriteSource(
            "Application/UserService.cs",
            """
            namespace Demo.Application;

            public sealed class UserService
            {
                public Demo.Domain.User Find() => new();
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath)
            {
                Options = new DiagramGenerationOptions
                {
                    SplitOutput = new DiagramSplitOptions(
                        Enabled: true,
                        Mode: DiagramSplitMode.Folder,
                        IncludeOverview: false,
                        IncludeIndex: false)
                }
            },
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        var domainPath = workspace.GetOutputPath(".Domain.mmd");
        var applicationPath = workspace.GetOutputPath(".Application.mmd");

        Assert.Equal(applicationPath, result.OutputPath);
        Assert.Equal(new[] { applicationPath, domainPath }, result.OutputPaths);
        Assert.False(File.Exists(workspace.GetOutputPath(".index.md")));
        Assert.False(File.Exists(workspace.GetOutputPath(".all.mmd")));

        var applicationDiagram = File.ReadAllText(applicationPath);
        Assert.Contains("class Demo_Application_UserService", applicationDiagram);
        Assert.DoesNotContain("Demo_Domain_User", applicationDiagram);
        Assert.DoesNotContain("Demo_Application_UserService ..> Demo_Domain_User", applicationDiagram);

        var domainDiagram = File.ReadAllText(domainPath);
        Assert.Contains("class Demo_Domain_User", domainDiagram);
        Assert.Contains("+Name: string", domainDiagram);
    }

    [Fact]
    public async Task GenerateAsync_WhenSplitOutputUsesTypeOnly_AppliesDisplayModeToSplitFiles()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Services/UserService.cs",
            """
            namespace Demo.Services;

            public sealed class UserService
            {
                public string Name { get; }
            }
            """);

        await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath)
            {
                Options = new DiagramGenerationOptions(DisplayMode: DiagramDisplayMode.TypeOnly)
                {
                    SplitOutput = new DiagramSplitOptions(
                        Enabled: true,
                        Mode: DiagramSplitMode.Namespace,
                        IncludeOverview: false,
                        IncludeIndex: false)
                }
            },
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        var servicesDiagram = File.ReadAllText(workspace.GetOutputPath(".Demo.Services.mmd"));
        Assert.Contains("class Demo_Services_UserService", servicesDiagram);
        Assert.DoesNotContain("+Name: string", servicesDiagram);
    }

    [Fact]
    public async Task GenerateAsync_WhenSplitOutputHasNoTypes_WritesEmptyDiagram()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Empty.cs",
            """
            namespace Demo;
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath)
            {
                Options = new DiagramGenerationOptions
                {
                    SplitOutput = new DiagramSplitOptions(
                        Enabled: true,
                        Mode: DiagramSplitMode.Namespace,
                        IncludeOverview: false,
                        IncludeIndex: false)
                }
            },
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        var emptyPath = workspace.GetOutputPath(".empty.mmd");
        Assert.Equal(emptyPath, result.OutputPath);
        Assert.Equal(new[] { emptyPath }, result.OutputPaths);
        Assert.Contains("classDiagram", File.ReadAllText(emptyPath));
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

        public string GetOutputPath(string suffix)
        {
            return Path.Combine(
                Root,
                $"{Path.GetFileNameWithoutExtension(OutputPath)}{suffix}");
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
