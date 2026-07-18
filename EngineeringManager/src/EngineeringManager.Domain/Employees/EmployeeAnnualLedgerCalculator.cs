namespace EngineeringManager.Domain.Employees;

public enum AnnualLedgerEntryCategory
{
    OpeningBalance = 1,
    Wage = 2,
    Expense = 3,
    OtherPayable = 4,
    Adjustment = 5
}

public sealed record AnnualLedgerPayableInput(
    Guid SourceId,
    AnnualLedgerEntryCategory Category,
    DateOnly EntryDate,
    decimal Amount);

public sealed record AnnualLedgerReceiptInput(Guid ReceiptId, DateOnly ReceiptDate, decimal Amount);

public sealed record AnnualLedgerReceiptAllocation(Guid ReceiptId, Guid PayableSourceId, decimal Amount);

public sealed record EmployeeWageAmount(decimal AutomaticAmount, decimal FinalAmount);

public sealed record EmployeeAnnualLedgerSummary(
    decimal PriorYearCarryForward,
    decimal CurrentYearWagePayable,
    decimal ExpensePayable,
    decimal OtherPayable,
    decimal AdjustmentAmount,
    decimal CurrentYearNewPayable,
    decimal ReceivedAmount,
    decimal CurrentBalance,
    decimal SettlementProgressPercent,
    bool IsOverpaid,
    IReadOnlyList<AnnualLedgerReceiptAllocation> ReceiptAllocations);

public static class EmployeeAnnualLedgerCalculator
{
    public static EmployeeAnnualLedgerSummary Calculate(
        DateOnly yearStart,
        DateOnly yearEnd,
        IEnumerable<AnnualLedgerPayableInput> entries,
        IEnumerable<AnnualLedgerReceiptInput> receipts)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(receipts);
        if (yearEnd < yearStart)
        {
            throw new ArgumentException("业务年度结束日期不能早于开始日期。", nameof(yearEnd));
        }

        var payableEntries = entries.ToArray();
        var receiptEntries = receipts.ToArray();
        if (receiptEntries.Any(item => item.Amount <= 0m))
        {
            throw new ArgumentException("领款金额必须大于零。", nameof(receipts));
        }

        var priorCarryForward =
            payableEntries.Where(item => item.EntryDate < yearStart).Sum(item => item.Amount) -
            receiptEntries.Where(item => item.ReceiptDate < yearStart).Sum(item => item.Amount);
        var currentEntries = payableEntries.Where(item => item.EntryDate >= yearStart && item.EntryDate <= yearEnd).ToArray();
        var wagePayable = currentEntries.Where(item => item.Category == AnnualLedgerEntryCategory.Wage).Sum(item => item.Amount);
        var expensePayable = currentEntries.Where(item => item.Category == AnnualLedgerEntryCategory.Expense).Sum(item => item.Amount);
        var otherPayable = currentEntries.Where(item => item.Category == AnnualLedgerEntryCategory.OtherPayable).Sum(item => item.Amount);
        var adjustmentAmount = currentEntries.Where(item => item.Category == AnnualLedgerEntryCategory.Adjustment).Sum(item => item.Amount);
        var currentYearNewPayable = wagePayable + expensePayable + otherPayable + adjustmentAmount;
        var receivedAmount = receiptEntries.Where(item => item.ReceiptDate >= yearStart && item.ReceiptDate <= yearEnd).Sum(item => item.Amount);
        var currentBalance = priorCarryForward + currentYearNewPayable - receivedAmount;
        var amountDueBeforeReceipts = priorCarryForward + currentYearNewPayable;
        var settlementProgress = amountDueBeforeReceipts <= 0m
            ? 100m
            : decimal.Round(Math.Clamp(receivedAmount / amountDueBeforeReceipts * 100m, 0m, 100m), 2, MidpointRounding.AwayFromZero);

