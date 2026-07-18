using System.Globalization;
using EngineeringManager.Domain.Finance;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class ProjectTaxRulesTests
{
    [Theory]
    [InlineData("0.01")]
    [InlineData("0.03")]
    [InlineData("0.06")]
    [InlineData("0.09")]
    [InlineData("0.13")]
    public void ConfiguredRatesAreAllowed(string value)
    {
        ProjectTaxRules.IsAllowedRate(decimal.Parse(value, CultureInfo.InvariantCulture)).Should().BeTrue();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.05")]
    [InlineData("0.16")]
    public void OtherRatesAreRejected(string value)
    {
        ProjectTaxRules.IsAllowedRate(decimal.Parse(value, CultureInfo.InvariantCulture)).Should().BeFalse();
    }
}
