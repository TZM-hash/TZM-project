using System.Security.Claims;
using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.DataExchange;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class ImportModel(
    IImportService importService,
    IProjectWorkbookService projectWorkbookService,
    IDataExchangeTaskService taskService) : PageModel
{
    public IReadOnlyList<ExportDataset> ImportableDatasets => importService.ImportableDatasets;
    public IReadOnlyList<ImportMappingTemplateDto> MappingTemplates { get; private set; } = [];
    public IReadOnlyList<ProjectWorkbookSheetDefinition> ProjectWorkbookSheets { get; private set; } = [];
    public ImportPreviewDto? Preview { get; private set; }
    public ProjectWorkbookImportPreviewDto? ProjectWorkbookPreview { get; private set; }
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);

    [BindProperty] public IFormFile? ImportFile { get; set; }
    [BindProperty] public ExportDataset ImportDataset { get; set; } = ExportDataset.Employees;
    [BindProperty] public ImportMode ImportMode { get; set; } = ImportMode.Mixed;
    [BindProperty] public string? SourceMappingJson { get; set; }
    [BindProperty] public string? MappingTemplateName { get; set; }
    [BindProperty] public bool SharedMappingTemplate { get; set; }
    [BindProperty] public bool IncludeAttachments { get; set; }
    [BindProperty] public IFormFile? ProjectWorkbookFile { get; set; }
    [BindProperty] public ImportMode ProjectWorkbookImportMode { get; set; } = ImportMode.Mixed;
    [BindProperty] public ProjectWorkbookSheet MappingTargetSheet { get; set; } = ProjectWorkbookSheet.ProjectMaster;
    [BindProperty] public bool BlankMeansNoChange { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

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
        Preview = await importService.PreviewAsync(
            new ImportPreviewRequest(UserId(), ImportDataset, ImportFile.FileName, stream.ToArray(), mapping, ImportMode, IncludeAttachments),
            cancellationToken);
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmImportAsync(Guid batchId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        await importService.ConfirmAsync(batchId, cancellationToken);
        return RedirectToPage();
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
        ProjectWorkbookPreview = await projectWorkbookService.PreviewAsync(
            new ProjectWorkbookImportRequest(
                UserId(),
                ProjectWorkbookFile.FileName,
                stream.ToArray(),
                ProjectWorkbookImportMode,
                ProjectWorkbookFile.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase),
                mappings,
                BlankMeansNoChange,
                WorkbookActor()),
            cancellationToken);
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmProjectWorkbookAsync(Guid batchId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        await projectWorkbookService.ConfirmAsync(WorkbookActor(), batchId, cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetDownloadImportErrorsAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var file = await taskService.DownloadImportErrorsAsync(UserId(), CanManage, batchId, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    public async Task<IActionResult> OnPostSaveMappingTemplateAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var mapping = string.IsNullOrWhiteSpace(SourceMappingJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(SourceMappingJson) ?? [];
        await importService.SaveMappingTemplateAsync(
            new SaveImportMappingTemplateRequest(
                UserId(),
                MappingTemplateName ?? string.Empty,
                ImportDataset,
                SharedMappingTemplate ? ExportTemplateScope.Shared : ExportTemplateScope.Personal,
                "1",
                mapping,
                CanManage),
            cancellationToken);
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        MappingTemplates = await importService.ListMappingTemplatesAsync(UserId(), ImportDataset, cancellationToken);
        ProjectWorkbookSheets = projectWorkbookService.GetSheets();
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("当前用户没有标识。");

    private ProjectWorkbookActor WorkbookActor() =>
        new(UserId(), User.FindAll(ClaimTypes.Role).Select(item => item.Value).Distinct(StringComparer.Ordinal).ToArray());
}
