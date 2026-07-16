using System.Security.Claims;
using EngineeringManager.Application.Companies;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Companies;

public abstract class CompanyPageModel(ICompanyActorService actorService) : PageModel
{
    protected async Task<CompanyActor> ResolveActorAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("当前用户缺少身份标识。");
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray();
        return await actorService.ResolveAsync(userId, roles, cancellationToken);
    }
}
