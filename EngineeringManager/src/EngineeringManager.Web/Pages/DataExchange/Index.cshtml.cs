using System.Security.Claims;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.DataExchange;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IExportService exportService, IImportService importService) : PageModel
{
    public IReadOnlyList<EngineeringManager.Domain.DataExchange.ExportFieldDefinition> Fields { get; private set; } = [];
    public IReadOnlyList<ExportTemplateDto> Templates { get; private set; } = [];
    public ImportPreviewDto? Preview { get; private set; }
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);

    [BindProperty] public ExportDataset Dataset { get; set; } = ExportDataset.ProjectOverview;
    [BindProperty] public List<string> SelectedFields { get; set; } = [];
    [BindProperty] public DateOnly? CutoffDate { get; set; }
    [BindProperty] public string? TemplateName { get; set; }
    [BindProperty] public bool SharedTemplate { get; set; }
    [BindProperty] public IFormFile? ImportFile { get; set; }
    [BindProperty] public ExportDataset ImportDataset { get; set; } = ExportDataset.Employees;

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostExportAsync(CancellationToken cancellationToken)
    {
        var result = await exportService.ExportAsync(new ExportRequest(Dataset, UserId(), SelectedFields, CutoffDate), cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
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
        Preview = await importService.PreviewAsync(new ImportPreviewRequest(UserId(), ImportDataset, ImportFile.FileName, stream.ToArray(), null), cancellationToken);
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmImportAsync(Guid batchId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        await importService.ConfirmAsync(batchId, cancellationToken);
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Fields = exportService.GetFieldCatalog(Dataset);
        Templates = await exportService.ListTemplatesAsync(UserId(), Dataset, cancellationToken);
        var last = await exportService.GetLastSelectionAsync(UserId(), Dataset, cancellationToken);
        if (SelectedFields.Count == 0 && last is not null)
        {
            SelectedFields = last.SelectedFields.ToList();
            CutoffDate ??= last.CutoffDate;
        }
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("当前用户没有标识。");
}
