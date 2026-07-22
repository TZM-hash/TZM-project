using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Web.Pages.Employees;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class EmployeeWorkspaceClassificationTests
{
    [Fact]
    public void EmployeeWorkspaceDomainExposesConfirmedClassifications()
    {
        var domainAssembly = typeof(EmployeeType).Assembly;

        EnumNames(domainAssembly, "EngineeringManager.Domain.Employees.EmployeeWageEntryType")
            .Should().Equal("Attendance", "Overtime", "Bonus", "Penalty", "Other");
        EnumNames(domainAssembly, "EngineeringManager.Domain.Employees.PayrollDisbursementType")
            .Should().Equal("Wage", "Other");
        EnumNames(domainAssembly, "EngineeringManager.Domain.Employees.PayrollPaymentCategory")
            .Should().Equal("Wage", "Other");
        EnumNames(domainAssembly, "EngineeringManager.Domain.Employees.PayrollFundingSource")
            .Should().Equal("CompanyAccount", "PersonalAdvance");
        Enum.GetNames<FinancialAccountType>().Should().Contain("PersonalAdvance");
        Enum.GetNames<AccountTransactionSourceType>().Should().Contain("PersonalAdvanceRepayment");
    }

    [Fact]
    public void EmployeeDisplayTextUsesConfirmedChineseLabels()
    {
        var webAssembly = typeof(IndexModel).Assembly;
        var displayType = webAssembly.GetType("EngineeringManager.Web.Presentation.EmployeeDisplayText");

        displayType.Should().NotBeNull();
        InvokeLabel(displayType!, "WageEntryType", 1).Should().Be("考勤工资");
        InvokeLabel(displayType!, "WageEntryType", 2).Should().Be("加班工资");
        InvokeLabel(displayType!, "WageEntryType", 3).Should().Be("奖金");
        InvokeLabel(displayType!, "WageEntryType", 4).Should().Be("罚款");
        InvokeLabel(displayType!, "WageEntryType", 5).Should().Be("其他");
        InvokeLabel(displayType!, "DisbursementType", 1).Should().Be("工资");
        InvokeLabel(displayType!, "DisbursementType", 2).Should().Be("其他");
        InvokeLabel(displayType!, "FundingSource", 1).Should().Be("公司账户");
        InvokeLabel(displayType!, "FundingSource", 2).Should().Be("私人转账");
    }

    private static string[] EnumNames(System.Reflection.Assembly assembly, string typeName)
    {
        var type = assembly.GetType(typeName);
        type.Should().NotBeNull($"{typeName} is required by the employee workspace");
        return Enum.GetNames(type!);
    }

    private static string InvokeLabel(Type displayType, string methodName, int value)
    {
        var method = displayType.GetMethod(methodName);
        method.Should().NotBeNull();
        var enumType = method!.GetParameters().Single().ParameterType;
        return method.Invoke(null, [Enum.ToObject(enumType, value)]).Should().BeOfType<string>().Subject;
    }
}
