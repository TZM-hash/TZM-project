using System.ComponentModel.DataAnnotations;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringManager.Web.Pages.Equipment;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.EquipmentManager)]
public sealed class EditModel(IEquipmentService service) : EquipmentPageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public async Task OnGetAsync(Guid? copyFrom, CancellationToken token)
    {
        if (copyFrom.HasValue) Input = InputModel.From(await service.CopyEquipmentAsync(ResolveActor(), copyFrom.Value, token));
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken token)
    {
        if(!ModelState.IsValid) return Page();
        var saved = await service.SaveEquipmentAsync(ResolveActor(), Input.ToRequest(), token);
        return RedirectToPage("Details", new { id = saved.Id });
    }
    public sealed class InputModel
    {
        public Guid? Id { get; set; }
        [Required] public string EquipmentNumber { get; set; } = string.Empty;
        [Required] public string Name { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? Category { get; set; }
        public EquipmentOwnershipType OwnershipType { get; set; } = EquipmentOwnershipType.SelfOwned;
        public Guid? OwnerLegalEntityId { get; set; }
        public Guid? LessorBusinessPartnerId { get; set; }
        public decimal? InternalDailyRate { get; set; }
        public Guid? ConcurrencyStamp { get; set; }
        [Required] public string Reason { get; set; } = "维护设备档案";
        public SaveEquipmentRequest ToRequest() => new(Id, EquipmentNumber, Name, Model, Category, OwnershipType, OwnerLegalEntityId, LessorBusinessPartnerId, InternalDailyRate, ConcurrencyStamp, Reason);
        public static InputModel From(EquipmentDetailsDto item) => new() { EquipmentNumber = item.EquipmentNumber, Name = item.Name, Model = item.Model, Category = item.Category, OwnershipType = item.OwnershipType, OwnerLegalEntityId = item.OwnerLegalEntityId, LessorBusinessPartnerId = item.LessorBusinessPartnerId, InternalDailyRate = item.InternalDailyRate };
    }
}
