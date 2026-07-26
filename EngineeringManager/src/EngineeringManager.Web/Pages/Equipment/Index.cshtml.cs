using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Companies;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Application.Partners;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Equipment;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.SiteStaff + "," + SystemRoles.QueryOnly + "," + SystemRoles.EquipmentManager)]
public sealed class IndexModel(
    IEquipmentService service,
    ICompanyManagementService companyService,
    IBusinessPartnerService partnerService,
    IProjectService projectService) : EquipmentPageModel
{
    public EquipmentDashboardDto Dashboard { get; private set; } = new(0, 0, 0, 0, 0, new Dictionary<string, int>(), []);
    public IReadOnlyList<CompanyListItemDto> Companies { get; private set; } = [];
    public IReadOnlyList<BusinessPartnerDto> Lessors { get; private set; } = [];
    public IReadOnlyList<ProjectListItemDto> Projects { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public EquipmentStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CompanyId { get; set; }
    [BindProperty(SupportsGet = true)] public bool Unassigned { get; set; }
    [BindProperty] public EquipmentEditorInput Editor { get; set; } = new();
    [BindProperty] public IFormFile? QualificationAttachmentFile { get; set; }
    [BindProperty] public UsageModel.InputModel UsageInput { get; set; } = new();
    public string? ActiveDialog { get; private set; }
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.EquipmentManager);

    public async Task OnGetAsync(CancellationToken token) => await LoadAsync(token);

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken token)
    {
        if (!CanManage) return Forbid();
        RemoveModelStatePrefix(nameof(UsageInput));
        if (!ModelState.IsValid)
        {
            ActiveDialog = "editor";
            await LoadAsync(token);
            return Page();
        }

        try
        {
            CertificateAttachmentUpload? upload = null;
            if (QualificationAttachmentFile is not null)
            {
                if (QualificationAttachmentFile.Length is <= 0 or > CertificateAttachmentUpload.MaxSizeBytes)
                    throw new InvalidOperationException("证书附件不能为空且不能超过 20MB。");
                await using var buffer = new MemoryStream();
                await QualificationAttachmentFile.CopyToAsync(buffer, token);
                upload = new CertificateAttachmentUpload(
                    QualificationAttachmentFile.FileName,
                    QualificationAttachmentFile.ContentType,
                    buffer.ToArray());
            }
            await service.SaveEquipmentAsync(ResolveActor(), Editor.ToRequest(upload), token);
            TempData["SuccessMessage"] = Editor.Id.HasValue ? "设备档案已更新。" : "设备已新增。";
            return RedirectToPage(new { CompanyId, Unassigned, Keyword, Status });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            ActiveDialog = "editor";
            await LoadAsync(token);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUsageAsync(CancellationToken token)
    {
        RemoveModelStatePrefix(nameof(Editor));
        if (!ModelState.IsValid)
        {
            ActiveDialog = "usage";
            await LoadAsync(token);
            return Page();
        }
        try
        {
            await service.SaveUsageAsync(ResolveActor(), UsageInput.ToRequest(), token);
            TempData["SuccessMessage"] = "设备进退场记录已保存。";
            return RedirectToPage(new { CompanyId, Unassigned, Keyword, Status });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            ActiveDialog = "usage";
            await LoadAsync(token);
            return Page();
        }
    }

    public async Task<IActionResult> OnGetQualificationAttachmentAsync(Guid equipmentId, CancellationToken token)
    {
        try
        {
            var file = await service.DownloadQualificationAttachmentAsync(ResolveActor(), equipmentId, token);
            return File(file.Content, file.ContentType, file.OriginalFileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task LoadAsync(CancellationToken token)
    {
        var actor = ResolveActor();
        Dashboard = await service.GetDashboardAsync(actor, new EquipmentFilter(CompanyId, null, Status, Keyword, Unassigned), token);
        Companies = (await companyService.ListAsync(new CompanyActor(actor.UserId, false, actor.CanAccessAll, actor.AccessibleCompanyIds), token))
            .Where(item => item.IsActive)
            .ToArray();
        Lessors = await partnerService.ListAsync(null, null, token);
        Projects = await projectService.ListProjectsAsync(null, null, token);
    }

    private void RemoveModelStatePrefix(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(key => key.Equals(prefix, StringComparison.Ordinal) || key.StartsWith(prefix + ".", StringComparison.Ordinal)).ToArray())
            ModelState.Remove(key);
    }
}
