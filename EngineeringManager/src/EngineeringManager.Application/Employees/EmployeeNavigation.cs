namespace EngineeringManager.Application.Employees;

public sealed record EmployeeNavigation(Guid? PreviousEmployeeId, Guid? NextEmployeeId);

public static class EmployeeNavigationResolver
{
    public static EmployeeNavigation Resolve(IReadOnlyList<Guid> orderedEmployeeIds, Guid currentEmployeeId)
    {
        var currentIndex = -1;
        for (var index = 0; index < orderedEmployeeIds.Count; index++)
        {
            if (orderedEmployeeIds[index] != currentEmployeeId) continue;
            currentIndex = index;
            break;
        }

        if (currentIndex < 0) return new(null, null);

        return new(
            currentIndex > 0 ? orderedEmployeeIds[currentIndex - 1] : null,
            currentIndex < orderedEmployeeIds.Count - 1 ? orderedEmployeeIds[currentIndex + 1] : null);
    }
}
