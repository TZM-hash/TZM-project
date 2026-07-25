using System.Security.Claims;
using EngineeringManager.Application.Companies;
using EngineeringManager.Application.Employees;
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

    protected static int CountActiveEmployees(IEnumerable<EmployeeDto> employees, IEnumerable<Guid> companyIds)
    {
        var visibleCompanyIds = companyIds.ToHashSet();
        return employees.Count(employee =>
            employee.IsActive
            && ResolveEmployeeCompanyId(employee) is { } companyId
            && visibleCompanyIds.Contains(companyId));
    }

    private static Guid? ResolveEmployeeCompanyId(EmployeeDto employee) =>
        employee.Affiliations.FirstOrDefault(affiliation => affiliation.IsPrimary)?.LegalEntityId
        ?? employee.DefaultLegalEntityId;
}
