using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringManager.Web.Pages.Equipment;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.EquipmentManager)]
public sealed class SettlementModel(IEquipmentSettlementService service) : EquipmentPageModel
{
    [BindProperty] public Guid UsageId { get; set; }
    [BindProperty] public DateOnly SettlementDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [BindProperty] public bool GeneratePayable { get; set; }
    [BindProperty] public string Reason { get; set; } = "设备最终结算";
    public EquipmentSettlementDto? Result { get; private set; }
    public void OnGet(Guid usageId) => UsageId = usageId;
    public async Task OnPostAsync(CancellationToken token) => Result = await service.FinalizeAsync(ResolveActor(), new FinalizeEquipmentSettlementRequest(UsageId, SettlementDate, [], GeneratePayable, Reason, null), token);
}
