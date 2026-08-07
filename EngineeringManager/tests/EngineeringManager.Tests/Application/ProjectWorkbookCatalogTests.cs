using EngineeringManager.Application.DataExchange;
using EngineeringManager.Infrastructure.DataExchange;
using FluentAssertions;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectWorkbookCatalogTests
{
    [Fact]
    public void CatalogDefinesStableOrderedProjectSheets()
    {
        ProjectWorkbookCatalog.Sheets.Select(item => item.Sheet).Should().Equal(
            ProjectWorkbookSheet.ProjectMaster,
            ProjectWorkbookSheet.ProjectSummary,
            ProjectWorkbookSheet.Contracts,
            ProjectWorkbookSheet.QuantityLines,
            ProjectWorkbookSheet.Milestones,
            ProjectWorkbookSheet.Assignments,
            ProjectWorkbookSheet.Partners,
            ProjectWorkbookSheet.Construction,
            ProjectWorkbookSheet.StageResults,
            ProjectWorkbookSheet.Receivables,
            ProjectWorkbookSheet.Collections,
            ProjectWorkbookSheet.Payables,
            ProjectWorkbookSheet.Payments,
            ProjectWorkbookSheet.Invoices,
            ProjectWorkbookSheet.Deductions,
            ProjectWorkbookSheet.Attachments);

        ProjectWorkbookCatalog.Sheets.Select(item => item.WorksheetName).Should().OnlyHaveUniqueItems();
        ProjectWorkbookCatalog.Get(ProjectWorkbookSheet.ProjectSummary).CanImport.Should().BeFalse();
        ProjectWorkbookCatalog.Get(ProjectWorkbookSheet.Attachments).RequiresArchive.Should().BeTrue();
        ProjectWorkbookCatalog.Get(ProjectWorkbookSheet.ProjectMaster).Fields
            .Should().Contain(item => item.Key == "responsible_employee_id" && item.CanImport);
    }

    [Fact]
    public void ImportableSheetsDeclareStableKeysAndDependencies()
    {
        var sheets = ProjectWorkbookCatalog.Sheets;
        var indexes = sheets.Select((item, index) => (item.Sheet, index)).ToDictionary(item => item.Sheet, item => item.index);

        foreach (var sheet in sheets.Where(item => item.CanImport))
        {
            sheet.Fields.Should().Contain(item => item.Key == "project_number");
            sheet.Fields.Should().Contain(item => item.Key == "_system_id" && item.IsHidden);
            sheet.Fields.Should().Contain(item => item.Key == "_project_system_id" && item.IsHidden);
            sheet.Fields.Should().Contain(item => item.Key == "_dataset_version" && item.IsHidden);
            foreach (var dependency in sheet.DependsOn)
            {
                indexes[dependency].Should().BeLessThan(indexes[sheet.Sheet]);
            }
        }
    }

    [Fact]
    public void SummaryCatalogKeepsCurrentAmountAndQuantityCalculationInputs()
    {
        var summaryFields = ProjectWorkbookCatalog.Get(ProjectWorkbookSheet.ProjectSummary).Fields;
        summaryFields.Select(item => item.Key).Should().Contain("contract_amount").And.Contain("current_project_amount")
            .And.NotContain("estimated_amount").And.NotContain("settled_amount").And.NotContain("line_item_count");

        var quantityFields = ProjectWorkbookCatalog.Get(ProjectWorkbookSheet.QuantityLines).Fields;
        quantityFields.Select(item => item.Key).Should().Contain("quantity").And.Contain("unit_price").And.Contain("accounting_label");
    }
}
