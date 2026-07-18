namespace EngineeringManager.Domain.Employees;

public sealed record PayrollDisbursementLineInput(
    PayrollRecipientType RecipientType,
    Guid? EmployeeId,
    Guid? ConstructionWorkerId,
    Guid? CrewBusinessPartnerId,
    decimal Amount)
{
    public static PayrollDisbursementLineInput ForEmployee(Guid employeeId, decimal amount) =>
        new(PayrollRecipientType.Employee, employeeId, null, null, amount);

    public static PayrollDisbursementLineInput ForCrewWorker(Guid constructionWorkerId, Guid crewBusinessPartnerId, decimal amount) =>
        new(PayrollRecipientType.CrewWorker, null, constructionWorkerId, crewBusinessPartnerId, amount);
}

public sealed record PayrollCrewAmount(Guid CrewBusinessPartnerId, decimal Amount);

public sealed record PayrollDisbursementSummary(
    decimal EmployeeAmount,
    decimal CrewAmount,
    decimal DetailAmount,
    decimal ActualAmount,
    decimal Difference,
    IReadOnlyList<PayrollCrewAmount> CrewAmounts);

public static class PayrollDisbursementRules
{
    public static PayrollDisbursementSummary Calculate(
        decimal actualAmount,
        IEnumerable<PayrollDisbursementLineInput> lines)
    {
        if (actualAmount < 0m || decimal.Round(actualAmount, 2) != actualAmount)
        {
            throw new ArgumentException("实际发放总额不能为负数且最多保留两位小数。", nameof(actualAmount));
        }

        var items = lines.ToArray();
        foreach (var item in items)
        {
            EnsureReferenceMatchesType(item);
            if (item.Amount <= 0m || decimal.Round(item.Amount, 2) != item.Amount)
            {
                throw new ArgumentException("人员发放金额必须大于零且最多保留两位小数。", nameof(lines));
            }
        }

        var duplicate = items
            .GroupBy(RecipientKey)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException("同一人员不能在同一工资批次重复出现。");
        }

        var employeeAmount = items.Where(item => item.RecipientType == PayrollRecipientType.Employee).Sum(item => item.Amount);
        var crewAmounts = items
            .Where(item => item.RecipientType == PayrollRecipientType.CrewWorker)
            .GroupBy(item => item.CrewBusinessPartnerId!.Value)
            .Select(group => new PayrollCrewAmount(group.Key, group.Sum(item => item.Amount)))
            .OrderBy(item => item.CrewBusinessPartnerId)
            .ToArray();
        var crewAmount = crewAmounts.Sum(item => item.Amount);
        var detailAmount = employeeAmount + crewAmount;
        return new PayrollDisbursementSummary(
            employeeAmount,
            crewAmount,
            detailAmount,
            actualAmount,
            actualAmount - detailAmount,
            crewAmounts);
    }

    public static PayrollDisbursementSummary EnsureCanReview(
        decimal actualAmount,
        Guid? projectId,
        IEnumerable<PayrollDisbursementLineInput> lines)
    {
        var summary = Calculate(actualAmount, lines);
        if (summary.DetailAmount <= 0m)
        {
            throw new InvalidOperationException("已核对工资批次必须包含至少一条人员发放明细。");
        }

        if (summary.CrewAmount > 0m && !projectId.HasValue)
        {
            throw new InvalidOperationException("包含施工班组人员的工资批次必须选择发放项目。");
        }

        if (summary.Difference != 0m)
        {
            throw new InvalidOperationException($"人员明细合计与实际发放总额存在差额：{summary.Difference:N2}。");
        }

        return summary;
    }

    private static void EnsureReferenceMatchesType(PayrollDisbursementLineInput item)
    {
        var valid = item.RecipientType switch
        {
            PayrollRecipientType.Employee =>
                item.EmployeeId.HasValue && !item.ConstructionWorkerId.HasValue && !item.CrewBusinessPartnerId.HasValue,
            PayrollRecipientType.CrewWorker =>
                !item.EmployeeId.HasValue && item.ConstructionWorkerId.HasValue && item.CrewBusinessPartnerId.HasValue,
            _ => false
        };
        if (!valid)
        {
            throw new ArgumentException("人员来源与关联人员字段不匹配。", nameof(item));
        }
    }

    private static string RecipientKey(PayrollDisbursementLineInput item) => item.RecipientType switch
    {
        PayrollRecipientType.Employee => $"employee:{item.EmployeeId}",
        PayrollRecipientType.CrewWorker => $"crew:{item.ConstructionWorkerId}",
        _ => throw new ArgumentOutOfRangeException(nameof(item))
    };
}
