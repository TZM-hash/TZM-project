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
    public bool CanEditCurrentAffiliation => CanManage
        && (!AsOf.HasValue || AsOf.Value == DateOnly.FromDateTime(DateTime.Today));

    [BindProperty(SupportsGet = true)] public Guid PersonId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? AsOf { get; set; }
    [BindProperty] public PublicPersonInput PublicInput { get; set; } = new();
    [BindProperty] public CurrentAffiliationInput AffiliationInput { get; set; } = new();
    [BindProperty] public ScopeSwitchInput SwitchInput { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();
        InitializePublicInput();
        InitializeAffiliationInput();
        InitializeSwitchInput();
        return Page();
    }

    public async Task<IActionResult> OnPostSavePublicDataAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        RemoveModelStateFor(nameof(AffiliationInput), nameof(SwitchInput));
        if (!ModelState.IsValid)
        {
            return await ReloadAfterPublicDataErrorAsync(cancellationToken);
        }

        try
        {
            await personnelService.SavePublicDataAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                new SavePersonRequest(
                    PersonId,
                    PublicInput.Name,
                    PublicInput.Phone,
                    PublicInput.IdentityNumber,
                    PublicInput.BankAccountNumber,
                    PublicInput.BankName,
                    PublicInput.Notes,
                    PublicInput.IsActive,
                    PublicInput.ConcurrencyStamp,
                    PublicInput.Reason),
                cancellationToken);
            if (TempData is not null) TempData["SuccessMessage"] = "人员公共资料已更新。";
            return RedirectToPage(new { personId = PersonId, asOf = AsOf });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAfterPublicDataErrorAsync(cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostSaveAffiliationAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        RemoveModelStateFor(nameof(PublicInput), nameof(SwitchInput));
        if (!CanEditCurrentAffiliation)
        {
            ModelState.AddModelError(string.Empty, "历史日期查看模式只读，请返回当前日期后再调整归属。");
            return await ReloadAfterAffiliationErrorAsync(cancellationToken);
        }
        if (!ModelState.IsValid)
        {
            return await ReloadAfterAffiliationErrorAsync(cancellationToken);
        }

        try
        {
            var person = await personnelService.GetAsync(PersonId, AffiliationInput.EffectiveDate, CanViewSensitive, cancellationToken)
                ?? throw new InvalidOperationException("人员不存在。");
            var current = person.CurrentAffiliation
                ?? throw new InvalidOperationException("当前人员没有可调整的有效归属。");
            var (legalEntityId, businessPartnerId) = ParseOwnerKey(AffiliationInput.OwnerKey);
            var crewBusinessPartnerId = current.Scope == PersonnelScope.External
                && current.ExternalType == ExternalPersonnelType.ConstructionCrew
                    ? businessPartnerId
                    : AffiliationInput.CrewBusinessPartnerId;
            await personnelService.SaveAffiliationAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                new SavePersonnelAffiliationRequest(
                    PersonId,
                    current.Scope,
                    current.InternalType,
                    current.ExternalType,
                    legalEntityId,
                    businessPartnerId,
                    AffiliationInput.OrganizationUnitId,
                    AffiliationInput.ProjectId,
                    crewBusinessPartnerId,
                    AffiliationInput.PositionTitle,
                    AffiliationInput.EffectiveDate,
                    AffiliationInput.Reason,
                    AffiliationInput.ConcurrencyStamp),
                cancellationToken);
            if (TempData is not null) TempData["SuccessMessage"] = "人员当前归属已更新。";
            return RedirectToPage(new { personId = PersonId, asOf = AsOf });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAfterAffiliationErrorAsync(cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostSwitchScopeAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        RemoveModelStateFor(nameof(PublicInput), nameof(AffiliationInput));
        if (!CanEditCurrentAffiliation)
        {
            ModelState.AddModelError(string.Empty, "历史日期查看模式只读，请返回当前日期后再切换身份。");
            return await ReloadAfterSwitchErrorAsync(cancellationToken);
        }
        NormalizeSwitchTypes();
        if (!ModelState.IsValid)
        {
            return await ReloadAfterSwitchErrorAsync(cancellationToken);
        }

        try
        {
            var (legalEntityId, businessPartnerId) = string.IsNullOrWhiteSpace(SwitchInput.OwnerKey)
                ? (SwitchInput.LegalEntityId, SwitchInput.BusinessPartnerId)
                : ParseOwnerKey(SwitchInput.OwnerKey);
            var crewBusinessPartnerId = SwitchInput.Scope == PersonnelScope.External
                && SwitchInput.ExternalType == ExternalPersonnelType.ConstructionCrew
                    ? businessPartnerId
                    : SwitchInput.CrewBusinessPartnerId;
            await personnelService.SwitchScopeAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                new SwitchPersonnelScopeRequest(
                    PersonId,
                    SwitchInput.Scope,
                    SwitchInput.InternalType,
                    SwitchInput.ExternalType,
                    legalEntityId,
                    businessPartnerId,
                    SwitchInput.OrganizationUnitId,
                    SwitchInput.ProjectId,
                    crewBusinessPartnerId,
                    SwitchInput.PositionTitle,
                    SwitchInput.EffectiveDate,
                    SwitchInput.Reason,
                    SwitchInput.ConcurrencyStamp),
                cancellationToken);

            if (TempData is not null) TempData["SuccessMessage"] = "人员身份已按生效日期完成切换。";
            return RedirectToPage(new { personId = PersonId, asOf = AsOf });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAfterSwitchErrorAsync(cancellationToken);
        }
    }

    private void NormalizeSwitchTypes()
    {
        if (SwitchInput.Scope == PersonnelScope.Internal)
        {
            SwitchInput.InternalType ??= EmployeeType.Formal;
            SwitchInput.ExternalType = null;
        }
        else
        {
            SwitchInput.InternalType = null;
            SwitchInput.ExternalType ??= ExternalPersonnelType.Other;
        }
    }

    private void RemoveModelStateFor(params string[] prefixes)
    {
        var keys = ModelState.Keys
            .Where(key => prefixes.Any(prefix =>
                string.Equals(key, prefix, StringComparison.Ordinal)
                || key.StartsWith(prefix + ".", StringComparison.Ordinal)))
            .ToArray();
        foreach (var key in keys)
        {
            ModelState.Remove(key);
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

    private async Task<IActionResult> ReloadAfterPublicDataErrorAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();
        InitializeAffiliationInput();
        InitializeSwitchInput();
        return Page();
    }

    private async Task<IActionResult> ReloadAfterAffiliationErrorAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();
        InitializePublicInput();
        InitializeSwitchInput();
        return Page();
    }

    private async Task<IActionResult> ReloadAfterSwitchErrorAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();
        InitializePublicInput();
        InitializeAffiliationInput();
        return Page();
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
            Reason = "人员身份切换",
            ConcurrencyStamp = current?.ConcurrencyStamp
        };
    }

    private void InitializePublicInput()
    {
        PublicInput = new PublicPersonInput
        {
            Name = Person.Name,
            Phone = Person.Phone,
            IdentityNumber = CanViewSensitive ? Person.IdentityNumber : null,
            BankAccountNumber = CanViewSensitive ? Person.BankAccountNumber : null,
            BankName = Person.BankName,
            Notes = Person.Notes,
            IsActive = Person.IsActive,
            ConcurrencyStamp = Person.ConcurrencyStamp,
            Reason = "更新人员公共资料"
        };
    }

    private void InitializeAffiliationInput()
    {
        var current = Person.CurrentAffiliation;
        AffiliationInput = new CurrentAffiliationInput
        {
            OwnerKey = current?.LegalEntityId is Guid legalEntityId
                ? $"legal:{legalEntityId}"
                : current?.BusinessPartnerId is Guid businessPartnerId
                    ? $"partner:{businessPartnerId}"
                    : null,
            LegalEntityId = current?.LegalEntityId,
            BusinessPartnerId = current?.BusinessPartnerId,
            OrganizationUnitId = current?.OrganizationUnitId,
            ProjectId = current?.ProjectId,
            CrewBusinessPartnerId = current?.CrewBusinessPartnerId,
            PositionTitle = current?.PositionTitle,
            EffectiveDate = DateOnly.FromDateTime(DateTime.Today),
            Reason = "调整当前归属",
            ConcurrencyStamp = current?.ConcurrencyStamp
        };
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

    public sealed class PublicPersonInput
    {
        [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
        [StringLength(50)] public string? Phone { get; set; }
        [StringLength(50)] public string? IdentityNumber { get; set; }
        [StringLength(100)] public string? BankAccountNumber { get; set; }
        [StringLength(100)] public string? BankName { get; set; }
        [StringLength(1000)] public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid ConcurrencyStamp { get; set; }
        [Required, StringLength(500)] public string Reason { get; set; } = "更新人员公共资料";
    }

    public sealed class CurrentAffiliationInput
    {
        [Required] public string? OwnerKey { get; set; }
        public Guid? LegalEntityId { get; set; }
        public Guid? BusinessPartnerId { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? CrewBusinessPartnerId { get; set; }
        [StringLength(100)] public string? PositionTitle { get; set; }
        [Required] public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        [Required, StringLength(500)] public string Reason { get; set; } = "调整当前归属";
        public Guid? ConcurrencyStamp { get; set; }
    }

    public sealed class ScopeSwitchInput
    {
        [Required] public PersonnelScope Scope { get; set; } = PersonnelScope.Internal;
        public EmployeeType? InternalType { get; set; }
        public ExternalPersonnelType? ExternalType { get; set; }
        public string? OwnerKey { get; set; }
        public Guid? LegalEntityId { get; set; }
        public Guid? BusinessPartnerId { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? CrewBusinessPartnerId { get; set; }
        [StringLength(100)] public string? PositionTitle { get; set; }
        [Required] public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        [Required, StringLength(500)] public string Reason { get; set; } = "人员身份切换";
        [Required] public Guid? ConcurrencyStamp { get; set; }
    }
}
