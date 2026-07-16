using EngineeringManager.Domain.Organization;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class CompanyRulesTests
{
    [Fact]
    public void DefaultAccountPurposesMustBeUniquePerCompany()
    {
        var accounts = new[]
        {
            new CompanyAccountDefault(true, false, false),
            new CompanyAccountDefault(true, false, false)
        };

        Action action = () => CompanyAccountRules.Validate(accounts);

        action.Should().Throw<InvalidOperationException>().WithMessage("*默认收款账户*");
    }

    [Fact]
    public void OneAccountCanServeMultipleDefaultPurposes()
    {
        var accounts = new[]
        {
            new CompanyAccountDefault(true, true, true),
            new CompanyAccountDefault(false, false, false)
        };

        Action action = () => CompanyAccountRules.Validate(accounts);

        action.Should().NotThrow();
    }

    [Fact]
    public void CompanyCategoryKeepsHistoryWhenDisabled()
    {
        var category = new CompanyCategory
        {
            Code = "GENERAL_COMPANY",
            Name = "一般纳税人有限公司",
            IsActive = false
        };

        category.IsActive.Should().BeFalse();
        category.Code.Should().Be("GENERAL_COMPANY");
        category.Name.Should().Be("一般纳税人有限公司");
    }
}
