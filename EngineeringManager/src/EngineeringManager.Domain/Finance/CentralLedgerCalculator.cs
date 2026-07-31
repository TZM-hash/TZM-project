namespace EngineeringManager.Domain.Finance;

public sealed record CentralLedgerCalculationInput(
    decimal GrossSettlementAmount,
    decimal Deductions,
    decimal InvoiceReducingDeductions,
    decimal BaseInvoiceAmount,
    decimal InvoicedAmount,
    decimal CashAmount);

public sealed record CentralLedgerMetrics(
    decimal GrossSettlementAmount,
    decimal Deductions,
    decimal ActualAmount,
    decimal ShouldInvoiceAmount,
    decimal InvoicedAmount,
    decimal CashAmount,
    decimal UncollectedOrUnpaid,
    decimal Uninvoiced,
    decimal InvoicedAndCollectedOrPaid,
    decimal InvoicedAndUncollectedOrUnpaid,
    decimal AdvanceInvoiceCash,
    decimal UninvoicedAndUncollectedOrUnpaid,
    decimal InvoicedWithoutCashRequirement,
    decimal OverSettlementCash,
    decimal OverInvoiced)
{
    public static CentralLedgerMetrics Zero => new(
        0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
}

public static class CentralLedgerCalculator
{
    public static CentralLedgerMetrics Calculate(CentralLedgerCalculationInput input)
    {
        Validate(input);

        var grossSettlementAmount = Math.Max(input.GrossSettlementAmount, 0m);
        var baseInvoiceAmount = Math.Max(input.BaseInvoiceAmount, 0m);
        var invoicedAmount = Math.Max(input.InvoicedAmount, 0m);
        var actualAmount = Math.Max(grossSettlementAmount - input.Deductions, 0m);
        var shouldInvoiceAmount = Math.Max(baseInvoiceAmount - input.InvoiceReducingDeductions, 0m);
        // Legacy refund rows can make the net allocated cash negative when the
        // matching original receipt is not present in the imported history.
        // Settlement metrics represent effective cash received/paid, so the
        // lower bound is zero while the raw refund remains visible in the cash ledger.
        var cashAmount = Math.Max(input.CashAmount, 0m);
        var invoicedCashBase = Math.Min(invoicedAmount, actualAmount);

        return new CentralLedgerMetrics(
            grossSettlementAmount,
            input.Deductions,
            actualAmount,
            shouldInvoiceAmount,
            invoicedAmount,
            cashAmount,
            Math.Max(actualAmount - cashAmount, 0m),
            Math.Max(shouldInvoiceAmount - invoicedAmount, 0m),
            Math.Min(Math.Min(invoicedAmount, cashAmount), actualAmount),
            Math.Max(invoicedCashBase - cashAmount, 0m),
            Math.Max(cashAmount - invoicedAmount, 0m),
            Math.Max(actualAmount - Math.Max(
                Math.Min(input.InvoicedAmount, actualAmount),
                Math.Min(cashAmount, actualAmount)), 0m),
            Math.Max(Math.Min(input.InvoicedAmount, shouldInvoiceAmount) - actualAmount, 0m),
            Math.Max(cashAmount - actualAmount, 0m),
            Math.Max(input.InvoicedAmount - shouldInvoiceAmount, 0m));
    }

    public static CentralLedgerMetrics Add(CentralLedgerMetrics left, CentralLedgerMetrics right) => new(
        left.GrossSettlementAmount + right.GrossSettlementAmount,
        left.Deductions + right.Deductions,
        left.ActualAmount + right.ActualAmount,
        left.ShouldInvoiceAmount + right.ShouldInvoiceAmount,
        left.InvoicedAmount + right.InvoicedAmount,
        left.CashAmount + right.CashAmount,
        left.UncollectedOrUnpaid + right.UncollectedOrUnpaid,
        left.Uninvoiced + right.Uninvoiced,
        left.InvoicedAndCollectedOrPaid + right.InvoicedAndCollectedOrPaid,
        left.InvoicedAndUncollectedOrUnpaid + right.InvoicedAndUncollectedOrUnpaid,
        left.AdvanceInvoiceCash + right.AdvanceInvoiceCash,
        left.UninvoicedAndUncollectedOrUnpaid + right.UninvoicedAndUncollectedOrUnpaid,
        left.InvoicedWithoutCashRequirement + right.InvoicedWithoutCashRequirement,
        left.OverSettlementCash + right.OverSettlementCash,
        left.OverInvoiced + right.OverInvoiced);

    private static void Validate(CentralLedgerCalculationInput input)
    {
        if (input.Deductions < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.Deductions, "扣款金额不能为负数。");
        }

        if (input.InvoiceReducingDeductions < 0m || input.InvoiceReducingDeductions > input.Deductions)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.InvoiceReducingDeductions, "扣减应开票的扣款必须位于全部扣款范围内。");
        }

    }
}
