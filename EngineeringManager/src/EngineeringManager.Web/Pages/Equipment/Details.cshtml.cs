using EngineeringManager.Application.Companies;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Equipment;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly + "," + SystemRoles.EquipmentManager)]
public sealed class DetailsModel(IEquipmentService service, ICompanyManagementService companyService, IBusinessPartnerService partnerService) : EquipmentPageModel
{
    public EquipmentDetailsDto Equipment { get; private set; } = null!;
    public IReadOnlyList<CompanyListItemDto> Companies { get; private set; } = [];
    public IReadOnlyList<BusinessPartnerDto> Lessors { get; private set; } = [];
    public bool CanManage => ResolveActor().CanManage;
    public bool QuickEditOpen { get; private set; }
    [BindProperty] public EditModel.InputModel QuickEdit { get; set; } = new();

    public async Task OnGetAsync(Guid id, CancellationToken token) => await LoadAsync(id, true, token);

    public async Task<IActionResult> OnPostQuickEditAsync(Guid id, CancellationToken token)
    {
        if (!CanManage) return Forbid();
        QuickEdit.Id = id;
        if (!ModelState.IsValid)
        {
            QuickEditOpen = true;
            await LoadAsync(id, false, token);
            return Page();
        }
        try
        {
            await service.SaveEquipmentAsync(ResolveActor(), QuickEdit.ToRequest(), token);
            return RedirectToPage(new { id });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            QuickEditOpen = true;
            await LoadAsync(id, false, token);
            return Page();
        }
    }

    private async Task LoadAsync(Guid id, bool populateQuickEdit, CancellationToken token)
    {
        var equipmentActor = ResolveActor();
        var dashboard = await service.GetDashboardAsync(equipmentActor, new EquipmentFilter(null, null, null, null), token);
        Equipment = dashboard.Items.SingleOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("设备不存在或无权访问。");
        if (!equipmentActor.CanManage) return;
        Companies = await companyService.ListAsync(new CompanyActor(equipmentActor.UserId, false, equipmentActor.CanAccessAll, equipmentActor.AccessibleCompanyIds), token);
        Lessors = await partnerService.ListAsync(null, null, token);
        if (populateQuickEdit) QuickEdit = EditModel.InputModel.From(Equipment);
    }
}
