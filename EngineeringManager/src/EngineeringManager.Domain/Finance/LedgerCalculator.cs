namespace EngineeringManager.Domain.Finance;

public sealed record LedgerSummary(
    decimal ReceivableAmount,
    decimal CollectedAmount,
    decimal UncollectedAmount,
    decimal PayableAmount,
    decimal PaidAmount,
    decimal DeductionAmount,
    decimal UnpaidAmount,
    bool HasCollectionRisk,
    bool HasPaymentRisk);

public static class LedgerCalculator
{
    public static LedgerSummary Calculate(
        decimal receivableAmount,
        decimal collectionAmount,
        decimal refundOrReversalAmount,
        decimal payableAmount,
        decimal paymentAmount,
        decimal paymentReversalAmount,
        decimal deductionAmount)
    {
        var values = new[]
        {
            receivableAmount,
            collectionAmount,
            refundOrReversalAmount,
            payableAmount,
            paymentAmount,
            paymentReversalAmount,
            deductionAmount
        };
        if (values.Any(value => value < 0m))
        {
            throw new ArgumentException("经营台账金额不能为负数，退款和冲销应使用独立记录。");
        }

        var collectedAmount = collectionAmount - refundOrReversalAmount;
        var paidAmount = paymentAmount - paymentReversalAmount;
        var uncollectedAmount = receivableAmount - collectedAmount;
        var unpaidAmount = payableAmount - paidAmount - deductionAmount;
        return new LedgerSummary(
            receivableAmount,
            collectedAmount,
            uncollectedAmount,
            payableAmount,
            paidAmount,
            deductionAmount,
            unpaidAmount,
            collectedAmount > receivableAmount,
            paidAmount + deductionAmount > payableAmount);
    }
}
