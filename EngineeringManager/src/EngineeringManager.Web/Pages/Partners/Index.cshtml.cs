using EngineeringManager.Application.Partners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Partners;

[Authorize]
public sealed class IndexModel(IBusinessPartnerService partnerService) : PageModel
{
    public IReadOnlyList<BusinessPartnerDto> Partners { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Partners = await partnerService.ListAsync(null, null, cancellationToken);
    }
}
