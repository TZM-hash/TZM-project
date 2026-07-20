namespace EngineeringManager.Domain.Projects;

public sealed record LineItemAmountInput(
    decimal? Quantity,
    decimal? UnitPrice,
    bool RequiresInvoice = true);

public sealed record ProjectAmountSummary(
    decimal EstimatedAmount,
    decimal SettledAmount,
    decimal CurrentAmount,
    decimal InvoiceRequiredAmount,
    ProjectSettlementStatus SettlementStatus);

public static class ProjectAmountCalculator
{
    public static ProjectAmountSummary Calculate(ProjectStage stage, IEnumerable<LineItemAmountInput> lineItems)
    {
        ArgumentNullException.ThrowIfNull(lineItems);
        var items = lineItems.ToArray();
        var currentAmount = items.Sum(item => CalculateAmount(item.Quantity, item.UnitPrice));
        var invoiceRequiredAmount = items.Where(item => item.RequiresInvoice)
            .Sum(item => CalculateAmount(item.Quantity, item.UnitPrice));
        var isPartiallySettled = stage == ProjectStage.PartiallySettled;
        var isSettled = stage == ProjectStage.SettledArchived;
        var estimatedAmount = isPartiallySettled || isSettled ? 0m : currentAmount;
        var settledAmount = isPartiallySettled || isSettled ? currentAmount : 0m;
        var settlementStatus = isPartiallySettled
            ? ProjectSettlementStatus.PartiallySettled
            : isSettled
                ? ProjectSettlementStatus.Settled
                : ProjectSettlementStatus.Estimated;

        return new ProjectAmountSummary(estimatedAmount, settledAmount, currentAmount, invoiceRequiredAmount, settlementStatus);
    }

    private static decimal CalculateAmount(decimal? quantity, decimal? unitPrice) =>
        (quantity ?? 0m) * (unitPrice ?? 0m);
}
