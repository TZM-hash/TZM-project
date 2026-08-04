namespace EngineeringManager.Application.Projects;

public sealed record ProjectNavigation(Guid? PreviousProjectId, Guid? NextProjectId);

public static class ProjectNavigationResolver
{
    public static ProjectNavigation Resolve(IReadOnlyList<Guid> orderedProjectIds, Guid currentProjectId)
    {
        var currentIndex = -1;
        for (var index = 0; index < orderedProjectIds.Count; index++)
        {
            if (orderedProjectIds[index] != currentProjectId) continue;
            currentIndex = index;
            break;
        }

        if (currentIndex < 0) return new(null, null);

        return new(
            currentIndex > 0 ? orderedProjectIds[currentIndex - 1] : null,
            currentIndex < orderedProjectIds.Count - 1 ? orderedProjectIds[currentIndex + 1] : null);
    }
}
