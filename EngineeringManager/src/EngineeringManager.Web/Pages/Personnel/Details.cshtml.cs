using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EngineeringManager.Application.Personnel;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Personnel;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class DetailsModel(IPersonnelService personnelService) : PageModel
{
    public PersonnelDetailsDto Person { get; private set; } = null!;
    public PersonnelOptionSetDto Options { get; private set; } = new([], [], [], [], []);
    public bool CanViewSensitive => User.IsInRole(SystemRoles.SystemAdministrator)
        || User.IsInRole(SystemRoles.ApplicationAdministrator)
        || User.IsInRole(SystemRoles.Finance);
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator)
        || User.IsInRole(SystemRoles.ApplicationAdministrator);

    [BindProperty(SupportsGet = true)] public Guid PersonId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? AsOf { get; set; }
    [BindProperty] public ScopeSwitchInput SwitchInput { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();
        InitializeSwitchInput();
        return Page();
    }

    public async Task<IActionResult> OnPostSwitchScopeAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            await personnelService.SwitchScopeAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                new SwitchPersonnelScopeRequest(
                    PersonId,
                    SwitchInput.Scope,
                    SwitchInput.InternalType,
                    SwitchInput.ExternalType,
                    SwitchInput.LegalEntityId,
                    SwitchInput.BusinessPartnerId,
                    SwitchInput.OrganizationUnitId,
                    SwitchInput.ProjectId,
                    SwitchInput.CrewBusinessPartnerId,
                    SwitchInput.PositionTitle,
                    SwitchInput.EffectiveDate,
                    SwitchInput.Reason),
                cancellationToken);

            if (TempData is not null) TempData["SuccessMessage"] = "人员身份已按生效日期完成切换。";
            return RedirectToPage(new { personId = PersonId, asOf = AsOf });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var person = await personnelService.GetAsync(PersonId, AsOf, CanViewSensitive, cancellationToken);
        if (person is null) return false;
        Person = person;
        Options = await personnelService.GetOptionsAsync(cancellationToken);
        return true;
    }

    private void InitializeSwitchInput()
    {
        var current = Person.CurrentAffiliation;
        SwitchInput = new ScopeSwitchInput
        {
            Scope = current?.Scope == PersonnelScope.Internal ? PersonnelScope.External : PersonnelScope.Internal,
            InternalType = current?.Scope == PersonnelScope.External ? EmployeeType.Formal : null,
            ExternalType = current?.Scope == PersonnelScope.Internal ? ExternalPersonnelType.Other : null,
            EffectiveDate = DateOnly.FromDateTime(DateTime.Today),
            Reason = "人员身份切换"
        };
    }

    public sealed class ScopeSwitchInput
    {
        [Required] public PersonnelScope Scope { get; set; } = PersonnelScope.Internal;
        public EmployeeType? InternalType { get; set; }
        public ExternalPersonnelType? ExternalType { get; set; }
        public Guid? LegalEntityId { get; set; }
        public Guid? BusinessPartnerId { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? CrewBusinessPartnerId { get; set; }
        [StringLength(100)] public string? PositionTitle { get; set; }
        [Required] public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        [Required, StringLength(500)] public string Reason { get; set; } = "人员身份切换";
    }
}
