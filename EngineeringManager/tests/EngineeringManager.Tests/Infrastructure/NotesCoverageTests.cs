using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Domain.Organization;
using FluentAssertions;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class NotesCoverageTests
{
    [Theory]
    [InlineData(typeof(Employee))]
    [InlineData(typeof(Project))]
    [InlineData(typeof(Contract))]
    [InlineData(typeof(ProjectMilestone))]
    [InlineData(typeof(ProjectAssignment))]
    [InlineData(typeof(ProjectPartner))]
    [InlineData(typeof(PartnerContact))]
    [InlineData(typeof(FinancialAccount))]
    [InlineData(typeof(EquipmentSettlement))]
    public void PriorityEntitiesExposeNotes(Type entityType)
    {
        entityType.GetProperty("Notes").Should().NotBeNull($"{entityType.Name} 需要统一备注字段");
    }

    [Theory]
    [InlineData(typeof(LegalEntity))]
    [InlineData(typeof(BusinessPartner))]
    [InlineData(typeof(Equipment))]
    [InlineData(typeof(ContractLineItem))]
    [InlineData(typeof(StageResultLine))]
    [InlineData(typeof(CollectionEntry))]
    [InlineData(typeof(PaymentEntry))]
    [InlineData(typeof(EmployeeCertificate))]
    public void ExistingNotesEntitiesRemainCoveredWithoutDuplicateFields(Type entityType)
    {
        entityType.GetProperty("Notes").Should().NotBeNull();
    }

    [Fact]
    public void ExportCatalogsIncludeNotesForCoreModules()
    {
        var exportService = ReadFile("src", "EngineeringManager.Infrastructure", "DataExchange", "ExportService.cs");

        foreach (var dataset in new[] { "ProjectOverview", "Employees", "Partners", "Collections", "Payments", "Accounts", "Companies", "Equipment", "EquipmentSettlements" })
        {
            var catalogStart = exportService.IndexOf($"[ExportDataset.{dataset}]", StringComparison.Ordinal);
            catalogStart.Should().BeGreaterThanOrEqualTo(0);
            var catalogEnd = exportService.IndexOf("],", catalogStart, StringComparison.Ordinal);
            exportService[catalogStart..catalogEnd].Should().Contain("new(\"notes\", \"备注\"");
        }
    }

    [Theory]
    [InlineData("Employees", "Index.cshtml")]
    [InlineData("Projects", "Index.cshtml")]
    [InlineData("Companies", "Index.cshtml")]
    [InlineData("Partners", "Index.cshtml")]
    [InlineData("Equipment", "Index.cshtml")]
    public void MainMasterListsDisplayNotesSummaries(string module, string page)
    {
        var source = ReadFile("src", "EngineeringManager.Web", "Pages", module, page);

        source.Should().Contain("data-column-key=\"notes\"");
        source.Should().Contain(".Notes");
    }

    private static string ReadFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
