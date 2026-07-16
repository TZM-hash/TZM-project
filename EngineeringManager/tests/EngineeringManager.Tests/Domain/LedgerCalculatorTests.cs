using EngineeringManager.Domain.Finance;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class LedgerCalculatorTests
{
    [Fact]
    public void CalculatesReceivableAndPayableBalances()
    {
        var summary = LedgerCalculator.Calculate(
            receivableAmount: 100m,
            collectionAmount: 70m,
            refundOrReversalAmount: 10m,
            payableAmount: 80m,
            paymentAmount: 50m,
            paymentReversalAmount: 5m,
            deductionAmount: 10m);

        summary.CollectedAmount.Should().Be(60m);
        summary.UncollectedAmount.Should().Be(40m);
        summary.PaidAmount.Should().Be(45m);
        summary.UnpaidAmount.Should().Be(25m);
        summary.HasCollectionRisk.Should().BeFalse();
        summary.HasPaymentRisk.Should().BeFalse();
    }

    [Fact]
    public void OverCollectionAndOverPaymentAreKeptAndMarkedAsRisk()
    {
        var summary = LedgerCalculator.Calculate(100m, 120m, 0m, 80m, 100m, 0m, 0m);

        summary.UncollectedAmount.Should().Be(-20m);
        summary.UnpaidAmount.Should().Be(-20m);
        summary.HasCollectionRisk.Should().BeTrue();
        summary.HasPaymentRisk.Should().BeTrue();
    }
}
