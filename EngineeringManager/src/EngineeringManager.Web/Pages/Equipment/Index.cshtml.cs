using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Companies;
using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Application.Partners;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Security;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Equipment;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.SiteStaff + "," + SystemRoles.QueryOnly + "," + SystemRoles.EquipmentManager)]
public sealed class IndexModel(
    IEquipmentService service,
    ICompanyManagementService companyService,
    IBusinessPartnerService partnerService,
    IProjectService projectService,
    IBusinessYearService businessYearService) : EquipmentPageModel
{
    public EquipmentDashboardDto Dashboard { get; private set; } = new(0, 0, 0, 0, 0, new Dictionary<string, int>(), []);
    public EquipmentDashboardDto PortfolioDashboard { get; private set; } = new(0, 0, 0, 0, 0, new Dictionary<string, int>(), []);
    public IReadOnlyList<CompanyListItemDto> Companies { get; private set; } = [];
    public IReadOnlyList<CompanyEquipmentSummary> CompanySummaries { get; private set; } = [];
    public IReadOnlyList<BusinessPartnerDto> Lessors { get; private set; } = [];
    public IReadOnlyList<ProjectListItemDto> Projects { get; private set; } = [];
    public IReadOnlyList<BusinessYearDto> BusinessYears { get; private set; } = [];
    public BusinessYearDto SelectedBusinessYear { get; private set; } = null!;
    public IReadOnlyList<EquipmentUsageHistoryDto> UsageHistory { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public EquipmentStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CompanyId { get; set; }
    [BindProperty(SupportsGet = true)] public bool Unassigned { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? BusinessYearId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? OpenUsageEquipmentId { get; set; }
    [BindProperty] public EquipmentEditorInput Editor { get; set; } = new();
    [BindProperty] public IFormFile? QualificationAttachmentFile { get; set; }
    [BindProperty] public UsageModel.InputModel UsageInput { get; set; } = new();
    [BindProperty] public DeleteEquipmentInput DeleteInput { get; set; } = new();
    public string? ActiveDialog { get; private set; }
    public bool UsageEditorOpen { get; private set; }
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.EquipmentManager);

    public async Task OnGetAsync(CancellationToken token) => await LoadAsync(token);

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken token)
    {
        if (!CanManage) return Forbid();
        RemoveModelStatePrefix(nameof(UsageInput));
        RemoveModelStatePrefix(nameof(DeleteInput));
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
            return RedirectToPage(new { CompanyId, Unassigned, Keyword, Status, BusinessYearId });
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
        if (!CanManage) return Forbid();
        RemoveModelStatePrefix(nameof(Editor));
        RemoveModelStatePrefix(nameof(DeleteInput));
        if (!ModelState.IsValid)
        {
            ActiveDialog = "usage";
            UsageEditorOpen = true;
            OpenUsageEquipmentId = UsageInput.EquipmentId;
            await LoadAsync(token);
            return Page();
        }
        try
        {
            await service.SaveUsageAsync(ResolveActor(), UsageInput.ToRequest(), token);
            TempData["SuccessMessage"] = "设备进退场记录已保存。";
            return RedirectToPage(new { CompanyId, Unassigned, Keyword, Status, BusinessYearId });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            ActiveDialog = "usage";
            UsageEditorOpen = true;
            OpenUsageEquipmentId = UsageInput.EquipmentId;
            await LoadAsync(token);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken token)
    {
        if (!CanManage) return Forbid();
        RemoveModelStatePrefix(nameof(Editor));
        RemoveModelStatePrefix(nameof(UsageInput));
        if (!ModelState.IsValid)
        {
            ActiveDialog = "delete";
            await LoadAsync(token);
            return Page();
        }
        try
        {
            await service.DeleteEquipmentAsync(
                ResolveActor(),
                DeleteInput.Id,
                DeleteInput.ConcurrencyStamp,
                DeleteInput.ConfirmationNumber,
                DeleteInput.Reason,
                token);
            TempData["SuccessMessage"] = "设备已删除。";
            return RedirectToPage(new { CompanyId, Keyword, Status, BusinessYearId });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            ActiveDialog = "delete";
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
        var accessibleCompanies = await companyService.ListAsync(new CompanyActor(actor.UserId, false, actor.CanAccessAll, actor.AccessibleCompanyIds), token);
        Companies = accessibleCompanies.Where(item => item.IsActive).ToArray();
        if (CompanyId.HasValue && Companies.All(item => item.Id != CompanyId.Value)) CompanyId = null;
        Unassigned = false;
        Dashboard = await service.GetDashboardAsync(actor, new EquipmentFilter(CompanyId, null, Status, Keyword), token);
        PortfolioDashboard = await service.GetDashboardAsync(actor, new EquipmentFilter(null, null, null, null), token);
        CompanySummaries = Companies
            .Select(company =>
            {
                var items = PortfolioDashboard.Items.Where(item => item.ManagingLegalEntityId == company.Id).ToArray();
                return new CompanyEquipmentSummary(
                    company.Id,
                    company.ShortName,
                    items.Length,
                    items.Count(item => item.OwnershipType == EquipmentOwnershipType.SelfOwned),
                    items.Count(item => item.OwnershipType == EquipmentOwnershipType.Rented),
                    items.Count(item => item.OwnershipType == EquipmentOwnershipType.Other));
            })
            .ToArray();
        Lessors = await partnerService.ListAsync(null, null, token);
        Projects = await projectService.ListProjectsAsync(null, null, token);
        var today = DateOnly.FromDateTime(DateTime.Today);
        BusinessYears = await businessYearService.ListAsync(token);
        SelectedBusinessYear = (BusinessYearId.HasValue ? BusinessYears.FirstOrDefault(item => item.Id == BusinessYearId) : null)
            ?? await businessYearService.GetByDateAsync(today, token)
            ?? (BusinessYears.Count > 0 ? BusinessYears[0] : null)
            ?? new BusinessYearDto(Guid.Empty, $"{today.Year} 业务年", new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31), Guid.Empty);
        if (BusinessYears.Count == 0) BusinessYears = [SelectedBusinessYear];
        BusinessYearId = SelectedBusinessYear.Id == Guid.Empty ? null : SelectedBusinessYear.Id;
        UsageHistory = OpenUsageEquipmentId.HasValue
            ? await service.ListUsagesAsync(actor, new EquipmentUsageFilter(OpenUsageEquipmentId, SelectedBusinessYear.StartDate, SelectedBusinessYear.EndDate), token)
            : [];
        if (OpenUsageEquipmentId.HasValue) ActiveDialog = "usage";
    }

    private void RemoveModelStatePrefix(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(key => key.Equals(prefix, StringComparison.Ordinal) || key.StartsWith(prefix + ".", StringComparison.Ordinal)).ToArray())
            ModelState.Remove(key);
    }

    public sealed record CompanyEquipmentSummary(
        Guid? CompanyId,
        string CompanyName,
        int TotalCount,
        int SelfOwnedCount,
        int RentedCount,
        int OtherCount);

    public sealed class DeleteEquipmentInput
    {
        public Guid Id { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        public string EquipmentNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "请输入设备编号以确认删除。")]
        public string ConfirmationNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "请填写删除原因。")]
        public string Reason { get; set; } = "删除设备档案";
    }
}
