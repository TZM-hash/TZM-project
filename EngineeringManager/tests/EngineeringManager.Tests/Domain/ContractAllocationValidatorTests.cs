using EngineeringManager.Domain.Projects;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class ContractAllocationValidatorTests
{
    [Fact]
    public void FixedAmountAllocationMustEqualContractAmount()
    {
        var action = () => ContractAllocationValidator.Validate(
            ContractAllocationMode.FixedAmount,
            100m,
            [new ContractAllocationInput(60m, null), new ContractAllocationInput(40m, null)]);

        action.Should().NotThrow();
    }

    [Fact]
    public void PercentageAllocationMustEqualOneHundredPercent()
    {
        var action = () => ContractAllocationValidator.Validate(
            ContractAllocationMode.Percentage,
            100m,
            [new ContractAllocationInput(null, 70m), new ContractAllocationInput(null, 30m)]);

        action.Should().NotThrow();
    }

    [Fact]
    public void InvalidAllocationThrowsClearValidationError()
    {
        var action = () => ContractAllocationValidator.Validate(
            ContractAllocationMode.FixedAmount,
            100m,
            [new ContractAllocationInput(80m, null)]);

        action.Should().Throw<ArgumentException>().WithMessage("*合同金额*");
    }
}
