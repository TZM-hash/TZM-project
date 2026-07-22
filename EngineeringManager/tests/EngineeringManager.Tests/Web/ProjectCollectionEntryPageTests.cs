using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ProjectCollectionEntryPageTests
{
    [Fact]
    public void CollectionCreationUsesQuantityStyleRowsAndMovesDateAndAttachment()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().Contain("class=\"inline-edit-panel collection-entry-compact-form\"")
            .And.Contain("class=\"collection-entry-row collection-entry-row--1\"")
            .And.Contain("class=\"collection-entry-row collection-entry-row--2\"")
            .And.Contain("class=\"collection-entry-row collection-entry-row--3\"")
            .And.Contain("data-collection-contract")
            .And.Contain("data-collection-payer")
            .And.Contain("data-business-partner-id")
            .And.Contain("<th>收款方式</th><th>附件</th><th>备注</th>");

        var accountIndex = page.IndexOf("asp-for=\"CollectionEdit.AccountId\"", StringComparison.Ordinal);
        var dateIndex = page.IndexOf("asp-for=\"CollectionEdit.EntryDate\"", StringComparison.Ordinal);
        accountIndex.Should().BeGreaterThanOrEqualTo(0);
        dateIndex.Should().BeGreaterThan(accountIndex);
        styles.Should().Contain(".collection-entry-row--1")
            .And.Contain(".collection-entry-row--2")
            .And.Contain(".collection-entry-row--3")
            .And.Contain(".collection-entry-compact-field--notes textarea")
            .And.Contain("min-height: 7rem")
            .And.NotContain(".collection-entry-row--1 { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(0, 1fr) minmax(18rem, 1.35fr); }");
    }

    [Fact]
    public void CollectionDefaultPayerComesFromProjectGeneralContractors()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml.cs");
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "site.js");

        page.Should().Contain("data-collection-payer-from-contractors")
            .And.Contain("collectionContractorOptions")
            .And.Contain("collectionRowContractorOptions");
        model.Should().Contain("CollectionEdit.ContractId = defaultContractId")
            .And.Contain("contractorOptions.Length == 1")
            .And.Contain("CollectionEdit.BusinessPartnerId = contractorOptions[0].Id");
        script.Should().Contain("data-collection-payer-from-contractors")
            .And.Contain("options.length === 1");
    }

    [Fact]
    public void CollectionQuickEditUsesBatchSaveAndQuantityStyleNotesWithoutAnActionColumn()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml.cs");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");
        var collection = Section(page, "project-tab-panel--collection", "project-tab-panel--invoice");

        collection.Should().Contain("id=\"collection-batch-form\"")
            .And.Contain("asp-page-handler=\"Collections\"")
            .And.Contain(">保存修改</button>")
            .And.Contain(">取消编辑</button>")
            .And.Contain("CollectionRowEdits[")
            .And.Contain("data-quantity-notes-anchor")
            .And.Contain("data-quantity-notes-edit")
            .And.Contain("data-quantity-notes-input")
            .And.Contain("data-quantity-edit-dirty")
            .And.Contain("collection-inline-table")
            .And.NotContain(">操作</th>")
            .And.NotContain("asp-page-handler=\"FinanceRow\"")
            .And.NotContain("保存本行");
        model.Should().Contain("[BindProperty] public List<FinanceRowEditInput> CollectionRowEdits")
            .And.Contain("OnPostCollectionsAsync")
            .And.Contain("CollectionRowEdits.Where(item => item.IsDirty)");
        styles.Should().Contain(".collection-inline-table [data-inline-edit-control].inline-cell-control:not([hidden]) { position: static;")
            .And.Contain(".project-tab-panel--collection .tab-panel-heading > .button-row")
            .And.Contain(".project-tab-panel--collection.is-editing [data-inline-edit-open]");
    }

    [Fact]
    public void ProjectOverviewExposesCompactContractEditorAndUsesContractTotal()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml.cs");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().Contain("data-project-contracts")
            .And.Contain("data-project-contract-add")
            .And.Contain("data-project-contract-remove")
            .And.Contain("QuickEdit.Contracts[")
            .And.Contain("合同名称")
            .And.Contain("合同金额")
            .And.Contain("合同总额")
            .And.Contain("project-summary-quarter project-contract-editor")
            .And.Contain("project-summary-quarter project-overview-equipment")
            .And.Contain("data-project-contractors")
            .And.Contain("QuickEdit.GeneralContractorNames[")
            .And.Contain("维护总包单位")
            .And.Contain("data-collection-payer-from-contractors")
            .And.Contain("ProjectGeneralContractors.Display");
        model.Should().Contain("List<ProjectContractQuickEditInput> Contracts")
            .And.Contain("Contracts = Workspace.Contracts")
            .And.Contain("CollectionEdit.ContractId = defaultContractId");
        styles.Should().Contain(".project-contract-editor")
            .And.Contain(".project-contract-row")
            .And.Contain(".project-summary-grid > .project-summary-quarter")
            .And.Contain("grid-auto-flow: dense")
            .And.Contain("grid-row: span 6")
            .And.Contain(".project-summary-metrics");
        page.Should().Contain("project-summary-metrics");
    }


    [Fact]
    public void InvoicePaymentConstructionQuickEditMatchQuantityCollectionPattern()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml.cs");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");
        var invoice = Section(page, "project-tab-panel--invoice", "project-tab-panel--payment");
        var payment = Section(page, "project-tab-panel--payment", "project-tab-panel--construction");
        var construction = Section(page, "project-tab-panel--construction", "</article>");

        foreach (var section in new[] { invoice, payment, construction })
        {
            section.Should().Contain(">保存修改</button>")
                .And.Contain(">取消编辑</button>")
                .And.Contain("data-quantity-notes-edit")
                .And.NotContain(">操作</th>")
                .And.NotContain("保存本行");
        }

        invoice.Should().Contain("id=\"invoice-batch-form\"")
            .And.Contain("InvoiceRowEdits[")
            .And.Contain("invoice-inline-table")
            .And.Contain("<th>附件</th><th class=\"quantity-notes-column\">备注</th>")
            .And.Contain("asp-for=\"InvoiceEdit.Description\"")
            .And.Contain("name=\"InvoiceRowEdits[@editIndex].Description\" value=\"@row.Notes\"")
            .And.Contain("asp-for=\"InvoiceEdit.GrossAmount\"")
            .And.NotContain("asp-for=\"InvoiceEdit.NetAmount\"")
            .And.NotContain("asp-for=\"InvoiceEdit.TaxAmount\"")
            .And.NotContain("<th>不含税</th>")
            .And.NotContain("<th>税额</th>")
            .And.Contain("<th>含税金额</th>");
        payment.Should().Contain("id=\"payment-batch-form\"")
            .And.Contain("PayableRowEdits[")
            .And.Contain("PaymentRowEdits[")
            .And.Contain("payment-inline-table")
            .And.Contain("<th>附件</th><th class=\"quantity-notes-column\">备注</th>")
            .And.Contain("isPayrollPayment")
            .And.Contain("PaymentRowEdits[@editIndex].SourceType");
        construction.Should().Contain("id=\"construction-batch-form\"")
            .And.Contain("ConstructionRowEdits[")
            .And.Contain("construction-inline-table")
            .And.Contain("<th>附件</th><th class=\"quantity-notes-column\">备注</th>");

        model.Should().Contain("OnPostInvoicesAsync")
            .And.Contain("OnPostPaymentsAsync")
            .And.Contain("OnPostConstructionsAsync")
            .And.Contain("List<FinanceRowEditInput> InvoiceRowEdits")
            .And.Contain("List<FinanceRowEditInput> PayableRowEdits")
            .And.Contain("List<FinanceRowEditInput> PaymentRowEdits")
            .And.Contain("List<ConstructionEditInput> ConstructionRowEdits")
            .And.Contain("invoiceEdit.Description")
            .And.Contain("InvoiceEdit.Description")
            .And.Contain("PayrollCrewDisbursement")
            .And.Contain("public string SourceType")
            .And.Contain("ResolveInvoiceAmounts")
            .And.Contain("SerializeGeneralContractors")
            .And.Contain("List<string> GeneralContractorNames")
            .And.Contain("contractorOptions.Length == 1");
        styles.Should().Contain(".invoice-inline-table [data-inline-edit-control].inline-cell-control:not([hidden]) { position: static;")
            .And.Contain(".payment-inline-table [data-inline-edit-control].inline-cell-control:not([hidden]) { position: static;")
            .And.Contain(".construction-inline-table [data-inline-edit-control].inline-cell-control:not([hidden]) { position: static;")
            .And.Contain(".project-tab-panel--invoice .tab-panel-heading > .button-row")
            .And.Contain(".project-tab-panel--payment .tab-panel-heading > .button-row")
            .And.Contain(".project-tab-panel--construction .tab-panel-heading > .button-row");
    }
    private static string Section(string page, string startMarker, string endMarker)
    {
        var start = page.IndexOf(startMarker, StringComparison.Ordinal);
        var end = page.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        end.Should().BeGreaterThan(start);
        return page[start..end];
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
