using System.ComponentModel.DataAnnotations;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringManager.Web.Pages.Equipment;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.EquipmentManager + "," + SystemRoles.SiteStaff)]
public sealed class UsageModel(IEquipmentService service) : EquipmentPageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public void OnGet(Guid equipmentId) => Input.EquipmentId = equipmentId;
    public async Task<IActionResult> OnPostAsync(CancellationToken token)
    {
        if(!ModelState.IsValid) return Page();
        await service.SaveUsageAsync(ResolveActor(), Input.ToRequest(), token);
        return RedirectToPage("Details", new { id = Input.EquipmentId });
    }
    public sealed class InputModel
    {
        public Guid EquipmentId { get; set; }
        public Guid ProjectId { get; set; }
        public Guid LegalEntityId { get; set; }
        [Required] public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public DateOnly? ExitDate { get; set; }
        public RentMode RentMode { get; set; } = RentMode.Daily;
        public decimal UnitRate { get; set; }
        public string Reason { get; set; } = "登记设备进退场";
        public SaveEquipmentUsageRequest ToRequest() => new(null, EquipmentId, ProjectId, LegalEntityId, null, EntryDate, ExitDate, RentMode, MonthlyProrationMode.ThirtyDay, UnitRate, false, null, ExitDate.HasValue ? [new EquipmentPeriodRequest(EntryDate, ExitDate.Value, EquipmentPeriodType.Work, true, null)] : [], null, Reason);
    }
}
