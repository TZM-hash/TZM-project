using System.Security.Claims;
using EngineeringManager.Application.Backups;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Projects;
using EngineeringManager.Application.Reminders;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Reminders;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EngineeringManager.Tests.Web;

public sealed class DataExchangeBackupAuthorizationTests
{
    [Theory]
    [InlineData("SystemAdministrator")]
    [InlineData("ApplicationAdministrator")]
    [InlineData("Finance")]
    [InlineData("ProjectManager")]
    [InlineData("QueryOnly")]
    public async Task ExportRolesCanOpenDataExchange(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient();

        (await client.GetAsync("/DataExchange")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await client.GetAsync("/Reminders")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("SystemAdministrator")]
    [InlineData("ApplicationAdministrator")]
    public async Task AdministratorsCanOpenBackupPage(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient();

        (await client.GetAsync("/Backups")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Finance")]
    [InlineData("ProjectManager")]
    [InlineData("QueryOnly")]
    [InlineData("SiteStaff")]
    public async Task NonAdministratorsCannotOpenBackupPage(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/Backups")).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public void BackupPageExposesSeparateTypesAndSchedules()
    {
        var page = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages", "Backups", "Index.cshtml"));
        page.Should().Contain("data-backup-settings").And.Contain("data-backup-full").And.Contain("定时策略").And.Contain("NAS/共享目录");
    }

    [Fact]
    public async Task DataExchangeProjectWorkbookShowsCompleteFiltersAndPreviewErrors()
    {
        await using var factory = CreateFactory("SystemAdministrator");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/DataExchange");
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        var page = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages", "DataExchange", "Index.cshtml"));

        html.Should().Contain("name=\"ProjectStages\"")
            .And.Contain("name=\"ProjectLegalEntityId\"")
            .And.Contain("name=\"ProjectResponsibleUserId\"")
            .And.Contain("name=\"SelectedProjectIds\"");
        page.Should().Contain("源行").And.Contain("错误与修正建议");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private static WebApplicationFactory<Program> CreateFactory(string role) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = DataExchangeTestHandler.Scheme;
                    options.DefaultChallengeScheme = DataExchangeTestHandler.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, DataExchangeTestHandler>(DataExchangeTestHandler.Scheme, _ => { });
                services.RemoveAll<IExportService>();
                services.RemoveAll<IImportService>();
                services.RemoveAll<IBackupService>();
                services.RemoveAll<IReminderService>();
                services.RemoveAll<IProjectWorkbookService>();
                services.RemoveAll<IProjectService>();
                services.AddSingleton<IExportService, FakeExportService>();
                services.AddSingleton<IImportService, FakeImportService>();
                services.AddSingleton<IBackupService, FakeBackupService>();
                services.AddSingleton<IReminderService, FakeReminderService>();
                services.AddSingleton<IProjectWorkbookService, FakeProjectWorkbookService>();
                services.AddSingleton<IProjectService, FakeProjectService>();
            });
            builder.UseSetting(DataExchangeTestHandler.RoleSetting, role);
        });

    private sealed class FakeExportService : IExportService
    {
        public IReadOnlyList<EngineeringManager.Domain.DataExchange.ExportFieldDefinition> GetFieldCatalog(ExportDataset dataset) => [];
        public Task<ExportFileResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ExportSelectionDto?> GetLastSelectionAsync(string userId, ExportDataset dataset, CancellationToken cancellationToken) => Task.FromResult<ExportSelectionDto?>(null);
        public Task<ExportTemplateDto> SaveTemplateAsync(SaveExportTemplateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ExportTemplateDto>> ListTemplatesAsync(string userId, ExportDataset dataset, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ExportTemplateDto>>([]);
        public Task<ExportFileResult> ExportModulesAsync(ExportModuleRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ExportTaskDto>> ListTasksAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ExportTaskDto>>([]);
    }

    private sealed class FakeImportService : IImportService
    {
        public Task<ExportFileResult> GenerateTemplateAsync(ExportDataset dataset, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ImportPreviewDto> PreviewAsync(ImportPreviewRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ConfirmAsync(Guid batchId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ImportMappingTemplateDto> SaveMappingTemplateAsync(SaveImportMappingTemplateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ImportMappingTemplateDto>> ListMappingTemplatesAsync(string userId, ExportDataset dataset, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ImportMappingTemplateDto>>([]);
    }

    private sealed class FakeProjectWorkbookService : IProjectWorkbookService
    {
        public IReadOnlyList<ProjectWorkbookSheetDefinition> GetSheets() =>
        [
            new(ProjectWorkbookSheet.ProjectMaster, "项目主档", true, false, [], []),
            new(ProjectWorkbookSheet.ProjectSummary, "项目经营汇总", false, false, [], [])
        ];
        public Task<ExportFileResult> ExportAsync(ProjectWorkbookExportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProjectWorkbookImportPreviewDto> PreviewAsync(ProjectWorkbookImportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ConfirmAsync(ProjectWorkbookActor actor, Guid batchId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeProjectService : IProjectService
    {
        private static readonly Guid ProjectId = Guid.Parse("72000000-0000-0000-0000-000000000001");
        private static readonly Guid CompanyId = Guid.Parse("72000000-0000-0000-0000-000000000002");
        public Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContractDto> AddContractAsync(CreateContractRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContractLineItemDto> AddLineItemAsync(CreateContractLineItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContractLineItemDto> UpdateLineItemAsync(UpdateContractLineItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProjectListItemDto>> ListProjectsAsync(string? search, EngineeringManager.Domain.Projects.ProjectStage? stage, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProjectListItemDto>>([]);
        public Task<ProjectListPageDto> SearchProjectsAsync(ProjectListActor actor, ProjectListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectListPageDto([new ProjectListItemDto(new ProjectDto(ProjectId, "P-DATA", "数据交换项目", null, EngineeringManager.Domain.Projects.ProjectStage.UnderConstruction), new ProjectSummaryDto(0m, 0m, 0m, 0m, EngineeringManager.Domain.Projects.ProjectSettlementStatus.Estimated, 0, 0))], new ProjectListAggregateDto(1, 0m, 0m, 0), 1, 20, 1, 1, [ProjectId]));
        public Task<ProjectListOptionsDto> GetListOptionsAsync(ProjectListActor actor, CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectListOptionsDto([new ProjectFilterOptionDto(CompanyId.ToString(), "签约公司")], [new ProjectFilterOptionDto("manager", "负责人")]));
        public Task<ProjectDetailsDto?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<ProjectDetailsDto?>(null);
    }

    private sealed class FakeBackupService : IBackupService
    {
        public Task<BackupTaskDto> CreateBackupAsync(string requestedByUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BackupTaskDto> CreateBackupAsync(string requestedByUserId, BackupKind kind, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<BackupTaskDto>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BackupTaskDto>>([]);
        public Task<IReadOnlyList<BackupScheduleDto>> ListSchedulesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BackupScheduleDto>>([]);
        public Task<BackupScheduleDto> SaveScheduleAsync(SaveBackupScheduleRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<BackupTaskDto>> RunDueSchedulesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BackupTaskDto>>([]);
        public Task<SettingsRestorePreviewDto> PreviewSettingsAsync(byte[] content, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RestoreSettingsAsync(byte[] content, IReadOnlyCollection<string> categories, string requestedByUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeReminderService : IReminderService
    {
        public Task RefreshAsync(DateOnly today, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ReminderDto>> ListAsync(bool includeResolved, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReminderDto>>([]);
        public Task MarkReadAsync(Guid reminderId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResolveAsync(Guid reminderId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class DataExchangeTestHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "DataExchangeTest";
        public const string RoleSetting = "DataExchangeTestAuth:Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Context.RequestServices.GetRequiredService<IConfiguration>()[RoleSetting];
            var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "data-exchange-user"));
            identity.AddClaim(new Claim(ClaimTypes.Name, "数据交换测试用户"));
            identity.AddClaim(new Claim(ClaimTypes.Role, role!));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
