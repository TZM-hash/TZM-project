namespace EngineeringManager.Domain.Projects;

public sealed record LineItemAmountInput(
    decimal? EstimatedQuantity,
    decimal? EstimatedUnitPrice,
    decimal? SettledQuantity,
    decimal? SettledUnitPrice);

public sealed record ProjectAmountSummary(
    decimal EstimatedAmount,
    decimal SettledAmount,
    decimal CurrentAmount,
    ProjectSettlementStatus SettlementStatus);

public static class ProjectAmountCalculator
{
    public static ProjectAmountSummary Calculate(IEnumerable<LineItemAmountInput> lineItems)
    {
        ArgumentNullException.ThrowIfNull(lineItems);
        var items = lineItems.ToArray();
        var estimatedAmount = items.Sum(item => CalculateAmount(item.EstimatedQuantity, item.EstimatedUnitPrice));
        var settledAmount = items.Sum(item => CalculateAmount(item.SettledQuantity, item.SettledUnitPrice));
        var currentAmount = items.Sum(item =>
            HasFinalAmount(item)
                ? CalculateAmount(item.SettledQuantity, item.SettledUnitPrice)
                : CalculateAmount(item.EstimatedQuantity, item.EstimatedUnitPrice));
        var settledCount = items.Count(HasFinalAmount);
        var settlementStatus = settledCount == 0
            ? ProjectSettlementStatus.Estimated
            : settledCount == items.Length
                ? ProjectSettlementStatus.Settled
                : ProjectSettlementStatus.PartiallySettled;

        return new ProjectAmountSummary(estimatedAmount, settledAmount, currentAmount, settlementStatus);
    }

    private static bool HasFinalAmount(LineItemAmountInput item) =>
        item.SettledQuantity.HasValue && item.SettledUnitPrice.HasValue;

    private static decimal CalculateAmount(decimal? quantity, decimal? unitPrice) =>
        (quantity ?? 0m) * (unitPrice ?? 0m);
}
