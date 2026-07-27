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
    [InlineData(IndexModel.CustomerScope, BusinessPartnerRoleType.MaterialSupplier)]
    [InlineData(null, BusinessPartnerRoleType.CustomerOrGeneralContractor)]
    public async Task SaveRejectsAnEditorTargetOutsideTheCurrentScope(
        string? scope,
        BusinessPartnerRoleType existingRole)
    {
        var partner = Partner(existingRole);
        var service = new RecordingPartnerService(partner);
        var model = new IndexModel(service, null!, null!)
        {
            Scope = scope,
            Editor = new IndexModel.PartnerEditorInput
            {
                Id = partner.Id,
                PartnerNumber = partner.PartnerNumber,
                Name = partner.Name,
                ShortName = partner.ShortName,
                RoleType = BusinessPartnerRoleType.MaterialSupplier,
                IsActive = true,
                ConcurrencyStamp = partner.ConcurrencyStamp,
                Reason = "测试跨范围编辑"
            },
            PageContext = PageContextForProjectManager()
        };

        var result = await model.OnPostSaveAsync(CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        model.ModelState.IsValid.Should().BeFalse();
        service.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task SaveAllowsAnInactiveEditorTargetInsideTheCurrentScope()
    {
        var partner = Partner(BusinessPartnerRoleType.MaterialSupplier, isActive: false);
        var service = new RecordingPartnerService(partner);
        var pageContext = PageContextForProjectManager();
        var model = new IndexModel(service, null!, null!)
        {
            Editor = new IndexModel.PartnerEditorInput
            {
                Id = partner.Id,
                PartnerNumber = partner.PartnerNumber,
                Name = partner.Name,
                ShortName = partner.ShortName,
                RoleType = BusinessPartnerRoleType.MaterialSupplier,
                IsActive = true,
                ConcurrencyStamp = partner.ConcurrencyStamp,
                Reason = "重新启用单位"
            },
            PageContext = pageContext,
            TempData = new TempDataDictionary(pageContext.HttpContext, new NullTempDataProvider())
        };

        var result = await model.OnPostSaveAsync(CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>();
        service.UpdateCount.Should().Be(1);
    }

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

    private static BusinessPartnerDto Partner(BusinessPartnerRoleType role, bool isActive = true) =>
        new(
            Guid.NewGuid(),
            "BP-TEST",
            "测试单位",
            "测试",
            null,
            null,
            [new PartnerRoleDto(role, null, null, null)],
            [],
            0,
            isActive,
            Guid.NewGuid());

    private sealed class RecordingPartnerService(BusinessPartnerDto existing) : IBusinessPartnerService
    {
        public int UpdateCount { get; private set; }

        public Task<BusinessPartnerDto> CreateAsync(CreateBusinessPartnerRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BusinessPartnerDto> CopyAsync(CopyBusinessPartnerRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BusinessPartnerDto> UpdateAsync(string userId, UpdateBusinessPartnerRequest request, CancellationToken cancellationToken)
        {
            UpdateCount++;
            return Task.FromResult(existing);
        }

        public Task LinkToProjectAsync(LinkPartnerToProjectRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BusinessPartnerDto>> ListAsync(string? search, BusinessPartnerRoleType? role, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BusinessPartnerDto>>([existing]);

        public Task<IReadOnlyList<BusinessPartnerDto>> ListForManagementAsync(string? search, BusinessPartnerRoleType? role, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BusinessPartnerDto>>([existing]);

        public Task<BusinessPartnerDto?> GetAsync(Guid partnerId, CancellationToken cancellationToken) =>
            Task.FromResult<BusinessPartnerDto?>(partnerId == existing.Id && existing.IsActive ? existing : null);
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
