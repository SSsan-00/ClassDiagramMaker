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
        Assert.Contains("+int Id", result.Mermaid);
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
        Assert.Contains("+Demo.Pages.Users.IndexModel Model", result.Mermaid);
        Assert.Contains("+Demo.Services.IUserRepository Repository", result.Mermaid);
        Assert.Contains("+string Heading", result.Mermaid);
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
        Assert.Contains("+Demo.Pages.AboutModel Model", result.Mermaid);
        Assert.Contains("+string Title", result.Mermaid);
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
        Assert.Contains("+Demo.Pages.AboutModel Model", result.Mermaid);
        Assert.Contains("+string Title", result.Mermaid);
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
        Assert.Contains("#string CacheKey$", result.Mermaid);
        Assert.Contains("+Create~TArg~(TArg arg) T*", result.Mermaid);
        Assert.Contains("+int Count$", result.Mermaid);
        Assert.Contains("note for Demo_Repository_T", result.Mermaid);
        Assert.Contains("CacheKey modifiers: static readonly", result.Mermaid);
        Assert.Contains("Create constraints: where TArg : struct", result.Mermaid);
        AssertClassBodyLinesDoNotContain(result.Mermaid, "{");
        AssertClassBodyLinesDoNotContain(result.Mermaid, ": ");
    }

    [Fact]
    public async Task GenerateAsync_ParsesNestedTypesAcrossNamespaceStyles()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "FileScoped.cs",
            """
            namespace Demo.FileScoped;

            public abstract partial class Repository<T>
                where T : class
            {
                protected sealed class Entry
                {
                    public T Value { get; }
                }
            }
            """);
        workspace.WriteSource(
            "BlockScoped.cs",
            """
            namespace Demo.BlockScoped
            {
                internal readonly record struct Snapshot<T>(T Value)
                    where T : unmanaged;

                public enum SnapshotState
                {
                    Created,
                    Saved
                }
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Equal(4, result.TypeCount);
        Assert.Contains("class Demo_FileScoped_Repository_T", result.Mermaid);
        Assert.Contains("<<abstract>>", result.Mermaid);
        Assert.Contains("where T : class", result.Mermaid);
        Assert.Contains("class Demo_FileScoped_Repository_T_Entry", result.Mermaid);
        Assert.Contains("<<sealed>>", result.Mermaid);
        Assert.Contains("+T Value", result.Mermaid);
        Assert.Contains("class Demo_BlockScoped_Snapshot_T", result.Mermaid);
        Assert.Contains("<<record>>", result.Mermaid);
        Assert.Contains("<<readonly>>", result.Mermaid);
        Assert.Contains("where T : unmanaged", result.Mermaid);
        Assert.Contains("class Demo_BlockScoped_SnapshotState", result.Mermaid);
        Assert.Contains("<<enumeration>>", result.Mermaid);
        Assert.Contains("Created", result.Mermaid);
        Assert.Contains("Saved", result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_ParsesInterfaceMembersWithImplicitPublicVisibility()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "IRepository.cs",
            """
            namespace Demo;

            public interface IRepository<T>
            {
                T Current { get; }
                event System.EventHandler Changed;
                T this[int index] { get; }
                T Find<TQuery>(TQuery query)
                    where TQuery : class;
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Contains("class Demo_IRepository_T", result.Mermaid);
        Assert.Contains("<<interface>>", result.Mermaid);
        Assert.Contains("+T Current", result.Mermaid);
        Assert.Contains("+System.EventHandler Changed", result.Mermaid);
        Assert.Contains("+this(int index) T", result.Mermaid);
        Assert.Contains("+Find~TQuery~(TQuery query) T", result.Mermaid);
        Assert.DoesNotContain("-T Current", result.Mermaid);
        Assert.DoesNotContain("-Find~TQuery~", result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_CollectsReferencesFromGenericArrayTupleNullableAndAliasQualifiedTypes()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Aggregate.cs",
            """
            namespace Demo;

            public sealed class Aggregate
            {
                private readonly System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<UserDto?>> users;

                public (UserDto User, UserStatus Status) Get(UserId[] ids, global::Demo.UserDto? fallback) => default;
            }

            public sealed class UserDto
            {
            }

            public sealed class UserId
            {
            }

            public enum UserStatus
            {
                Active
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Contains("-System.Collections.Generic.IReadOnlyDictionary~string_System.Collections.Generic.IReadOnlyList~UserDto?~~ users", result.Mermaid);
        Assert.Contains("Demo_Aggregate --> Demo_UserDto : users", result.Mermaid);
        Assert.Contains("Demo_Aggregate ..> Demo_UserDto : Get", result.Mermaid);
        Assert.Contains("Demo_Aggregate ..> Demo_UserStatus : Get", result.Mermaid);
        Assert.Contains("Demo_Aggregate ..> Demo_UserId : Get", result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_MergesPartialTypesAndPreservesRelationshipsFromEachDeclaration()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "CustomerService.Part1.cs",
            """
            namespace Demo;

            public abstract class BaseService
            {
            }

            public partial class CustomerService : BaseService
            {
                public Customer Current { get; }
            }
            """);
        workspace.WriteSource(
            "CustomerService.Part2.cs",
            """
            namespace Demo;

            public interface ICustomerService
            {
            }

            public partial class CustomerService : ICustomerService
            {
                public Customer Find(CustomerId id) => new();
            }

            public sealed class Customer
            {
            }

            public sealed class CustomerId
            {
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Equal(5, result.TypeCount);
        Assert.Equal(1, CountOccurrences(result.Mermaid, "class Demo_CustomerService {"));
        Assert.Contains("<<partial>>", result.Mermaid);
        Assert.Contains("+Customer Current", result.Mermaid);
        Assert.Contains("+Find(CustomerId id) Customer", result.Mermaid);
        Assert.Contains("Demo_BaseService <|-- Demo_CustomerService", result.Mermaid);
        Assert.Contains("Demo_ICustomerService <|.. Demo_CustomerService", result.Mermaid);
        Assert.Contains("Demo_CustomerService --> Demo_Customer : Current", result.Mermaid);
        Assert.Contains("Demo_CustomerService ..> Demo_CustomerId : Find", result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_CollectsDependenciesFromMethodBodiesAliasesAndUsingStatic()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "UserService.cs",
            """
            using RepoAlias = Demo.Repositories.UserRepository;
            using static Demo.Helpers.StaticHelper;

            namespace Demo.Services;

            public sealed class UserService
            {
                public UserDto Run(System.IServiceProvider services)
                {
                    RepoAlias repository = new RepoAlias();
                    var dto = (UserDto)repository.Load();
                    var audit = repository.LoadAudit();
                    if (dto is ActiveUserDto active)
                    {
                        Touch(active);
                    }

                    var fromService = services.GetRequiredService<IUserRepository>();
                    Touch(audit);
                    return fromService.Create(dto);
                }

                public object FindAudit()
                {
                    RepoAlias repository = new RepoAlias();
                    return repository.LoadAudit();
                }
            }

            public static class ServiceProviderExtensions
            {
                public static T GetRequiredService<T>(this System.IServiceProvider services) => default!;
            }
            """);
        workspace.WriteSource(
            "Dependencies.cs",
            """
            namespace Demo.Repositories
            {
                public sealed class UserRepository
                {
                    public object Load() => new();

                    public AuditLog LoadAudit() => new();
                }

                public sealed class AuditLog
                {
                }
            }

            namespace Demo.Services
            {
                public interface IUserRepository
                {
                    UserDto Create(UserDto dto);
                }

                public class UserDto
                {
                }

                public sealed class ActiveUserDto : UserDto
                {
                }
            }

            namespace Demo.Helpers
            {
                public static class StaticHelper
                {
                    public static void Touch(object value)
                    {
                    }
                }
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Contains("Demo_Services_UserService ..> Demo_Repositories_UserRepository : Run", result.Mermaid);
        Assert.Contains("Demo_Services_UserService ..> Demo_Services_UserDto : Run", result.Mermaid);
        Assert.Contains("Demo_Services_UserService ..> Demo_Services_ActiveUserDto : Run", result.Mermaid);
        Assert.Contains("Demo_Services_UserService ..> Demo_Services_IUserRepository : Run", result.Mermaid);
        Assert.Contains("Demo_Services_UserService ..> Demo_Repositories_AuditLog : Run", result.Mermaid);
        Assert.Contains("Demo_Services_UserService ..> Demo_Repositories_AuditLog : FindAudit", result.Mermaid);
        Assert.Contains("Demo_Services_UserService ..> Demo_Helpers_StaticHelper : Run", result.Mermaid);
        Assert.Contains("Demo_Services_UserService ..> Demo_Helpers_StaticHelper : using static", result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_CollectsDependenciesFromAttributesConstraintsAndGenericBaseArguments()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Controller.cs",
            """
            namespace Demo;

            [ServiceFilter(typeof(AuditFilter))]
            public sealed class UserController<TValidator> : BaseController<UserDto>
                where TValidator : IValidator<UserDto>
            {
                [Inject]
                public IUserRepository Repository { get; }
            }

            public abstract class BaseController<T>
            {
            }

            public sealed class UserDto
            {
            }

            public interface IValidator<T>
            {
            }

            public interface IUserRepository
            {
            }

            public sealed class AuditFilter
            {
            }

            public sealed class ServiceFilterAttribute : System.Attribute
            {
                public ServiceFilterAttribute(System.Type type)
                {
                }
            }

            public sealed class InjectAttribute : System.Attribute
            {
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Contains("Demo_BaseController_T <|-- Demo_UserController_TValidator", result.Mermaid);
        Assert.Contains("Demo_UserController_TValidator ..> Demo_UserDto : base", result.Mermaid);
        Assert.Contains("Demo_UserController_TValidator ..> Demo_IValidator_T : where TValidator", result.Mermaid);
        Assert.Contains("Demo_UserController_TValidator ..> Demo_UserDto : where TValidator", result.Mermaid);
        Assert.Contains("Demo_UserController_TValidator ..> Demo_AuditFilter : attribute", result.Mermaid);
        Assert.Contains("Demo_UserController_TValidator ..> Demo_ServiceFilterAttribute : attribute", result.Mermaid);
        Assert.Contains("Demo_UserController_TValidator --> Demo_IUserRepository : Repository", result.Mermaid);
        Assert.Contains("Demo_UserController_TValidator --> Demo_InjectAttribute : Repository", result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_CollectsDelegateAndClassPrimaryConstructorDependencies()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Worker.cs",
            """
            namespace Demo;

            public sealed class Worker(UserRepository repository, IClock clock)
            {
                public UserDto Run() => repository.Load(clock.Now);
            }

            public delegate UserDto UserMapper(UserEntity entity);

            public sealed class UserRepository
            {
                public UserDto Load(System.DateTime now) => new();
            }

            public interface IClock
            {
                System.DateTime Now { get; }
            }

            public sealed class UserDto
            {
            }

            public sealed class UserEntity
            {
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Equal(6, result.TypeCount);
        Assert.Contains("class Demo_UserMapper", result.Mermaid);
        Assert.Contains("<<delegate>>", result.Mermaid);
        Assert.Contains("+Invoke(UserEntity entity) UserDto", result.Mermaid);
        Assert.Contains("Demo_Worker ..> Demo_UserRepository : Worker", result.Mermaid);
        Assert.Contains("Demo_Worker ..> Demo_IClock : Worker", result.Mermaid);
        Assert.Contains("Demo_Worker ..> Demo_UserDto : Run", result.Mermaid);
        Assert.Contains("Demo_UserMapper ..> Demo_UserDto : Invoke", result.Mermaid);
        Assert.Contains("Demo_UserMapper ..> Demo_UserEntity : Invoke", result.Mermaid);
    }

    [Fact]
    public async Task GenerateAsync_CollectsRazorMarkupReferencesForTagHelpersViewComponentsAndPartials()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Pages/Users/Index.cshtml",
            """
            @page
            @model Demo.Pages.Users.IndexModel
            @addTagHelper *, Demo

            <user-card user="Model.User"></user-card>
            @await Component.InvokeAsync("UserSummary", new { id = Model.User.Id })
            @await Html.PartialAsync("_UserRow", Model.User)
            """);
        workspace.WriteSource(
            "Pages/Users/Index.cshtml.cs",
            """
            namespace Demo.Pages.Users
            {
                public sealed class IndexModel
                {
                    public Demo.Models.UserDto User { get; }
                }

                public sealed class _UserRow
                {
                }
            }

            namespace Demo.Models
            {
                public sealed class UserDto
                {
                    public int Id { get; }
                }
            }

            namespace Demo.TagHelpers
            {
                public sealed class UserCardTagHelper
                {
                }
            }

            namespace Demo.ViewComponents
            {
                public sealed class UserSummaryViewComponent
                {
                }
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath),
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Contains("Pages_Users_Index --> Demo_Pages_Users_IndexModel : Model", result.Mermaid);
        Assert.Contains("Pages_Users_Index ..> Demo_TagHelpers_UserCardTagHelper : tag helper", result.Mermaid);
        Assert.Contains("Pages_Users_Index ..> Demo_ViewComponents_UserSummaryViewComponent : view component", result.Mermaid);
        Assert.Contains("Pages_Users_Index ..> Demo_Pages_Users_UserRow : partial", result.Mermaid);
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
        Assert.DoesNotContain("+string Name", result.Mermaid);
        Assert.DoesNotContain("+Create() T", result.Mermaid);
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

        Assert.Contains("-UserRepository repository", result.Mermaid);
        Assert.Contains("+User? Current", result.Mermaid);
        Assert.DoesNotContain("+UserService(UserRepository repository)", result.Mermaid);
        Assert.DoesNotContain("+Find(int id) User?", result.Mermaid);
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
    public async Task GenerateAsync_WhenDependencyFilterIsOff_HidesSemanticAndMarkupDependencies()
    {
        using var workspace = TestWorkspace.Create();
        workspace.WriteSource(
            "Dashboard.cs",
            """
            using static Demo.StaticHelper;

            namespace Demo;

            [ServiceFilter(typeof(AuditFilter))]
            public sealed class Dashboard
            {
                public DashboardDto Create()
                {
                    Touch();
                    return new DashboardDto();
                }
            }

            public sealed class DashboardDto
            {
            }

            public static class StaticHelper
            {
                public static void Touch()
                {
                }
            }

            public sealed class ServiceFilterAttribute : System.Attribute
            {
                public ServiceFilterAttribute(System.Type type)
                {
                }
            }

            public sealed class AuditFilter
            {
            }
            """);
        workspace.WriteSource(
            "Pages/Index.cshtml",
            """
            @page
            <user-card></user-card>
            @await Component.InvokeAsync("Dashboard")
            """);
        workspace.WriteSource(
            "Pages/Index.cshtml.cs",
            """
            namespace Demo;

            public sealed class UserCardTagHelper
            {
            }

            public sealed class DashboardViewComponent
            {
            }
            """);

        var result = await new ClassDiagramService().GenerateAsync(
            new GenerationRequest(workspace.Root, workspace.Root, null, workspace.OutputPath)
            {
                Options = new DiagramGenerationOptions(IncludeDependency: false)
            },
            new Progress<GenerationProgress>(),
            CancellationToken.None);

        Assert.Contains("class Demo_Dashboard", result.Mermaid);
        Assert.Contains("class Pages_Index", result.Mermaid);
        Assert.DoesNotContain("..>", result.Mermaid);
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
        Assert.Contains("+string Name", domainDiagram);
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
        Assert.DoesNotContain("+string Name", servicesDiagram);
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

    private static void AssertClassBodyLinesDoNotContain(string mermaid, string unexpected)
    {
        var inClassBody = false;
        foreach (var line in mermaid.Split(Environment.NewLine))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("class ", StringComparison.Ordinal) && trimmed.EndsWith("{", StringComparison.Ordinal))
            {
                inClassBody = true;
                continue;
            }

            if (inClassBody && trimmed == "}")
            {
                inClassBody = false;
                continue;
            }

            if (inClassBody && !trimmed.StartsWith("<<", StringComparison.Ordinal))
            {
                Assert.DoesNotContain(unexpected, trimmed);
            }
        }
    }

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var searchIndex = 0;
        while (searchIndex < value.Length)
        {
            var index = value.IndexOf(pattern, searchIndex, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            searchIndex = index + pattern.Length;
        }

        return count;
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
