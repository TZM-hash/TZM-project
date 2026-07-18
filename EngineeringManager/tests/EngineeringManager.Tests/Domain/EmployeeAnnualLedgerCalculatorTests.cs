using EngineeringManager.Domain.Employees;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class EmployeeAnnualLedgerCalculatorTests
{
    [Fact]
    public void CalculatesAnnualSummaryWithCarryForwardAndNegativeBalance()
    {
        var yearStart = new DateOnly(2026, 3, 1);
        var yearEnd = new DateOnly(2027, 2, 28);
        var entries = new[]
        {
            Entry(2026, 2, 15, 1_000m, AnnualLedgerEntryCategory.OpeningBalance),
            Entry(2026, 3, 10, 5_000m, AnnualLedgerEntryCategory.Wage),
            Entry(2026, 4, 1, -500m, AnnualLedgerEntryCategory.Wage),
            Entry(2026, 5, 2, 800m, AnnualLedgerEntryCategory.Expense),
            Entry(2026, 6, 3, 300m, AnnualLedgerEntryCategory.OtherPayable),
            Entry(2026, 7, 4, -100m, AnnualLedgerEntryCategory.Adjustment)
        };
        var receipts = new[]
        {
            Receipt(2026, 2, 20, 200m),
            Receipt(2026, 8, 1, 6_500m)
        };

        var summary = EmployeeAnnualLedgerCalculator.Calculate(yearStart, yearEnd, entries, receipts);

        summary.PriorYearCarryForward.Should().Be(800m);
        summary.CurrentYearWagePayable.Should().Be(4_500m);
        summary.ExpensePayable.Should().Be(800m);
        summary.OtherPayable.Should().Be(300m);
        summary.AdjustmentAmount.Should().Be(-100m);
        summary.CurrentYearNewPayable.Should().Be(5_500m);
        summary.ReceivedAmount.Should().Be(6_500m);
        summary.CurrentBalance.Should().Be(-200m);
        summary.IsOverpaid.Should().BeTrue();
    }

    [Fact]
    public void ReceiptsAllocateToOldestPositivePayableFirst()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var receipt = Guid.NewGuid();
        var entries = new[]
        {
            new AnnualLedgerPayableInput(first, AnnualLedgerEntryCategory.Wage, new DateOnly(2026, 1, 5), 600m),
            new AnnualLedgerPayableInput(second, AnnualLedgerEntryCategory.Expense, new DateOnly(2026, 1, 10), 500m)
        };
        var receipts = new[]
        {
            new AnnualLedgerReceiptInput(receipt, new DateOnly(2026, 1, 20), 800m)
        };

        var summary = EmployeeAnnualLedgerCalculator.Calculate(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            entries,
            receipts);

        summary.ReceiptAllocations.Should().ContainInOrder(
            new AnnualLedgerReceiptAllocation(receipt, first, 600m),
            new AnnualLedgerReceiptAllocation(receipt, second, 200m));
    }

    [Fact]
    public void DeductionUsesPositiveQuantityAndPriceButProducesNegativeAmount()
    {
        var amount = EmployeeAnnualLedgerCalculator.CalculateWageAmount(
            PayrollItemNature.Deduction,
            quantity: 2m,
            unitPrice: 150m,
            manualAmount: null,
            adjustmentAmount: 20m);

        amount.AutomaticAmount.Should().Be(-300m);
        amount.FinalAmount.Should().Be(-280m);
    }

    [Fact]
    public void OppositeAdjustmentReversesOriginalAdjustment()
    {
        var entries = new[]
        {
            Entry(2026, 7, 1, 350m, AnnualLedgerEntryCategory.Adjustment),
            Entry(2026, 7, 2, -350m, AnnualLedgerEntryCategory.Adjustment)
        };

        var summary = EmployeeAnnualLedgerCalculator.Calculate(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            entries,
            []);

        summary.AdjustmentAmount.Should().Be(0m);
        summary.CurrentBalance.Should().Be(0m);
    }

    private static AnnualLedgerPayableInput Entry(
        int year,
        int month,
        int day,
        decimal amount,
        AnnualLedgerEntryCategory category) =>
        new(Guid.NewGuid(), category, new DateOnly(year, month, day), amount);

    private static AnnualLedgerReceiptInput Receipt(int year, int month, int day, decimal amount) =>
        new(Guid.NewGuid(), new DateOnly(year, month, day), amount);
}
