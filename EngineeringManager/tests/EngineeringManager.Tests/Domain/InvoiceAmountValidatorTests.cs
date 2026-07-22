using EngineeringManager.Domain.Finance;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class InvoiceAmountValidatorTests
{
    [Fact]
    public void NetAmountPlusTaxMustEqualGrossAmount()
    {
        var action = () => InvoiceAmountValidator.Validate(100m, 13m, 113m, 13m);

        action.Should().NotThrow();
    }

    [Fact]
    public void InvalidInvoiceAmountsAreRejected()
    {
        var action = () => InvoiceAmountValidator.Validate(100m, 10m, 120m, 10m);

        action.Should().Throw<ArgumentException>().WithMessage("*含税金额*");
    }

    [Fact]
    public void SplitGrossCalculatesNetAndTaxFromSelectedRate()
    {
        var split = InvoiceAmountValidator.SplitGross(113m, 0.13m);
        split.NetAmount.Should().Be(100m);
        split.TaxAmount.Should().Be(13m);
    }
}
