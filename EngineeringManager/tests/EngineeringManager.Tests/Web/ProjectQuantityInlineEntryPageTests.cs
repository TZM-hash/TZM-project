using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ProjectQuantityInlineEntryPageTests
{
    [Fact]
    public void ProjectDetailsHostsQuantityCreationAndRowAttachmentControls()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");

        page.Should().Contain("<summary>新增工程量明细</summary>")
            .And.Contain("asp-page-handler=\"CreateQuantity\"")
            .And.Contain("asp-page-handler=\"QuantityAttachment\"")
            .And.Contain("asp-page-handler=\"DeleteQuantityAttachment\"")
            .And.Contain("<th class=\"quantity-upload-column\">上传</th><th class=\"quantity-attachment-column\">附件</th><th class=\"quantity-notes-column\">备注</th>")
            .And.Contain("enctype=\"multipart/form-data\"")
            .And.NotContain("/Projects/Contracts/Edit");
    }

    [Fact]
    public void QuantityTableUsesCompactColumnsAndFlexibleNotes()
    {
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        styles.Should().Contain(".quantity-inline-table")
            .And.Contain(".quantity-inline-table th, .quantity-inline-table td { text-align: left;")
            .And.Contain(".quantity-notes-column")
            .And.Contain("white-space: normal")
            .And.Contain("overflow-wrap: anywhere");
    }

    [Fact]
    public void QuantityNotesShowAnExcelStyleCommentOutsideTheScrollableTable()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().Contain("data-quantity-notes-anchor")
            .And.Contain("data-quantity-notes-content")
            .And.Contain("quantity-notes-comment")
            .And.Contain("tooltip.setAttribute(\"role\", \"tooltip\")")
            .And.Contain("document.body.append(tooltip)")
            .And.Contain("anchor.addEventListener(\"pointerenter\", showTooltip)")
            .And.Contain("anchor.addEventListener(\"focus\", showTooltip)");
        styles.Should().Contain(".quantity-notes-comment { position: fixed;")
            .And.Contain("white-space: pre-wrap")
            .And.Contain("overflow-y: auto")
            .And.Contain("background: #fff8c5");
    }

    [Fact]
    public void QuantityQuickEditUsesOneHeaderSaveButtonAndKeepsAttachmentToolsInline()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml.cs");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().Contain("class=\"quantity-attachment-tools\"")
            .And.Contain("class=\"quantity-attachment-content\"")
            .And.Contain("quantity-attachment-preview-link")
            .And.Contain("id=\"quantity-batch-form\"")
            .And.Contain("asp-page-handler=\"Quantities\"")
            .And.Contain(">保存修改</button>")
            .And.Contain("data-quantity-edit-dirty")
            .And.NotContain("asp-page-handler=\"Quantity\"");
        model.Should().Contain("[BindProperty] public List<QuantityEditInput> QuantityEdits")
            .And.Contain("OnPostQuantitiesAsync")
            .And.Contain("Where(item => item.IsDirty)");
        styles.Should().NotContain(".quantity-inline-table th[data-inline-edit-actions][hidden], .quantity-inline-table td[data-inline-edit-actions][hidden]")
            .And.Contain(".project-tab-panel--quantity.is-editing [data-inline-edit-open] { display: inline-flex;")
            .And.Contain(".project-tab-panel--quantity .tab-panel-heading > .button-row { display: flex;")
            .And.Contain(".quantity-row-upload, .quantity-attachment-tools")
            .And.Contain("flex-wrap: nowrap")
            .And.Contain(".quantity-row-upload .button, .quantity-attachment-content .button")
            .And.Contain("min-width: 3.75rem");
    }

    [Fact]
    public void QuantityCreationPlacesInvoiceBetweenContractAndAccountingWithFlexibleNotes()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        var contractIndex = page.IndexOf("asp-for=\"CreateQuantity.ContractId\"", StringComparison.Ordinal);
        var invoiceIndex = page.IndexOf("asp-for=\"CreateQuantity.RequiresInvoice\"", StringComparison.Ordinal);
        var accountingIndex = page.IndexOf("asp-for=\"CreateQuantity.AccountingLabel\"", StringComparison.Ordinal);
        contractIndex.Should().BeGreaterThanOrEqualTo(0);
        invoiceIndex.Should().BeGreaterThan(contractIndex);
        accountingIndex.Should().BeGreaterThan(invoiceIndex);
        page.Should().Contain("quantity-entry-compact-field--invoice")
            .And.Contain("quantity-entry-compact-field--accounting")
            .And.Contain("quantity-entry-compact-field--notes");
        styles.Should().Contain(".quantity-entry-row--1")
            .And.Contain(".quantity-entry-compact-field--contract")
            .And.Contain(".quantity-entry-compact-field--invoice")
            .And.Contain(".quantity-entry-compact-field--accounting")
            .And.Contain(".quantity-entry-row--3 { grid-template-columns: minmax(0, 1fr); }");
    }

    [Fact]
    public void QuantityQuickEditUsesOriginalTableCellsAndFixedColumnLayout()
    {
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        styles.Should().Contain(".quantity-inline-table { width: 100%; min-width: 82rem; table-layout: fixed; }")
            .And.Contain(".quantity-inline-table [data-inline-edit-control].inline-cell-control:not([hidden]) { position: static;")
            .And.Contain(".quantity-notes-edit-trigger");
    }

    [Fact]
    public void QuantityQuickEditUsesDialogForNotesInsteadOfASmallInlineInput()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().Contain("data-quantity-notes-dialog")
            .And.Contain("data-quantity-notes-editor")
            .And.Contain("data-quantity-notes-save")
            .And.Contain("data-quantity-notes-cancel")
            .And.Contain("data-quantity-notes-input hidden")
            .And.Contain("data-quantity-notes-edit data-inline-edit-actions hidden")
            .And.Contain("noteDialog.showModal()")
            .And.Contain("activeNoteInput.value = noteEditor.value")
            .And.Contain("dirty.value = \"true\"")
            .And.Contain("noteDialog.close()");
        styles.Should().Contain(".quantity-notes-edit-trigger")
            .And.Contain(".quantity-notes-dialog-body")
            .And.Contain(".quantity-notes-dialog textarea")
            .And.Contain("min-height: 10rem");
    }

    [Fact]
    public void QuantityAttachmentPickerOmitsEmptyHintsWithoutDisablingPreview()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "attachment-picker.js");

        page.Should().NotContain("可选，最多 20MB")
            .And.NotContain("未选择附件")
            .And.NotContain("data-attachment-empty");
        script.Should().NotContain("data-attachment-empty")
            .And.NotContain("empty.hidden");
    }

    [Fact]
    public void QuantityRowAttachmentUsesOneButtonAndUploadsImmediatelyAfterFileSelection()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "attachment-picker.js");

        page.Should().Contain("data-auto-upload-picker")
            .And.Contain("data-auto-upload-input hidden")
            .And.Contain("type=\"button\" data-auto-upload-trigger>上传附件</button>")
            .And.NotContain("type=\"submit\">上传附件</button>");
        script.Should().Contain("document.querySelectorAll(\"[data-auto-upload-picker]\")")
            .And.Contain("trigger.addEventListener(\"click\", () => input.click())")
            .And.Contain("input.addEventListener(\"change\", () =>")
            .And.Contain("if (!input.files?.length) return;")
            .And.Contain("form.requestSubmit();");
    }

    [Fact]
    public void QuantityTableScopesLayoutChangesToUploadAttachmentAndNotesColumns()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().Contain("<th class=\"quantity-upload-column\">上传</th>")
            .And.Contain("<th class=\"quantity-attachment-column\">附件</th>")
            .And.Contain("<td class=\"quantity-upload-column\">")
            .And.Contain("<td class=\"quantity-attachment-cell quantity-attachment-column\">");
        styles.Should().Contain(".quantity-upload-column { width: 4.5rem;")
            .And.Contain(".quantity-attachment-column { width: 4.5rem;")
            .And.Contain(".quantity-notes-column { width: auto;")
            .And.Contain(".quantity-row-upload { display: flex;")
            .And.Contain(".quantity-inline-table .quantity-upload-column, .quantity-inline-table .quantity-attachment-column { padding-inline: .25rem;")
            .And.Contain(".quantity-attachment-content { min-height: 2rem; flex-direction: column;")
            .And.Contain(".quantity-attachment-cell { min-width: 4.5rem;")
            .And.Contain("min-width: 3.75rem")
            .And.NotContain("grid-template-columns: minmax(9rem, 1fr) auto");
    }

    [Fact]
    public void QuantityAttachmentsDoNotExposeDescriptionOrEmptyPlaceholder()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml.cs");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().NotContain("QuantityAttachmentDescription")
            .And.NotContain("附件说明")
            .And.NotContain("暂无附件")
            .And.NotContain("table-empty-value");
        model.Should().NotContain("QuantityAttachmentDescription")
            .And.Contain("BuildQuantityUploadAsync(Guid projectId, Guid lineItemId, IFormFile file, CancellationToken cancellationToken)");
        styles.Should().Contain(".quantity-row-upload { display: flex;")
            .And.Contain(".quantity-notes-column { width: auto; min-width: 24rem;")
            .And.NotContain(".table-empty-value");
    }

    [Fact]
    public void LegacyContractEditPageIsRemoved()
    {
        var root = RepositoryRoot();

        File.Exists(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Contracts", "Edit.cshtml")).Should().BeFalse();
        File.Exists(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Contracts", "Edit.cshtml.cs")).Should().BeFalse();
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