        return new EmployeeAnnualLedgerSummary(
            priorCarryForward,
            wagePayable,
            expensePayable,
            otherPayable,
            adjustmentAmount,
            currentYearNewPayable,
            receivedAmount,
            currentBalance,
            settlementProgress,
            currentBalance < 0m,
            AllocateReceipts(payableEntries, receiptEntries));
    }

    public static EmployeeWageAmount CalculateWageAmount(
        PayrollItemNature nature,
        decimal? quantity,
        decimal? unitPrice,
        decimal? manualAmount,
        decimal adjustmentAmount)
    {
        var usesQuantityAndPrice = quantity.HasValue || unitPrice.HasValue;
        decimal unsignedAmount;
        if (usesQuantityAndPrice)
        {
            if (!quantity.HasValue || !unitPrice.HasValue || manualAmount.HasValue)
            {
                throw new ArgumentException("量价工资必须同时填写数量和单价，且不能填写手工金额。");
            }

            if (quantity <= 0m || unitPrice < 0m)
            {
                throw new ArgumentException("工资数量必须大于零，单价不能为负数。");
            }

            unsignedAmount = decimal.Round(quantity.Value * unitPrice.Value, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            if (!manualAmount.HasValue || manualAmount <= 0m)
            {
                throw new ArgumentException("非量价工资必须填写大于零的手工金额。");
            }

            unsignedAmount = manualAmount.Value;
        }

        var automaticAmount = nature == PayrollItemNature.Deduction ? -unsignedAmount : unsignedAmount;
        return new EmployeeWageAmount(automaticAmount, automaticAmount + adjustmentAmount);
    }

    private static List<AnnualLedgerReceiptAllocation> AllocateReceipts(
        IReadOnlyList<AnnualLedgerPayableInput> entries,
        IReadOnlyList<AnnualLedgerReceiptInput> receipts)
    {
        var outstandingPayables = new Queue<OutstandingPayable>();
        var unappliedReceipts = new Queue<UnappliedReceipt>();
        var allocations = new List<AnnualLedgerReceiptAllocation>();
        var events = entries
            .Where(item => item.Amount > 0m)
            .Select(item => new LedgerEvent(item.EntryDate, 0, item, null))
            .Concat(receipts.Select(item => new LedgerEvent(item.ReceiptDate, 1, null, item)))
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Order)
            .ToArray();

        foreach (var ledgerEvent in events)
        {
            if (ledgerEvent.Payable is not null)
            {
                var payable = new OutstandingPayable(ledgerEvent.Payable.SourceId, ledgerEvent.Payable.Amount);
                while (payable.Remaining > 0m && unappliedReceipts.TryPeek(out var receipt))
                {
                    var amount = Math.Min(payable.Remaining, receipt.Remaining);
                    allocations.Add(new AnnualLedgerReceiptAllocation(receipt.ReceiptId, payable.SourceId, amount));
                    payable.Remaining -= amount;
                    receipt.Remaining -= amount;
                    if (receipt.Remaining == 0m)
                    {
                        unappliedReceipts.Dequeue();
                    }
                }

                if (payable.Remaining > 0m)
                {
                    outstandingPayables.Enqueue(payable);
                }

                continue;
            }

            var incomingReceipt = new UnappliedReceipt(ledgerEvent.Receipt!.ReceiptId, ledgerEvent.Receipt.Amount);
            while (incomingReceipt.Remaining > 0m && outstandingPayables.TryPeek(out var payableToSettle))
            {
                var amount = Math.Min(incomingReceipt.Remaining, payableToSettle.Remaining);
                allocations.Add(new AnnualLedgerReceiptAllocation(incomingReceipt.ReceiptId, payableToSettle.SourceId, amount));
                incomingReceipt.Remaining -= amount;
                payableToSettle.Remaining -= amount;
                if (payableToSettle.Remaining == 0m)
                {
                    outstandingPayables.Dequeue();
                }
            }

            if (incomingReceipt.Remaining > 0m)
            {
                unappliedReceipts.Enqueue(incomingReceipt);
            }
        }

        return allocations;
    }

    private sealed record LedgerEvent(
        DateOnly Date,
        int Order,
        AnnualLedgerPayableInput? Payable,
        AnnualLedgerReceiptInput? Receipt);

    private sealed class OutstandingPayable(Guid sourceId, decimal remaining)
    {
        public Guid SourceId { get; } = sourceId;

        public decimal Remaining { get; set; } = remaining;
    }

    private sealed class UnappliedReceipt(Guid receiptId, decimal remaining)
    {
        public Guid ReceiptId { get; } = receiptId;

        public decimal Remaining { get; set; } = remaining;
    }
}
