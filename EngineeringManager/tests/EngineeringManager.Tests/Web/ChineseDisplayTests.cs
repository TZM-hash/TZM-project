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
    public void ContractSigningStatusIncludesNoContractOption()
    {
        Enum.GetNames<ContractSigningStatus>().Should().Contain("NoContract");
        Enum.TryParse<ContractSigningStatus>("NoContract", out var value).Should().BeTrue();
        value.ToChinese().Should().Be("不签合同");
    }

    [Fact]
    public void EmployeeAndProjectEditorsExposeProjectResponsibleSelection()
    {
        var root = RepositoryRoot();
        var employeeEditor = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Employees", "_EmployeeEditor.cshtml"));
        var employeeDetails = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Employees", "Details.cshtml"));
        var projectEdit = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Edit.cshtml"));
        var projectDetails = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml"));

        employeeEditor.Should().Contain("是否作为项目负责人");
        employeeDetails.Should().Contain("是否作为项目负责人");
        projectEdit.Should().Contain("ResponsibleEmployees");
        projectDetails.Should().Contain("ResponsibleEmployees");
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

    [Fact]
    public void ProjectDisplayFormatsUnknownDatesAndImportedPaymentMethodsInChinese()
    {
        ProjectDisplayText.DateLabel(DateOnly.MinValue).Should().Be("日期待确认");
        ProjectDisplayText.DateLabel(new DateOnly(2026, 8, 1)).Should().Be("2026-08-01");
        var activityDateLabel = typeof(ProjectDisplayText).GetMethod("ActivityDateLabel", [typeof(DateTimeOffset)]);
        activityDateLabel.Should().NotBeNull();
        activityDateLabel!.Invoke(null, [DateTimeOffset.MinValue]).Should().Be("日期待确认");
        ProjectDisplayText.PaymentMethodLabel(nameof(PaymentMethod.BankTransfer)).Should().Be("银行转账");
        ProjectDisplayText.InvoiceTypeLabel(nameof(InvoiceDirection.Output)).Should().Be("销项发票");
        ProjectDisplayText.InvoiceTypeLabel(nameof(InvoiceDirection.Input)).Should().Be("进项发票");
    }

    [Fact]
    public void ProjectDetailsShowCompleteFinanceAmountsAndContainLongTableValues()
    {
        var root = RepositoryRoot();
        var details = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml"));
        var pagesCss = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "wwwroot", "css", "pages.css"));

        details.Should().Contain("<span>应收金额</span>")
            .And.Contain("<span>未开票</span>")
            .And.Contain("ProjectDisplayText.DateLabel(row.CollectionDate)")
            .And.Contain("ProjectDisplayText.DateLabel(row.InvoiceDate)")
            .And.Contain("ProjectDisplayText.PaymentMethodLabel(row.PaymentMethod)")
            .And.Contain("ProjectDisplayText.ActivityDateLabel(activity.OccurredAt)")
            .And.Contain("row.CollectionDate == DateOnly.MinValue")
            .And.Contain("row.InvoiceDate == DateOnly.MinValue")
            .And.Contain("row.EntryDate == DateOnly.MinValue")
            .And.Contain("row.PaymentDate == DateOnly.MinValue")
            .And.Contain("project-detail-inline-table");
        pagesCss.Should().Contain(".table-wrap > table.project-detail-inline-table th, .table-wrap > table.project-detail-inline-table td")
            .And.Contain("white-space: normal")
            .And.Contain(".table-wrap > table.quantity-inline-table { min-width: 74rem; }")
            .And.Contain(".table-wrap > table.collection-inline-table { min-width: 78rem; }")
            .And.Contain(".table-wrap > table.invoice-inline-table { min-width: 86rem; }")
            .And.Contain(".table-wrap > table.payment-inline-table { min-width: 78rem; }")
            .And.Contain(".table-wrap > table.construction-inline-table { min-width: 90rem; }")
            .And.Contain(".table-wrap > table.invoice-inline-table :is(th, td):nth-child(1)")
            .And.Contain(".table-wrap > table.payment-record-inline-table :is(th, td):nth-child(1)");
    }

    [Fact]
    public void CopyEntryPointsUseStandardShortNumbersAndCompactChineseNames()
    {
        var root = RepositoryRoot();
        var projectEdit = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Edit.cshtml.cs"));
        var sources = new[]
        {
            Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Employees", "Create.cshtml.cs"),
            Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Partners", "Create.cshtml.cs"),
            Path.Combine(root, "src", "EngineeringManager.Web", "wwwroot", "js", "pages", "employee-workspace.js"),
            Path.Combine(root, "src", "EngineeringManager.Web", "wwwroot", "js", "pages", "partner-workspace.js"),
            Path.Combine(root, "src", "EngineeringManager.Web", "wwwroot", "js", "pages", "crew-workspace.js"),
            Path.Combine(root, "src", "EngineeringManager.Infrastructure", "Companies", "CompanyManagementService.cs"),
            Path.Combine(root, "src", "EngineeringManager.Infrastructure", "Equipment", "EquipmentService.cs"),
            Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Equipment", "EquipmentEditorInput.cs"),
            Path.Combine(root, "src", "EngineeringManager.Web", "wwwroot", "js", "pages", "company-workspace.js"),
            Path.Combine(root, "src", "EngineeringManager.Web", "wwwroot", "js", "pages", "equipment-workspace.js")
        }.Select(File.ReadAllText).Prepend(projectEdit).ToArray();

        sources.Should().OnlyContain(source => !source.Contains("-COPY", StringComparison.Ordinal));
        sources.Should().OnlyContain(source => !source.Contains(" - 副本", StringComparison.Ordinal));
        sources.Should().Contain(source => source.Contains("ShortBusinessNumber.Next", StringComparison.Ordinal));
        sources.Should().Contain(source => source.Contains("nextEmployeeNumber", StringComparison.Ordinal));
        sources.Should().Contain(source => source.Contains("nextPartnerNumber", StringComparison.Ordinal));
        projectEdit.Should().Contain("db.Projects.AsNoTracking()");
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
