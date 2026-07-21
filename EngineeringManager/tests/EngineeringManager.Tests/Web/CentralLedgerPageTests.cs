using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class CentralLedgerPageTests
{
    [Fact]
    public void LayoutContainsOneCollapsibleCentralLedgerGroupWithTwoSecondLevelLinks()
    {
        var layout = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_Layout.cshtml");
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "collapsible-nav.js");

        Count(layout, "data-central-ledger-nav").Should().Be(1);
        layout.Should().Contain("中央账本")
            .And.Contain("asp-page=\"/Ledger/External/Index\"")
            .And.Contain("asp-page=\"/Ledger/Internal/Index\"");
        script.Should().Contain("central-ledger-nav-open")
            .And.Contain("localStorage")
            .And.Contain("data-central-ledger-nav");
    }

    [Fact]
    public void ExternalAndInternalPagesExposeConfirmedWorkbenchesAndTerminology()
    {
        var external = ReadFile("src", "EngineeringManager.Web", "Pages", "Ledger", "External", "Index.cshtml");
        var internalLedger = ReadFile("src", "EngineeringManager.Web", "Pages", "Ledger", "Internal", "Index.cshtml");
        var metrics = ReadFile("src", "EngineeringManager.Web", "Pages", "Ledger", "_LedgerMetrics.cshtml");

        external.Should().Contain("外部账本")
            .And.ContainAll("应收款", "应付款", "收款", "付款", "销项发票", "进项发票", "扣款", "待分摊", "异常", "年度账", "对账", "修改日志");
        metrics.Should().Contain("超前发票收款/付款").And.Contain("超结算收款/付款");
        internalLedger.Should().Contain("内部账本").And.Contain("自有公司").And.NotContain("合作商筛选");
    }

    [Fact]
    public void ExternalAnomalyCheckboxesUseNonNullablePagePropertiesAndPreserveNoFilterSemantics()
    {
        var pageModel = ReadFile("src", "EngineeringManager.Web", "Pages", "Ledger", "External", "Index.cshtml.cs");

        pageModel.Should().ContainAll(
                "public bool HasAdvanceInvoiceCash { get; set; }",
                "public bool HasOverSettlementCash { get; set; }",
                "public bool HasOverInvoiced { get; set; }")
            .And.ContainAll(
                "HasAdvanceInvoiceCash: HasAdvanceInvoiceCash ? true : null",
                "HasOverSettlementCash: HasOverSettlementCash ? true : null",
                "HasOverInvoiced: HasOverInvoiced ? true : null");
    }

    [Fact]
    public void FinanceYearPageExplicitlyStatesItsIndependentScope()
    {
        ReadFile("src", "EngineeringManager.Web", "Pages", "Ledger", "Years", "Index.cshtml")
            .Should().Contain("财务业务年度（仅中央账本）");
    }

    [Fact]
    public void UnifiedEntryPageContainsDeductionChoiceAllocationsAndReasonedDelete()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Ledger", "Entries", "Edit.cshtml");
        page.Should().Contain("RecordType")
            .And.Contain("Scope")
            .And.Contain("Direction")
            .And.Contain("同时扣减应开票金额")
            .And.Contain("Allocations")
            .And.Contain("分摊结算ID")
            .And.Contain("DeleteReason")
            .And.Contain("删除原因");
    }

    [Fact]
    public void ProjectFinanceEntryIsSlimmedAndProjectReceivableCannotBeCreatedManually()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var pageModel = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml.cs");
        var workspace = ReadFile("src", "EngineeringManager.Infrastructure", "Projects", "ProjectWorkspaceService.cs");
        var centralEntry = ReadFile("src", "EngineeringManager.Web", "Pages", "Ledger", "Entries", "Edit.cshtml.cs");
        var legacyEntry = ReadFile("src", "EngineeringManager.Web", "Pages", "Finance", "Entries", "Create.cshtml");
        var legacyEntryModel = ReadFile("src", "EngineeringManager.Web", "Pages", "Finance", "Entries", "Create.cshtml.cs");

        page.Should().Contain("项目内只登记销项发票")
            .And.NotContain("Enum.GetValues<InvoiceDirection>()")
            .And.NotContain("asp-page=\"/Ledger/Entries/Edit\"")
            .And.NotContain("关联应收");
        pageModel.Should().Contain("InvoiceDirection.Output, RequiredText(InvoiceEdit.InvoiceNumber")
            .And.Contain("InvoiceDirection.Output, RequiredText(FinanceRowEdit.InvoiceNumber");
        workspace.Should().Contain("item.Direction == LedgerDirection.Receivable")
            .And.Contain("item.ProjectId == projectId || item.Allocations.Any");
        centralEntry.Should().Contain("项目应收由工程量明细自动生成");
        legacyEntry.Should().NotContain("GetEnumSelectList<EngineeringManager.Application.Finance.FinanceEntryKind>()")
            .And.NotContain("<optgroup label=\"应收记录\">");
        legacyEntryModel.Should().Contain("项目应收由工程量明细自动生成");
    }

    [Fact]
    public void ReconciliationPagesStateNoLockRuleAndShowSnapshotCurrentDifference()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Ledger", "Reconciliations", "Index.cshtml");
        var details = ReadFile("src", "EngineeringManager.Web", "Pages", "Ledger", "Reconciliations", "Details.cshtml");
        index.Should().Contain("对账不锁定历史；修改后显示快照差异");
        details.Should().ContainAll("快照值", "当前值", "差异", "已物理删除");
    }

    private static int Count(string source, string value) => source.Split(value, StringSplitOptions.None).Length - 1;

    private static string ReadFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
