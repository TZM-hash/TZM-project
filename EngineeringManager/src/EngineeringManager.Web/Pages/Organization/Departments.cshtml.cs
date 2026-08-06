using EngineeringManager.Application.Organization;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Organization;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class DepartmentsModel(IOrganizationService organizationService) : PageModel
{
    private static readonly HashSet<string> AllowedReturnPages =
    [
        "/Companies/Details",
        "/Crews/Details",
        "/Partners/Details"
    ];

    public IReadOnlyList<DepartmentDto> Departments { get; private set; } = [];
    public OrganizationOwnerKind OwnerKind { get; private set; }
    public Guid OwnerId { get; private set; }
    public string OwnerName => Departments.Count > 0
        ? Departments[0].OwnerName
        : OwnerKind == OrganizationOwnerKind.LegalEntity ? "自有公司" : "合作单位 / 施工班组";
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    public string SafeReturnPage => !string.IsNullOrWhiteSpace(ReturnPage) && AllowedReturnPages.Contains(ReturnPage) ? ReturnPage : OwnerKind == OrganizationOwnerKind.LegalEntity ? "/Companies/Details" : "/Partners/Details";

    [BindProperty(SupportsGet = true)] public Guid? LegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? BusinessPartnerId { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnPage { get; set; }
    [BindProperty] public DepartmentInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveOwner()) return BadRequest("必须且只能指定一个自有公司或合作单位。");
        try
        {
            await LoadAsync(cancellationToken);
            return Page();
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(exception.Message);
        }
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!TryResolveOwner()) return BadRequest("必须且只能指定一个自有公司或合作单位。");
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            await organizationService.SaveDepartmentAsync(
                new SaveDepartmentRequest(
                    Input.Id,
                    OwnerKind,
                    OwnerId,
                    Input.Code,
                    Input.Name,
                    Input.ParentId,
                    Input.IsAuthorizationScope,
                    Input.IsActive),
                cancellationToken);
            return RedirectToPage(RouteValues());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!TryResolveOwner()) return BadRequest("必须且只能指定一个自有公司或合作单位。");
        try
        {
            await organizationService.DeactivateDepartmentAsync(id, cancellationToken);
            return RedirectToPage(RouteValues());
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    private bool TryResolveOwner()
    {
        if (LegalEntityId.HasValue == BusinessPartnerId.HasValue) return false;
        OwnerKind = LegalEntityId.HasValue ? OrganizationOwnerKind.LegalEntity : OrganizationOwnerKind.BusinessPartner;
        OwnerId = LegalEntityId ?? BusinessPartnerId!.Value;
        return OwnerId != Guid.Empty;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Departments = await organizationService.ListDepartmentsAsync(OwnerKind, OwnerId, true, cancellationToken);
        if (string.IsNullOrWhiteSpace(Input.Code))
        {
            Input = new DepartmentInput { IsActive = true, IsAuthorizationScope = true };
        }
    }

    private object RouteValues() => new
    {
        legalEntityId = LegalEntityId,
        businessPartnerId = BusinessPartnerId,
        returnPage = ReturnPage
    };

    public sealed class DepartmentInput
    {
        public Guid? Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
        public bool IsAuthorizationScope { get; set; } = true;
        public bool IsActive { get; set; } = true;
    }
}
