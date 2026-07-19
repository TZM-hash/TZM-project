using EngineeringManager.Domain.Finance;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class CentralLedgerCalculatorTests
{
    [Fact]
    public void CalculatesAdvanceInvoiceCashSeparatelyFromOutstandingSettlement()
    {
        var result = CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(
            GrossSettlementAmount: 1_000_000m,
            Deductions: 100_000m,
            InvoiceReducingDeductions: 0m,
            BaseInvoiceAmount: 1_000_000m,
            InvoicedAmount: 600_000m,
            CashAmount: 800_000m));

        result.ActualAmount.Should().Be(900_000m);
        result.ShouldInvoiceAmount.Should().Be(1_000_000m);
        result.UncollectedOrUnpaid.Should().Be(100_000m);
        result.Uninvoiced.Should().Be(400_000m);
        result.InvoicedAndCollectedOrPaid.Should().Be(600_000m);
        result.InvoicedAndUncollectedOrUnpaid.Should().Be(0m);
        result.AdvanceInvoiceCash.Should().Be(200_000m);
        result.UninvoicedAndUncollectedOrUnpaid.Should().Be(100_000m);
        result.OverSettlementCash.Should().Be(0m);
    }

    [Fact]
    public void InvoiceReducingDeductionReducesActualAndInvoiceBases()
    {
        var result = CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(
            GrossSettlementAmount: 100m,
            Deductions: 10m,
            InvoiceReducingDeductions: 10m,
            BaseInvoiceAmount: 100m,
            InvoicedAmount: 90m,
            CashAmount: 90m));

        result.ActualAmount.Should().Be(90m);
        result.ShouldInvoiceAmount.Should().Be(90m);
        result.UncollectedOrUnpaid.Should().Be(0m);
        result.Uninvoiced.Should().Be(0m);
        result.InvoicedWithoutCashRequirement.Should().Be(0m);
    }

    [Fact]
    public void DeductionCanReduceActualWithoutReducingInvoiceBase()
    {
        var result = CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(
            GrossSettlementAmount: 100m,
            Deductions: 10m,
            InvoiceReducingDeductions: 0m,
            BaseInvoiceAmount: 100m,
            InvoicedAmount: 100m,
            CashAmount: 90m));

        result.ActualAmount.Should().Be(90m);
        result.ShouldInvoiceAmount.Should().Be(100m);
        result.InvoicedAndCollectedOrPaid.Should().Be(90m);
        result.InvoicedAndUncollectedOrUnpaid.Should().Be(0m);
        result.InvoicedWithoutCashRequirement.Should().Be(10m);
    }

    [Fact]
    public void ReportsAdvanceInvoiceCashOverSettlementCashAndOverInvoiceIndependently()
    {
        var result = CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(
            GrossSettlementAmount: 100m,
            Deductions: 0m,
            InvoiceReducingDeductions: 0m,
            BaseInvoiceAmount: 80m,
            InvoicedAmount: 90m,
            CashAmount: 110m));

        result.AdvanceInvoiceCash.Should().Be(20m);
        result.OverSettlementCash.Should().Be(10m);
        result.OverInvoiced.Should().Be(10m);
    }

    [Fact]
    public void PayableDeductionRemainsSeparateFromActualCashPaid()
    {
        var result = CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(
            GrossSettlementAmount: 100m,
            Deductions: 10m,
            InvoiceReducingDeductions: 0m,
            BaseInvoiceAmount: 100m,
            InvoicedAmount: 0m,
            CashAmount: 90m));

        result.Deductions.Should().Be(10m);
        result.ActualAmount.Should().Be(90m);
        result.CashAmount.Should().Be(90m);
        result.UncollectedOrUnpaid.Should().Be(0m);
    }

    [Fact]
    public void RejectsNegativeInputComponents()
    {
        var action = () => CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(
            GrossSettlementAmount: 100m,
            Deductions: -1m,
            InvoiceReducingDeductions: 0m,
            BaseInvoiceAmount: 100m,
            InvoicedAmount: 0m,
            CashAmount: 0m));

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AggregatesAlreadyCalculatedDetailMetricsWithoutRecalculation()
    {
        var first = CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(100m, 10m, 0m, 100m, 60m, 80m));
        var second = CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(50m, 0m, 0m, 40m, 50m, 55m));

        var total = CentralLedgerCalculator.Add(first, second);

        total.GrossSettlementAmount.Should().Be(150m);
        total.Deductions.Should().Be(10m);
        total.ActualAmount.Should().Be(140m);
        total.ShouldInvoiceAmount.Should().Be(140m);
        total.InvoicedAmount.Should().Be(110m);
        total.CashAmount.Should().Be(135m);
        total.AdvanceInvoiceCash.Should().Be(first.AdvanceInvoiceCash + second.AdvanceInvoiceCash);
        total.OverSettlementCash.Should().Be(first.OverSettlementCash + second.OverSettlementCash);
        total.OverInvoiced.Should().Be(first.OverInvoiced + second.OverInvoiced);
    }
}
