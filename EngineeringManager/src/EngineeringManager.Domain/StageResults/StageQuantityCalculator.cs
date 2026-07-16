namespace EngineeringManager.Domain.StageResults;

public sealed record StageQuantitySummary(
    decimal CumulativeQuantity,
    decimal RemainingQuantity,
    decimal CompletionPercentage,
    bool ExceedsTarget);

public static class StageQuantityCalculator
{
    public static StageQuantitySummary Calculate(
        decimal targetQuantity,
        decimal previousCumulativeQuantity,
        decimal periodQuantity)
    {
        if (targetQuantity < 0m || previousCumulativeQuantity < 0m || periodQuantity < 0m)
        {
            throw new ArgumentException("目标工程量、历史累计工程量和本期工程量不能为负数。");
        }

        var cumulativeQuantity = previousCumulativeQuantity + periodQuantity;
        var remainingQuantity = Math.Max(targetQuantity - cumulativeQuantity, 0m);
        var completionPercentage = targetQuantity == 0m
            ? cumulativeQuantity == 0m ? 0m : 100m
            : cumulativeQuantity / targetQuantity * 100m;
        return new StageQuantitySummary(
            cumulativeQuantity,
            remainingQuantity,
            completionPercentage,
            cumulativeQuantity > targetQuantity);
    }
}
