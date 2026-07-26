using System.ComponentModel.DataAnnotations;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringManager.Web.Pages.Equipment;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.EquipmentManager)]
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
        public Guid? Id { get; set; }
        public Guid EquipmentId { get; set; }
        public Guid ProjectId { get; set; }
        public Guid LegalEntityId { get; set; }
        [Required] public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public DateOnly? ExitDate { get; set; }
        public RentMode RentMode { get; set; } = RentMode.Daily;
        public decimal UnitRate { get; set; }
        public Guid? ConcurrencyStamp { get; set; }
        public List<PeriodInputModel> Periods { get; set; } = [];
        public string Reason { get; set; } = "登记设备进退场";
        public SaveEquipmentUsageRequest ToRequest()
        {
            IReadOnlyCollection<EquipmentPeriodRequest> periods = Periods.Count > 0
                ? Periods.Select(item => item.ToRequest()).ToArray()
                : ExitDate.HasValue
                    ? [new EquipmentPeriodRequest(EntryDate, ExitDate.Value, EquipmentPeriodType.Work, true, null)]
                    : [];
            return new SaveEquipmentUsageRequest(Id, EquipmentId, ProjectId, LegalEntityId, null, EntryDate, ExitDate, RentMode, MonthlyProrationMode.ThirtyDay, UnitRate, false, null, periods, ConcurrencyStamp, Reason);
        }
    }

    public sealed class PeriodInputModel
    {
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public EquipmentPeriodType PeriodType { get; set; }
        public bool IsChargeable { get; set; }
        public string? Notes { get; set; }
        public EquipmentPeriodRequest ToRequest() => new(StartDate, EndDate, PeriodType, IsChargeable, Notes);
    }
}
