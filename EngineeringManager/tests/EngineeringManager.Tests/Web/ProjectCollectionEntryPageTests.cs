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
    public void CollectionDefaultPayerComesFromSelectedContractAndCanBeChanged()
    {
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml.cs");
        var workspaceService = ReadFile("src", "EngineeringManager.Infrastructure", "Projects", "ProjectWorkspaceService.cs");
        var dto = ReadFile("src", "EngineeringManager.Application", "Projects", "ProjectDtos.cs");

        model.Should().Contain("CollectionEdit.BusinessPartnerId = defaultContract?.BusinessPartnerId")
            .And.Contain("CollectionEdit.ContractId = defaultContractId");
        workspaceService.Should().Contain("ThenInclude(item => item.BusinessPartner)")
            .And.Contain("contract.BusinessPartnerId")
            .And.Contain("contract.BusinessPartner?.Name");
        dto.Should().Contain("Guid? BusinessPartnerId = null")
            .And.Contain("string? BusinessPartnerName = null");
    }

    [Fact]
    public void CollectionQuickEditUsesBatchSaveAndQuantityStyleNotesWithoutAnActionColumn()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml.cs");
        var collection = Section(page, "project-tab-panel--collection", "project-tab-panel--invoice");

        collection.Should().Contain("id=\"collection-batch-form\"")
            .And.Contain("asp-page-handler=\"Collections\"")
            .And.Contain(">保存修改</button>")
            .And.Contain("CollectionRowEdits[")
            .And.Contain("data-quantity-notes-anchor")
            .And.Contain("data-quantity-notes-edit")
            .And.Contain("data-quantity-notes-input")
            .And.Contain("data-quantity-edit-dirty")
            .And.NotContain(">操作</th>")
            .And.NotContain("asp-page-handler=\"FinanceRow\"")
            .And.NotContain("保存本行");
        model.Should().Contain("[BindProperty] public List<FinanceRowEditInput> CollectionRowEdits")
            .And.Contain("OnPostCollectionsAsync")
            .And.Contain("CollectionRowEdits.Where(item => item.IsDirty)");
    }

    [Fact]
    public void ProjectOverviewExposesCompactContractEditorAndUsesContractTotal()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml.cs");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().Contain("data-project-contracts")
            .And.Contain("data-project-contract-add")
            .And.Contain("QuickEdit.Contracts[")
            .And.Contain("合同名称")
            .And.Contain("合同金额")
            .And.Contain("合同总额");
        model.Should().Contain("List<ProjectContractQuickEditInput> Contracts")
            .And.Contain("Contracts = Workspace.Contracts")
            .And.Contain("CollectionEdit.ContractId = defaultContractId");
        styles.Should().Contain(".project-contract-editor")
            .And.Contain(".project-contract-row");
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
