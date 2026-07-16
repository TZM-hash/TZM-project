using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Companies;

public sealed class CompanyActorService(ApplicationDbContext db) : ICompanyActorService
{
    public async Task<CompanyActor> ResolveAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        var isSystemAdministrator = roles.Contains(SystemRoles.SystemAdministrator, StringComparer.Ordinal);
        var isApplicationAdministrator = roles.Contains(SystemRoles.ApplicationAdministrator, StringComparer.Ordinal);
        if (isSystemAdministrator || isApplicationAdministrator)
        {
            return CompanyActor.Administrator(userId);
        }

        var ids = await db.UserLegalEntityAccesses.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.LegalEntityId)
            .ToListAsync(cancellationToken);
        if (roles.Contains(SystemRoles.ProjectManager, StringComparer.Ordinal))
        {
            var projectIds = await db.ProjectLegalEntities.AsNoTracking()
                .Where(item => item.Project.ResponsibleUserId == userId || item.Project.Assignments.Any(assignment => assignment.UserId == userId))
                .Select(item => item.LegalEntityId)
                .ToListAsync(cancellationToken);
            ids.AddRange(projectIds);
        }
        return new CompanyActor(userId, false, false, ids.Distinct().ToArray());
    }
}
