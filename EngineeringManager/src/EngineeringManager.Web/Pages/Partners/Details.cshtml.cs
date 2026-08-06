using EngineeringManager.Application.Partners;
using EngineeringManager.Application.Organization;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Partners;

[Authorize]
public sealed class DetailsModel(
    IBusinessPartnerService service,
    IOrganizationSummaryService? organizationSummaryService = null) : PageModel
{
    public BusinessPartnerDto Partner { get; private set; } = null!;
    public OrganizationSummaryDto? OrganizationSummary { get; private set; }
    public bool CanManageFinance => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken token)
    {
        var partner = await service.GetAsync(id, token);
        if (partner is null) return NotFound();
        Partner = partner;
        if (organizationSummaryService is not null)
        {
            OrganizationSummary = await organizationSummaryService.GetAsync(
                new OrganizationSummaryQuery(OrganizationOwnerKind.BusinessPartner, id, DateOnly.FromDateTime(DateTime.Today)),
                token);
        }
        return Page();
    }
}
