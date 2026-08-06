namespace EngineeringManager.Domain.Personnel;

public sealed record EngagementPeriod(DateOnly StartDate, DateOnly? EndDate, bool IsPrimary);

public sealed record CurrentEngagement(DateOnly StartDate, DateOnly? EndDate, bool IsPrimary, string? ProjectName);

public static class PersonnelEngagementRules
{
    public static void ValidatePrimaryPeriods(IEnumerable<EngagementPeriod> source)
    {
        var periods = source.Where(item => item.IsPrimary).OrderBy(item => item.StartDate).ToArray();
        for (var index = 1; index < periods.Length; index++)
        {
            var previous = periods[index - 1];
            var current = periods[index];
            if (previous.EndDate is null || current.StartDate <= previous.EndDate.Value)
            {
                throw new InvalidOperationException("同一人员的主要身份归属时间区间不能重叠。");
            }
        }
    }

    public static CurrentEngagement? SelectCurrent(IEnumerable<CurrentEngagement> source, DateOnly asOf)
        => source
            .Where(item => item.IsPrimary && item.StartDate <= asOf && (item.EndDate is null || item.EndDate >= asOf))
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefault();

    public static void ValidateDepartmentOwner(
        Guid? departmentLegalEntityId,
        Guid? departmentBusinessPartnerId,
        Guid? selectedLegalEntityId,
        Guid? selectedBusinessPartnerId)
    {
        if (departmentLegalEntityId.HasValue && departmentBusinessPartnerId.HasValue)
        {
            throw new InvalidOperationException("部门不能同时属于自有公司和合作单位。");
        }

        if (departmentLegalEntityId != selectedLegalEntityId || departmentBusinessPartnerId != selectedBusinessPartnerId)
        {
            throw new InvalidOperationException("部门不属于当前选择的组织。");
        }
    }
}
