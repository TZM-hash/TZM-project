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

        var actualAmount = Math.Max(input.GrossSettlementAmount - input.Deductions, 0m);
        var shouldInvoiceAmount = Math.Max(input.BaseInvoiceAmount - input.InvoiceReducingDeductions, 0m);
        var invoicedCashBase = Math.Min(input.InvoicedAmount, actualAmount);

        return new CentralLedgerMetrics(
            input.GrossSettlementAmount,
            input.Deductions,
            actualAmount,
            shouldInvoiceAmount,
            input.InvoicedAmount,
            input.CashAmount,
            Math.Max(actualAmount - input.CashAmount, 0m),
            Math.Max(shouldInvoiceAmount - input.InvoicedAmount, 0m),
            Math.Min(Math.Min(input.InvoicedAmount, input.CashAmount), actualAmount),
            Math.Max(invoicedCashBase - input.CashAmount, 0m),
            Math.Max(input.CashAmount - input.InvoicedAmount, 0m),
            Math.Max(actualAmount - Math.Max(
                Math.Min(input.InvoicedAmount, actualAmount),
                Math.Min(input.CashAmount, actualAmount)), 0m),
            Math.Max(Math.Min(input.InvoicedAmount, shouldInvoiceAmount) - actualAmount, 0m),
            Math.Max(input.CashAmount - actualAmount, 0m),
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
        if (input.GrossSettlementAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.GrossSettlementAmount, "结算金额不能为负数。");
        }

        if (input.Deductions < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.Deductions, "扣款金额不能为负数。");
        }

        if (input.InvoiceReducingDeductions < 0m || input.InvoiceReducingDeductions > input.Deductions)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.InvoiceReducingDeductions, "扣减应开票的扣款必须位于全部扣款范围内。");
        }

        if (input.BaseInvoiceAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.BaseInvoiceAmount, "应开票金额不能为负数。");
        }

        if (input.InvoicedAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.InvoicedAmount, "已开票金额不能为负数。");
        }

        if (input.CashAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.CashAmount, "实际收付款金额不能为负数。");
        }
    }
}
