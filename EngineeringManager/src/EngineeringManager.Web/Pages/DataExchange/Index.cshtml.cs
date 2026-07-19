using System.Security.Claims;
using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Security;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.DataExchange;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IExportService exportService, IImportService importService, IProjectWorkbookService projectWorkbookService, IProjectService projectService) : PageModel
{
    public IReadOnlyList<EngineeringManager.Domain.DataExchange.ExportFieldDefinition> Fields { get; private set; } = [];
    public IReadOnlyList<ExportTemplateDto> Templates { get; private set; } = [];
    public IReadOnlyList<ExportTaskDto> Tasks { get; private set; } = [];
    public IReadOnlyList<ImportMappingTemplateDto> MappingTemplates { get; private set; } = [];
    public ImportPreviewDto? Preview { get; private set; }
    public IReadOnlyList<ProjectWorkbookSheetDefinition> ProjectWorkbookSheets { get; private set; } = [];
    public ProjectWorkbookImportPreviewDto? ProjectWorkbookPreview { get; private set; }
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    public bool CanExportProjectWorkbook => WorkbookActor().CanExport;
    public bool CanExportFullProjectWorkbook => WorkbookActor().CanExportFullWorkbook;
    public bool CanExportProjectWorkbookAttachments => WorkbookActor().CanExportAttachments;
    public ProjectListPageDto ProjectWorkbookProjects { get; private set; } = new([], new ProjectListAggregateDto(0, 0m, 0m, 0), 1, 100, 0, 1, []);
    public ProjectListOptionsDto ProjectWorkbookOptions { get; private set; } = new([], []);

    [BindProperty] public ExportDataset Dataset { get; set; } = ExportDataset.ProjectOverview;
    [BindProperty] public List<string> SelectedFields { get; set; } = [];
    [BindProperty] public DateOnly? CutoffDate { get; set; }
    [BindProperty] public string? TemplateName { get; set; }
    [BindProperty] public bool SharedTemplate { get; set; }
    [BindProperty] public IFormFile? ImportFile { get; set; }
    [BindProperty] public ExportDataset ImportDataset { get; set; } = ExportDataset.Employees;
    [BindProperty] public List<ExportDataset> SelectedDatasets { get; set; } = [];
    [BindProperty] public ExportScope Scope { get; set; } = ExportScope.CurrentView;
    [BindProperty] public ExportPackageFormat PackageFormat { get; set; } = ExportPackageFormat.Workbook;
    [BindProperty] public bool IncludeAttachments { get; set; }
    [BindProperty] public ImportMode ImportMode { get; set; } = ImportMode.Mixed;
    [BindProperty] public string? SourceMappingJson { get; set; }
    [BindProperty] public string? MappingTemplateName { get; set; }
    [BindProperty] public bool SharedMappingTemplate { get; set; }
    [BindProperty(SupportsGet = true)] public string? ProjectSearch { get; set; }
    [BindProperty(SupportsGet = true)] public List<ProjectStage> ProjectStages { get; set; } = [];
    [BindProperty(SupportsGet = true)] public Guid? ProjectLegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public string? ProjectResponsibleUserId { get; set; }
    [BindProperty(SupportsGet = true)] public ProjectAffiliationType? ProjectAffiliationType { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? ProjectMinimumCurrentAmount { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? ProjectMaximumCurrentAmount { get; set; }
    [BindProperty] public List<Guid> SelectedProjectIds { get; set; } = [];
    [BindProperty] public bool SelectAllMatchingProjects { get; set; }
    [BindProperty] public List<ProjectWorkbookSheet> SelectedProjectSheets { get; set; } = [];
    [BindProperty] public bool IncludeProjectAttachments { get; set; }
    [BindProperty] public IFormFile? ProjectWorkbookFile { get; set; }
    [BindProperty] public ImportMode ProjectWorkbookImportMode { get; set; } = ImportMode.Mixed;
    [BindProperty] public ProjectWorkbookSheet MappingTargetSheet { get; set; } = ProjectWorkbookSheet.ProjectMaster;
    [BindProperty] public bool BlankMeansNoChange { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostExportAsync(CancellationToken cancellationToken)
    {
        var result = await exportService.ExportAsync(new ExportRequest(Dataset, UserId(), SelectedFields, CutoffDate, CanViewSensitiveData: CanManage), cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    public async Task<IActionResult> OnPostExportModulesAsync(CancellationToken cancellationToken)
    {
        var datasets = SelectedDatasets.Count == 0 ? [Dataset] : SelectedDatasets.Distinct().ToArray();
        var selections = datasets.ToDictionary(
            dataset => dataset,
            dataset => (IReadOnlyList<string>)(dataset == Dataset
                ? SelectedFields
                : exportService.GetFieldCatalog(dataset).Where(field => field.IsDefault).Select(field => field.Key).ToArray()));
        var result = await exportService.ExportModulesAsync(
            new ExportModuleRequest(datasets, UserId(), selections, null, PackageFormat, IncludeAttachments, CanManage),
            cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    public async Task<IActionResult> OnPostExportProjectWorkbookAsync(CancellationToken cancellationToken)
    {
        if (!CanExportProjectWorkbook) return Forbid();
        if (SelectedProjectIds.Count > 0) SelectAllMatchingProjects = false;
        if (!SelectAllMatchingProjects && SelectedProjectIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "请至少勾选一个项目，或选择导出筛选命中的全部项目。");
            await LoadAsync(cancellationToken);
            return Page();
        }
        var query = new ProjectListQuery(ProjectSearch, ProjectStages, ProjectLegalEntityId, ProjectResponsibleUserId, ProjectMinimumCurrentAmount, ProjectMaximumCurrentAmount, null, false, AffiliationType: ProjectAffiliationType, IncludeInactive: true);
        var file = await projectWorkbookService.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(ProjectActor(), query, SelectAllMatchingProjects, SelectedProjectIds),
            SelectedProjectSheets,
            IncludeAttachments: IncludeProjectAttachments,
            Actor: WorkbookActor()), cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    public async Task<IActionResult> OnPostPreviewProjectWorkbookAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (ProjectWorkbookFile is null || ProjectWorkbookFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "请选择项目工作簿或 ZIP。");
            await LoadAsync(cancellationToken);
            return Page();
        }
        await using var stream = new MemoryStream();
        await ProjectWorkbookFile.CopyToAsync(stream, cancellationToken);
        IReadOnlyDictionary<ProjectWorkbookSheet, IReadOnlyDictionary<string, string>>? mappings = null;
        if (!string.IsNullOrWhiteSpace(SourceMappingJson))
        {
            var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(SourceMappingJson) ?? [];
            mappings = new Dictionary<ProjectWorkbookSheet, IReadOnlyDictionary<string, string>> { [MappingTargetSheet] = mapping };
        }
        ProjectWorkbookPreview = await projectWorkbookService.PreviewAsync(new ProjectWorkbookImportRequest(
            UserId(), ProjectWorkbookFile.FileName, stream.ToArray(), ProjectWorkbookImportMode,
            ProjectWorkbookFile.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase), mappings, BlankMeansNoChange, WorkbookActor()), cancellationToken);
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmProjectWorkbookAsync(Guid batchId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        await projectWorkbookService.ConfirmAsync(WorkbookActor(), batchId, cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveTemplateAsync(CancellationToken cancellationToken)
    {
        await exportService.SaveTemplateAsync(
            new SaveExportTemplateRequest(UserId(), TemplateName ?? string.Empty, Dataset, SharedTemplate ? ExportTemplateScope.Shared : ExportTemplateScope.Personal, SelectedFields, CutoffDate, CanManage),
            cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDownloadTemplateAsync(CancellationToken cancellationToken)
    {
        var result = await importService.GenerateTemplateAsync(ImportDataset, cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    public async Task<IActionResult> OnPostPreviewImportAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (ImportFile is null || ImportFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "请选择导入文件。");
            await LoadAsync(cancellationToken);
            return Page();
        }

        await using var stream = new MemoryStream();
        await ImportFile.CopyToAsync(stream, cancellationToken);
        IReadOnlyDictionary<string, string>? mapping = null;
        if (!string.IsNullOrWhiteSpace(SourceMappingJson))
        {
            mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(SourceMappingJson);
        }
        Preview = await importService.PreviewAsync(new ImportPreviewRequest(UserId(), ImportDataset, ImportFile.FileName, stream.ToArray(), mapping, ImportMode, IncludeAttachments), cancellationToken);
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmImportAsync(Guid batchId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        await importService.ConfirmAsync(batchId, cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveMappingTemplateAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var mapping = string.IsNullOrWhiteSpace(SourceMappingJson) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(SourceMappingJson) ?? [];
        await importService.SaveMappingTemplateAsync(new SaveImportMappingTemplateRequest(UserId(), MappingTemplateName ?? string.Empty, ImportDataset, SharedMappingTemplate ? ExportTemplateScope.Shared : ExportTemplateScope.Personal, "1", mapping, CanManage), cancellationToken);
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Fields = exportService.GetFieldCatalog(Dataset);
        Templates = await exportService.ListTemplatesAsync(UserId(), Dataset, cancellationToken);
        Tasks = await exportService.ListTasksAsync(UserId(), cancellationToken);
        MappingTemplates = await importService.ListMappingTemplatesAsync(UserId(), ImportDataset, cancellationToken);
        ProjectWorkbookSheets = projectWorkbookService.GetSheets();
        var projectActor = ProjectActor();
        var projectQuery = new ProjectListQuery(ProjectSearch, ProjectStages, ProjectLegalEntityId, ProjectResponsibleUserId, ProjectMinimumCurrentAmount, ProjectMaximumCurrentAmount, null, false, PageSize: 100, AffiliationType: ProjectAffiliationType, IncludeInactive: true);
        ProjectWorkbookProjects = await projectService.SearchProjectsAsync(projectActor, projectQuery, cancellationToken);
        ProjectWorkbookOptions = await projectService.GetListOptionsAsync(projectActor, cancellationToken);
        var last = await exportService.GetLastSelectionAsync(UserId(), Dataset, cancellationToken);
        if (SelectedFields.Count == 0 && last is not null)
        {
            SelectedFields = last.SelectedFields.ToList();
            CutoffDate ??= last.CutoffDate;
        }
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("当前用户没有标识。");

    private ProjectListActor ProjectActor()
    {
        var canAccessAll = User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance) || User.IsInRole(SystemRoles.QueryOnly) || User.IsInRole(SystemRoles.EquipmentManager);
        return new ProjectListActor(UserId(), canAccessAll);
    }

    private ProjectWorkbookActor WorkbookActor() =>
        new(UserId(), User.FindAll(ClaimTypes.Role).Select(item => item.Value).Distinct(StringComparer.Ordinal).ToArray());
}
