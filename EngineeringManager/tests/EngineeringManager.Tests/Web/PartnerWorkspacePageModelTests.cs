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
using PartnerCreateModel = EngineeringManager.Web.Pages.Partners.CreateModel;

namespace EngineeringManager.Tests.Web;

public sealed class PartnerWorkspacePageModelTests
{
    [Theory]
    [InlineData(null, "施工班组|甲方单位|材料单位|未分类单位|双角色班组")]
    [InlineData(IndexModel.CrewCategory, "施工班组|双角色班组")]
    [InlineData(IndexModel.SupplierCategory, "材料单位")]
    [InlineData(IndexModel.CustomerCategory, "甲方单位")]
    public async Task CategoryFiltersAreExclusiveWithoutWritingDuringPageLoad(string? category, string expectedNames)
    {
        var service = new RecordingPartnerService(Partners());
        var model = new IndexModel(service, null!, null!, null)
        {
            Category = category,
            PageContext = AnonymousPageContext()
        };

        await model.OnGetAsync(CancellationToken.None);

        model.Partners.Select(item => item.Name).Should().Equal(expectedNames.Split('|'));
        model.CategorySummaries.Single(item => item.Category == IndexModel.CrewCategory).Count.Should().Be(2);
        model.CategorySummaries.Single(item => item.Category == IndexModel.SupplierCategory).Count.Should().Be(1);
        model.CategorySummaries.Single(item => item.Category == IndexModel.CustomerCategory).Count.Should().Be(1);
        model.CategorySummaries.Sum(item => item.Count).Should().Be(model.AllPartners.Count - 1);
        model.AllPartners.Should().ContainSingle(item => item.Name == "未分类单位");
    }

    [Fact]
    public void PartnerWorkspaceDoesNotDependOnDirectorySynchronizerForHistoricalBackfill()
    {
        typeof(IndexModel).GetConstructors().Single().GetParameters()
            .Should().NotContain(item => item.ParameterType == typeof(IBusinessPartnerDirectorySynchronizer));
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
    public async Task LegacyOtherCategoryMapsToSupplierCategory()
    {
        var model = new IndexModel(new RecordingPartnerService(Partners()), null!, null!)
        {
            Category = "other",
            PageContext = AnonymousPageContext()
        };

        await model.OnGetAsync(CancellationToken.None);

        model.Category.Should().Be(IndexModel.SupplierCategory);
        model.Partners.Select(item => item.Name).Should().Equal("材料单位");
    }

    [Fact]
    public async Task RoleSummariesFollowTheActiveCategoryInsteadOfGlobalCounts()
    {
        var model = new IndexModel(new RecordingPartnerService(Partners()), null!, null!)
        {
            Category = IndexModel.SupplierCategory,
            PageContext = AnonymousPageContext()
        };

        await model.OnGetAsync(CancellationToken.None);

        model.RoleSummaries.Single(item => item.Role == BusinessPartnerRoleType.MaterialSupplier).Count.Should().Be(1);
        model.RoleSummaries.Single(item => item.Role == BusinessPartnerRoleType.ConstructionCrew).Count.Should().Be(0);
        model.RoleSummaries.Single(item => item.Role == BusinessPartnerRoleType.CustomerOrGeneralContractor).Count.Should().Be(0);
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
                TradeCategory = "钢材",
                PricingRule = "含税到场价",
                SettlementTerms = "月结",
                ContactName = "联系人",
                ContactPhone = "13800000000",
                ContactEmail = "partner@example.com",
                ContactAddress = "测试地址",
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
        service.LastUpdate.Role.PricingRule.Should().Be("含税到场价");
        service.LastUpdate.Role.SettlementTerms.Should().Be("月结");
        service.LastUpdate.PrimaryContact!.Email.Should().Be("partner@example.com");
        service.LastUpdate.PrimaryContact.Address.Should().Be("测试地址");
        ((RedirectToPageResult)result).RouteValues!["Category"].Should().Be(IndexModel.CrewCategory);
    }

    [Fact]
    public async Task FullPageEditorPreservesRoleAndContactFieldsAndMigratesTheSelectedRole()
    {
        var partner = new BusinessPartnerDto(
            Guid.NewGuid(),
            "BP-RICH",
            "完整资料单位",
            "完整资料",
            null,
            null,
            [new PartnerRoleDto(BusinessPartnerRoleType.ConstructionCrew, "土建", "按清单计价", "月度结算")],
            [new PartnerContactDto(Guid.NewGuid(), "联系人", "13900000000", "rich@example.com", "完整地址", true, "备注")],
            0,
            true,
            Guid.NewGuid());
        var service = new RecordingPartnerService([partner]);
        var pageContext = PageContextForProjectManager();
        var model = new PartnerCreateModel(service) { PageContext = pageContext };

        (await model.OnGetAsync(partner.Id, null, CancellationToken.None)).Should().BeOfType<PageResult>();
        model.RoleType = BusinessPartnerRoleType.MaterialSupplier;
        model.Reason = "调整单位类型";

        var result = await model.OnPostAsync(CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>();
        service.LastUpdate!.PreviousRoleType.Should().Be(BusinessPartnerRoleType.ConstructionCrew);
        service.LastUpdate.Role.PricingRule.Should().Be("按清单计价");
        service.LastUpdate.Role.SettlementTerms.Should().Be("月度结算");
        service.LastUpdate.PrimaryContact!.Email.Should().Be("rich@example.com");
        service.LastUpdate.PrimaryContact.Address.Should().Be("完整地址");
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
        new BusinessPartnerDto(Guid.NewGuid(), "BP-UNCATEGORIZED", "未分类单位", "未分类", null, null, [], [], 0, true, Guid.NewGuid()),
        new BusinessPartnerDto(
            Guid.NewGuid(),
            "BP-MULTI",
            "双角色班组",
            "双角色",
            null,
            null,
            [
                new PartnerRoleDto(BusinessPartnerRoleType.ConstructionCrew, null, null, null),
                new PartnerRoleDto(BusinessPartnerRoleType.CustomerOrGeneralContractor, null, null, null)
            ],
            [],
            0,
            true,
            Guid.NewGuid())
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
