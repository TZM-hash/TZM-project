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

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator)]
public sealed class CreateModel(IPersonnelService personnelService) : PageModel
{
    public PersonnelOptionSetDto Options { get; private set; } = new([], [], [], [], []);

    [BindProperty(SupportsGet = true)] public PersonnelScope? Scope { get; set; }
    [BindProperty] public CreatePersonInput Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Options = await personnelService.GetOptionsAsync(cancellationToken);
        Input.Scope = Scope ?? PersonnelScope.Internal;
        ApplyScopeDefaults();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        NormalizeScopeTypes();

        if (!ModelState.IsValid)
        {
            Options = await personnelService.GetOptionsAsync(cancellationToken);
            return Page();
        }

        try
        {
            var (legalEntityId, businessPartnerId) = ParseOwnerKey(Input.OwnerKey);
            var crewBusinessPartnerId = Input.Scope == PersonnelScope.External
                && Input.ExternalType == ExternalPersonnelType.ConstructionCrew
                    ? businessPartnerId
                    : Input.CrewBusinessPartnerId;
            var created = await personnelService.CreateAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                new CreatePersonRequest(
                    Input.PersonNumber,
                    Input.Name,
                    Input.Scope,
                    Input.InternalType,
                    Input.ExternalType,
                    Input.Phone,
                    Input.IdentityNumber,
                    Input.BankAccountNumber,
                    Input.BankName,
                    legalEntityId,
                    businessPartnerId,
                    Input.OrganizationUnitId,
                    Input.ProjectId,
                    crewBusinessPartnerId,
                    Input.PositionTitle,
                    Input.EffectiveDate,
                    Input.Notes,
                    Input.Reason),
                cancellationToken);
            return RedirectToPage("/Personnel/Details", new { personId = created.Id });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            Options = await personnelService.GetOptionsAsync(cancellationToken);
            return Page();
        }
    }

    private void ApplyScopeDefaults()
    {
        if (Input.Scope == PersonnelScope.Internal)
        {
            Input.InternalType ??= EmployeeType.Formal;
            Input.ExternalType = null;
        }
        else
        {
            Input.InternalType = null;
            Input.ExternalType ??= ExternalPersonnelType.BusinessPartner;
        }
        Input.EffectiveDate = DateOnly.FromDateTime(DateTime.Today);
    }

    private void NormalizeScopeTypes()
    {
        if (Input.Scope == PersonnelScope.Internal)
        {
            Input.InternalType ??= EmployeeType.Formal;
            Input.ExternalType = null;
        }
        else
        {
            Input.InternalType = null;
            Input.ExternalType ??= ExternalPersonnelType.BusinessPartner;
        }
    }

    private static (Guid? LegalEntityId, Guid? BusinessPartnerId) ParseOwnerKey(string? ownerKey)
    {
        if (string.IsNullOrWhiteSpace(ownerKey)) return (null, null);
        var parts = ownerKey.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id))
        {
            throw new InvalidOperationException("所属公司或单位格式无效。");
        }
        return parts[0] switch
        {
            "legal" => (id, null),
            "partner" => (null, id),
            _ => throw new InvalidOperationException("所属公司或单位格式无效。")
        };
    }

    public sealed class CreatePersonInput
    {
        [Required, StringLength(50)] public string PersonNumber { get; set; } = string.Empty;
        [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
        [Required] public PersonnelScope Scope { get; set; } = PersonnelScope.Internal;
        public EmployeeType? InternalType { get; set; } = EmployeeType.Formal;
        public ExternalPersonnelType? ExternalType { get; set; }
        [StringLength(50)] public string? Phone { get; set; }
        [StringLength(50)] public string? IdentityNumber { get; set; }
        [StringLength(100)] public string? BankAccountNumber { get; set; }
        [StringLength(100)] public string? BankName { get; set; }
        [Required] public string? OwnerKey { get; set; }
        public Guid? LegalEntityId { get; set; }
        public Guid? BusinessPartnerId { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? CrewBusinessPartnerId { get; set; }
        [StringLength(100)] public string? PositionTitle { get; set; }
        [Required] public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        [StringLength(1000)] public string? Notes { get; set; }
        [Required, StringLength(500)] public string Reason { get; set; } = "新增人员";
    }
}
