using EngineeringManager.Domain.Employees;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class PayrollCalculatorTests
{
    [Fact]
    public void EarningsDeductionsAndPaymentsProducePayrollSummary()
    {
        var summary = PayrollCalculator.Calculate(
            [
                new PayrollComponentInput(PayrollItemNature.Earning, 5000m),
                new PayrollComponentInput(PayrollItemNature.Earning, 800m),
                new PayrollComponentInput(PayrollItemNature.Deduction, 300m),
                new PayrollComponentInput(PayrollItemNature.Deduction, 100m)
            ],
            4000m);

        summary.GrossEarnings.Should().Be(5800m);
        summary.DeductionAmount.Should().Be(400m);
        summary.PayableAmount.Should().Be(5400m);
        summary.PaidAmount.Should().Be(4000m);
        summary.UnpaidAmount.Should().Be(1400m);
        summary.HasOverpaymentRisk.Should().BeFalse();
        summary.HasDeductionRisk.Should().BeFalse();
    }

    [Fact]
    public void OverpaymentAndExcessiveDeductionsAreFlaggedWithoutDiscardingAmounts()
    {
        var overpaid = PayrollCalculator.Calculate(
            [new PayrollComponentInput(PayrollItemNature.Earning, 5000m)],
            5200m);
        var overDeducted = PayrollCalculator.Calculate(
            [
                new PayrollComponentInput(PayrollItemNature.Earning, 1000m),
                new PayrollComponentInput(PayrollItemNature.Deduction, 1200m)
            ],
            0m);

        overpaid.UnpaidAmount.Should().Be(-200m);
        overpaid.HasOverpaymentRisk.Should().BeTrue();
        overDeducted.PayableAmount.Should().Be(-200m);
        overDeducted.HasDeductionRisk.Should().BeTrue();
    }
}
