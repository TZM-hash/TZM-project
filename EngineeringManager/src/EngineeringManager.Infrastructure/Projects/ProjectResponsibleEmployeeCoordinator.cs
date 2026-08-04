using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Projects;

internal static class ProjectResponsibleEmployeeCoordinator
{
    public static IReadOnlyList<Guid> ResolveIds(IReadOnlyCollection<Guid>? ids, Guid? legacyId)
    {
        var source = ids?.Where(item => item != Guid.Empty).Distinct().ToArray() ?? [];
        if (source.Length == 0 && legacyId is Guid value && value != Guid.Empty)
        {
            source = [value];
        }

        return source;
    }

    public static async Task<IReadOnlyList<Guid>> ResolveAndValidateAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<Guid>? ids,
        Guid? legacyId,
        CancellationToken cancellationToken)
    {
        var resolved = ResolveIds(ids, legacyId);
        if (resolved.Count == 0)
        {
            return resolved;
        }

        var eligibleCount = await db.Employees.CountAsync(
            item => resolved.Contains(item.Id) && item.IsActive && item.IsProjectResponsible,
            cancellationToken);
        if (eligibleCount != resolved.Count)
        {
            throw new InvalidOperationException("项目负责人不存在、已停用或未设置为项目负责人。");
        }

        return resolved;
    }

    public static void Synchronize(Project project, IReadOnlyCollection<Guid> requestedIds, ApplicationDbContext db)
    {
        var ids = requestedIds.Where(item => item != Guid.Empty).Distinct().ToArray();
        var requested = ids.ToHashSet();
        var existing = project.ResponsibleEmployeeLinks.ToDictionary(item => item.EmployeeId);
        var removed = project.ResponsibleEmployeeLinks.Where(item => !requested.Contains(item.EmployeeId)).ToArray();
        foreach (var link in removed)
        {
            project.ResponsibleEmployeeLinks.Remove(link);
            db.ProjectResponsibleEmployees.Remove(link);
        }

        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < ids.Length; index++)
        {
            var employeeId = ids[index];
            if (!existing.TryGetValue(employeeId, out var link))
            {
                link = new ProjectResponsibleEmployee
                {
                    ProjectId = project.Id,
                    EmployeeId = employeeId,
                    CreatedAt = now
                };
                project.ResponsibleEmployeeLinks.Add(link);
                db.ProjectResponsibleEmployees.Add(link);
            }

            link.SortOrder = index;
            link.IsPrimary = index == 0;
            link.UpdatedAt = now;
            link.ConcurrencyStamp = Guid.NewGuid();
        }

        project.ResponsibleEmployeeId = ids.Length == 0 ? null : ids[0];
    }

    public static IReadOnlyList<Guid> Ids(Project project)
    {
        var ids = project.ResponsibleEmployeeLinks
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.EmployeeId)
            .Select(item => item.EmployeeId)
            .Distinct()
            .ToArray();
        return ids.Length > 0 || !project.ResponsibleEmployeeId.HasValue
            ? ids
            : [project.ResponsibleEmployeeId.Value];
    }

    public static IReadOnlyList<string> Names(Project project)
    {
        var names = project.ResponsibleEmployeeLinks
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.EmployeeId)
            .Where(item => item.Employee is not null)
            .Select(item => item.Employee.Name)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return names.Length > 0 || project.ResponsibleEmployee is null
            ? names
            : [project.ResponsibleEmployee.Name];
    }
}
