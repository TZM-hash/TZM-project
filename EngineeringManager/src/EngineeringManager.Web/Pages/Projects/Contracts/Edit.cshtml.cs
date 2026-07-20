using System.Security.Claims;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Projects.Contracts;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.ProjectManager)]
public sealed class EditModel(IProjectService projectService, IProjectWorkspaceService workspaceService, IProjectRecordAttachmentService attachmentService) : PageModel
{
    public ProjectWorkspaceDto? Workspace { get; private set; }
    public IReadOnlyList<ProjectListItemDto> Projects { get; private set; } = [];

    [BindProperty(SupportsGet = true)] public Guid? ProjectId { get; set; }
    [BindProperty] public string ContractNumber { get; set; } = string.Empty;
    [BindProperty] public string ContractName { get; set; } = string.Empty;
    [BindProperty] public ContractType ContractType { get; set; } = ContractType.MainContract;
    [BindProperty] public string? CounterpartyName { get; set; }
    [BindProperty] public decimal ContractAmount { get; set; }
    [BindProperty] public Guid? ContractLegalEntityId { get; set; }
    [BindProperty] public Guid? ContractId { get; set; }
    [BindProperty] public string LineCode { get; set; } = string.Empty;
    [BindProperty] public string LineName { get; set; } = string.Empty;
    [BindProperty] public string Unit { get; set; } = string.Empty;
    [BindProperty] public decimal? Quantity { get; set; }
    [BindProperty] public decimal? UnitPrice { get; set; }
    [BindProperty] public string? AccountingLabel { get; set; }
    [BindProperty] public bool RequiresInvoice { get; set; } = true;
    [BindProperty] public Guid AttachmentLineItemId { get; set; }
    [BindProperty] public IFormFile? AttachmentFile { get; set; }
    [BindProperty] public string? AttachmentDescription { get; set; }
    public IReadOnlyDictionary<Guid, IReadOnlyList<ProjectRecordAttachmentDto>> Attachments { get; private set; } = new Dictionary<Guid, IReadOnlyList<ProjectRecordAttachmentDto>>();
    [BindProperty] public string? ContractNotes { get; set; }
    [BindProperty] public string? LineNotes { get; set; }

    public async Task OnGetAsync(CancellationToken token) => await LoadAsync(token);

    public async Task<IActionResult> OnPostContractAsync(CancellationToken token)
    {
        if (!ProjectId.HasValue) return RedirectToPage();
        try
        {
            var legalEntityId = ContractLegalEntityId ?? throw new ArgumentException("请选择我方签约公司。");
            await projectService.AddContractAsync(new CreateContractRequest(ProjectId.Value, ContractNumber, ContractName, ContractType,
                ContractAllocationMode.SingleCompany, CounterpartyName, ContractAmount,
                [new ContractAllocationRequest(legalEntityId, ContractAmount, null)], ContractNotes), token);
            return RedirectToPage(new { projectId = ProjectId.Value });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(token);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostLineAsync(CancellationToken token)
    {
        if (!ProjectId.HasValue || !ContractId.HasValue) return RedirectToPage(new { projectId = ProjectId });
        try
        {
            await projectService.AddLineItemAsync(new CreateContractLineItemRequest(ContractId.Value, LineCode, LineName, Unit,
                null, null, null, null, false, LineNotes, Quantity, UnitPrice, AccountingLabel, RequiresInvoice), token);
            return RedirectToPage(new { projectId = ProjectId.Value });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(token);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAttachmentAsync(CancellationToken token)
    {
        if (!ProjectId.HasValue || AttachmentFile is null) return RedirectToPage(new { projectId = ProjectId });
        await using var stream = AttachmentFile.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, token);
        await attachmentService.UploadAsync(new ProjectRecordAttachmentActor(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, true),
            new ProjectRecordAttachmentUpload(ProjectId.Value, ProjectRecordAttachmentType.Quantity, AttachmentLineItemId, AttachmentFile.FileName, AttachmentFile.ContentType, buffer.ToArray(), AttachmentDescription), token);
        return RedirectToPage(new { projectId = ProjectId.Value });
    }

    public async Task<IActionResult> OnGetAttachmentAsync(Guid projectId, Guid attachmentId, CancellationToken token)
    {
        var file = await attachmentService.DownloadAsync(projectId, attachmentId, token);
        return File(file.Content, file.ContentType, file.OriginalFileName);
    }

    public async Task<IActionResult> OnPostDeleteAttachmentAsync(Guid attachmentId, CancellationToken token)
    {
        if (!ProjectId.HasValue) return RedirectToPage();
        await attachmentService.DeleteAsync(new ProjectRecordAttachmentActor(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, true), ProjectId.Value, attachmentId, token);
        return RedirectToPage(new { projectId = ProjectId.Value });
    }

    private async Task LoadAsync(CancellationToken token)
    {
        Projects = await projectService.ListProjectsAsync(null, null, token);
        Workspace = ProjectId.HasValue ? await workspaceService.GetAsync(ProjectId.Value, token) : null;
        if (Workspace is not null)
        {
            var pairs = new Dictionary<Guid, IReadOnlyList<ProjectRecordAttachmentDto>>();
            foreach (var line in Workspace.Contracts.SelectMany(item => item.LineItems))
                pairs[line.Id] = await attachmentService.ListAsync(Workspace.Overview.Id, ProjectRecordAttachmentType.Quantity, line.Id, token);
            Attachments = pairs;
        }
        if (Workspace is not null && !ContractLegalEntityId.HasValue)
            ContractLegalEntityId = Workspace.Overview.LegalEntities.Select(item => Guid.TryParse(item.Value, out var id) ? id : Guid.Empty).FirstOrDefault(item => item != Guid.Empty);
    }
}
