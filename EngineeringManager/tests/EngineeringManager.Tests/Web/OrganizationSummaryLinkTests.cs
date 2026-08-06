using EngineeringManager.Application.Organization;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Projects;
using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class OrganizationSummaryLinkTests
{
    [Fact]
    public void DrillDownLinksContainExactOwnerStageScopeSubtypeAndActiveFilters()
    {
        var companyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var partnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        OrganizationSummaryLinks.Projects(OrganizationOwnerKind.LegalEntity, companyId, ProjectStage.AwaitingMobilization, ProjectStage.UnderConstruction)
            .Should().Be($"/Projects?LegalEntityId={companyId}&Stages=1&Stages=2");
        OrganizationSummaryLinks.Projects(OrganizationOwnerKind.BusinessPartner, partnerId, ProjectStage.SettledArchived)
            .Should().Be($"/Projects?BusinessPartnerId={partnerId}&Stages=5");
        OrganizationSummaryLinks.InternalPersonnel(companyId, EmployeeType.Formal)
            .Should().Be($"/Personnel/Internal?LegalEntityId={companyId}&IsActive=true&InternalType=Formal");
        OrganizationSummaryLinks.ExternalPersonnel(partnerId, ExternalPersonnelType.ConstructionCrew)
            .Should().Be($"/Personnel/External?BusinessPartnerId={partnerId}&IsActive=true&ExternalType=ConstructionCrew");
        OrganizationSummaryLinks.InternalPersonnel(companyId, isActive: null)
            .Should().Be($"/Personnel/Internal?LegalEntityId={companyId}");
        OrganizationSummaryLinks.ExternalPersonnel(partnerId, isActive: null)
            .Should().Be($"/Personnel/External?BusinessPartnerId={partnerId}");
    }

    [Fact]
    public void AllOrganizationPagesRenderSharedSummaryAndExactDestinations()
    {
        var shared = ReadPage("Shared", "_OrganizationSummary.cshtml");
        var pages = new[]
        {
            ReadPage("Companies", "Index.cshtml"),
            ReadPage("Companies", "Details.cshtml"),
            ReadPage("Partners", "Index.cshtml"),
            ReadPage("Partners", "Details.cshtml"),
            ReadPage("Crews", "Index.cshtml"),
            ReadPage("Crews", "Details.cshtml")
        };

        pages.Should().OnlyContain(page => page.Contains("_OrganizationSummary", StringComparison.Ordinal));
        shared.Should().Contain("OrganizationSummaryLinks.Projects")
            .And.Contain("OrganizationSummaryLinks.InternalPersonnel")
            .And.Contain("OrganizationSummaryLinks.ExternalPersonnel")
            .And.Contain("项目总数")
            .And.Contain("进行中")
            .And.Contain("已结算归档")
            .And.Contain("正式员工")
            .And.Contain("特殊临时人员")
            .And.Contain("当前人员")
            .And.Contain("部门");
    }

    [Fact]
    public void ProjectWorkbenchCarriesBusinessPartnerFilterThroughQueryOptionsAndSavedViews()
    {
        var model = ReadPage("Projects", "Index.cshtml.cs");

        model.Should().Contain("BusinessPartnerId")
            .And.Contain("ExportBusinessPartnerId")
            .And.Contain("options.BusinessPartners")
            .And.Contain("new(\"BusinessPartnerId\"")
            .And.Contain("ReadString(filters, \"BusinessPartnerId\")");
    }

    private static string ReadPage(params string[] parts)
    {
        var path = Path.Combine(new[] { RepositoryRoot(), "src", "EngineeringManager.Web", "Pages" }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
