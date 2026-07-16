namespace EngineeringManager.Domain.Projects;

public sealed record ContractAllocationInput(decimal? Amount, decimal? Percentage);

public static class ContractAllocationValidator
{
    public static void Validate(
        ContractAllocationMode mode,
        decimal contractAmount,
        IEnumerable<ContractAllocationInput> allocations)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        if (contractAmount < 0m)
        {
            throw new ArgumentException("合同金额不能为负数。", nameof(contractAmount));
        }

        var items = allocations.ToArray();
        if (items.Length == 0)
        {
            throw new ArgumentException("至少需要一条合同分摊记录。", nameof(allocations));
        }

        switch (mode)
        {
            case ContractAllocationMode.SingleCompany when items.Length != 1:
                throw new ArgumentException("单家公司合同只能有一条分摊记录。", nameof(allocations));
            case ContractAllocationMode.SingleCompany:
            case ContractAllocationMode.FixedAmount:
            case ContractAllocationMode.LineItem:
                ValidateAmountTotal(contractAmount, items);
                break;
            case ContractAllocationMode.Percentage:
                ValidatePercentageTotal(items);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知合同分摊方式。");
        }
    }

    private static void ValidateAmountTotal(decimal contractAmount, IReadOnlyCollection<ContractAllocationInput> allocations)
    {
        if (allocations.Any(item => !item.Amount.HasValue || item.Amount.Value < 0m))
        {
            throw new ArgumentException("固定金额分摊必须填写非负金额。", nameof(allocations));
        }

        var total = allocations.Sum(item => item.Amount!.Value);
        if (Math.Abs(total - contractAmount) > 0.01m)
        {
            throw new ArgumentException("合同分摊合计必须等于合同金额。", nameof(allocations));
        }
    }

    private static void ValidatePercentageTotal(IReadOnlyCollection<ContractAllocationInput> allocations)
    {
        if (allocations.Any(item => !item.Percentage.HasValue || item.Percentage.Value < 0m))
        {
            throw new ArgumentException("比例分摊必须填写非负比例。", nameof(allocations));
        }

        var total = allocations.Sum(item => item.Percentage!.Value);
        if (Math.Abs(total - 100m) > 0.01m)
        {
            throw new ArgumentException("合同分摊比例合计必须等于 100%。", nameof(allocations));
        }
    }
}
