using System.Security.Claims;
using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Security;
using EngineeringManager.Web.Pages.Partners;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace EngineeringManager.Tests.Web;

public sealed class PartnerWorkspacePageModelTests
{
    [Theory]
    [InlineData(null, "施工班组|甲方单位|材料单位|未分类单位")]
    [InlineData(IndexModel.CrewCategory, "施工班组")]
    [InlineData(IndexModel.CustomerCategory, "甲方单位")]
    [InlineData(IndexModel.OtherCategory, "材料单位|未分类单位")]
    public async Task CategoryFiltersAreDerivedFromRolesAndSynchronizeBeforeLoading(string? category, string expectedNames)
    {
        var service = new RecordingPartnerService(Partners());
        var synchronizer = new RecordingDirectorySynchronizer();
        var model = new IndexModel(service, null!, null!, null, synchronizer)
        {
            Category = category,
            PageContext = AnonymousPageContext()
        };

        await model.OnGetAsync(CancellationToken.None);

        synchronizer.CallCount.Should().Be(1);
        model.Partners.Select(item => item.Name).Should().Equal(expectedNames.Split('|'));
        model.CategorySummaries.Single(item => item.Category == IndexModel.CrewCategory).Count.Should().Be(1);
        model.CategorySummaries.Single(item => item.Category == IndexModel.CustomerCategory).Count.Should().Be(1);
        model.CategorySummaries.Single(item => item.Category == IndexModel.OtherCategory).Count.Should().Be(2);
    }

    [Fact]
    public async Task LegacyCustomerScopeMapsToCustomerCategory()
    {
        var model = new IndexModel(new RecordingPartnerService(Partners()), null!, null!)
        {
            Scope = IndexModel.CustomerScope,
            PageContext = AnonymousPageContext()
        };

        await model.OnGetAsync(CancellationToken.None);

        model.Category.Should().Be(IndexModel.CustomerCategory);
        model.Partners.Should().ContainSingle(item => item.Name == "甲方单位");
    }

    [Fact]
    public async Task SavePassesPreviousRoleAndPreservesTheCurrentCategory()
    {
        var partner = Partners()[0];
        var service = new RecordingPartnerService([partner]);
        var pageContext = PageContextForProjectManager();
        var model = new IndexModel(service, null!, null!)
        {
            Category = IndexModel.CrewCategory,
            Editor = new IndexModel.PartnerEditorInput
            {
                Id = partner.Id,
                PartnerNumber = partner.PartnerNumber,
                Name = partner.Name,
                ShortName = partner.ShortName,
                PreviousRoleType = BusinessPartnerRoleType.ConstructionCrew,
                RoleType = BusinessPartnerRoleType.MaterialSupplier,
                IsActive = true,
                ConcurrencyStamp = partner.ConcurrencyStamp,
                Reason = "调整合作单位分类"
            },
            PageContext = pageContext,
            TempData = new TempDataDictionary(pageContext.HttpContext, new NullTempDataProvider())
        };

        var result = await model.OnPostSaveAsync(CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>();
        service.LastUpdate.Should().NotBeNull();
        service.LastUpdate!.PreviousRoleType.Should().Be(BusinessPartnerRoleType.ConstructionCrew);
        ((RedirectToPageResult)result).RouteValues!["Category"].Should().Be(IndexModel.CrewCategory);
    }

    private static PageContext AnonymousPageContext() => new()
    {
        HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
    };

    private static PageContext PageContextForProjectManager()
    {
        var identity = new ClaimsIdentity("Test", ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "partner-test"));
        identity.AddClaim(new Claim(ClaimTypes.Role, SystemRoles.ProjectManager));
        return new PageContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private static BusinessPartnerDto[] Partners() =>
    [
        Partner("BP-CREW", "施工班组", BusinessPartnerRoleType.ConstructionCrew),
        Partner("BP-CUSTOMER", "甲方单位", BusinessPartnerRoleType.CustomerOrGeneralContractor),
        Partner("BP-MATERIAL", "材料单位", BusinessPartnerRoleType.MaterialSupplier),
        new BusinessPartnerDto(Guid.NewGuid(), "BP-UNCATEGORIZED", "未分类单位", "未分类", null, null, [], [], 0, true, Guid.NewGuid())
    ];

    private static BusinessPartnerDto Partner(string number, string name, BusinessPartnerRoleType role) =>
        new(
            Guid.NewGuid(),
            number,
            name,
            name,
            null,
            null,
            [new PartnerRoleDto(role, null, null, null)],
            [],
            0,
            true,
            Guid.NewGuid());

    private sealed class RecordingDirectorySynchronizer : IBusinessPartnerDirectorySynchronizer
    {
        public int CallCount { get; private set; }

        public Task SynchronizeAsync(Guid? projectId, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPartnerService(IReadOnlyList<BusinessPartnerDto> partners) : IBusinessPartnerService
    {
        public UpdateBusinessPartnerRequest? LastUpdate { get; private set; }

        public Task<BusinessPartnerDto> CreateAsync(CreateBusinessPartnerRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BusinessPartnerDto> CopyAsync(CopyBusinessPartnerRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BusinessPartnerDto> UpdateAsync(string userId, UpdateBusinessPartnerRequest request, CancellationToken cancellationToken)
        {
            LastUpdate = request;
            return Task.FromResult(partners.Single(item => item.Id == request.Id));
        }

        public Task LinkToProjectAsync(LinkPartnerToProjectRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BusinessPartnerDto>> ListAsync(string? search, BusinessPartnerRoleType? role, CancellationToken cancellationToken) =>
            ListForManagementAsync(search, role, cancellationToken);

        public Task<IReadOnlyList<BusinessPartnerDto>> ListForManagementAsync(string? search, BusinessPartnerRoleType? role, CancellationToken cancellationToken)
        {
            var result = role.HasValue
                ? partners.Where(item => item.Roles.Any(value => value.RoleType == role.Value)).ToArray()
                : partners;
            return Task.FromResult<IReadOnlyList<BusinessPartnerDto>>(result);
        }

        public Task<BusinessPartnerDto?> GetAsync(Guid partnerId, CancellationToken cancellationToken) =>
            Task.FromResult(partners.SingleOrDefault(item => item.Id == partnerId));
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
