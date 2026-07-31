using EngineeringManager.Domain.Certificates;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Web.Presentation;
using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ChineseDisplayTests
{
    [Fact]
    public void UnknownDisplayValuesUseChineseFallbacks()
    {
        ((EmployeeType)999).ToChinese().Should().Be("未知人员类型");
        ((ProjectStage)999).ToChinese().Should().Be("未知阶段");
        InvokeDisplay(typeof(FinancialAccountType), (FinancialAccountType)999).Should().Be("未知账户类型");
        InvokeDisplay(typeof(StageResultType), (StageResultType)999).Should().Be("未知成果类型");
        InvokeDisplay(typeof(StageResultStatus), (StageResultStatus)999).Should().Be("未知状态");
        InvokeDisplay(typeof(QualityResult), (QualityResult)999).Should().Be("未知结果");
    }

    [Fact]
    public void UnknownEmployeeDisplayValuesUseChineseFallbacks()
    {
        EmployeeDisplayText.WageEntryType((EmployeeWageEntryType)999).Should().Be("未知工资明细类型");
        EmployeeDisplayText.DisbursementType((PayrollDisbursementType)999).Should().Be("未知发放类型");
        EmployeeDisplayText.PaymentCategory((PayrollPaymentCategory)999).Should().Be("未知付款类别");
        EmployeeDisplayText.FundingSource((PayrollFundingSource)999).Should().Be("未知资金来源");
        EmployeeDisplayText.WageCategory((EmployeeWageCategory)999).Should().Be("未知工资类别");
    }

    [Fact]
    public void UserFacingEnumPagesDoNotRenderRawEnglishEnumValues()
    {
        var root = RepositoryRoot();
        var dataExchange = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "DataExchange", "Index.cshtml"));
        var backups = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Backups", "Index.cshtml"));
        var accounts = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Finance", "Accounts.cshtml"));
        var stageResults = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "StageResults", "Index.cshtml"));

        dataExchange.Should().Contain("@stage.ToChinese()").And.Contain("@value.ToChinese()");
        backups.Should().NotContain("GetEnumSelectList").And.NotContain(".Status.ToString()");
        accounts.Should().NotContain("item.AccountType.ToString()");
        stageResults.Should().NotContain("@result.ResultType</td>").And.NotContain("@result.Status</td>").And.NotContain("@result.QualityResult</td>");
    }

    [Fact]
    public void CreatePagesUseExplicitChineseEnumOptions()
    {
        var root = RepositoryRoot();
        var stageCreate = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "StageResults", "Create.cshtml"));
        var stageOffline = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "StageResults", "Offline.cshtml"));
        var employeeCreate = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Employees", "Create.cshtml"));
        var partnerCreate = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Partners", "Create.cshtml"));
        var equipmentUsage = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Equipment", "Usage.cshtml"));

        stageCreate.Should().NotContain("GetEnumSelectList").And.Contain("进度").And.Contain("有条件合格");
        stageOffline.Should().NotContain("GetEnumSelectList").And.Contain("进度").And.Contain("不合格");
        employeeCreate.Should().NotContain("GetEnumSelectList").And.Contain("正式员工").And.Contain("劳务员工");
        partnerCreate.Should().NotContain("GetEnumSelectList").And.Contain("甲方/总包").And.Contain("施工班组");
        equipmentUsage.Should().NotContain("GetEnumSelectList").And.Contain("日租").And.Contain("阶段包干");
    }

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string InvokeDisplay(Type enumType, object value)
    {
        var method = typeof(ProjectDisplayText).GetMethods()
            .SingleOrDefault(item => item.Name == "ToChinese" && item.IsStatic && item.GetParameters() is [{ ParameterType: var parameterType }] && parameterType == enumType);
        method.Should().NotBeNull();
        return (string)method!.Invoke(null, [value])!;
    }
}
