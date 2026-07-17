using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Partners;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class BusinessPartnerServiceTests
{
    [Fact]
    public async Task OnePartnerCanHaveMultipleRolesWithoutDuplicateMasterRecords()
    {
        await using var fixture = await PartnerFixture.CreateAsync();

        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-SVC-01",
                "综合合作单位",
                "综合单位",
                null,
                "测试单位",
                [
                    new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, "土建", "工程量计价", null),
                    new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, "辅材", "含税到场价", null)
                ],
                [new PartnerContactRequest("联系人", "13800000000", null, null, true)]),
            CancellationToken.None);

        partner.Roles.Should().HaveCount(2);
        (await fixture.Db.BusinessPartners.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DuplicatePartnerNumberIsRejected()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var request = new CreateBusinessPartnerRequest("BP-DUP", "单位一", "单位一", null, null, [], []);
        await fixture.Service.CreateAsync(request, CancellationToken.None);

        var action = () => fixture.Service.CreateAsync(request with { Name = "单位二" }, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*合作单位编号*");
    }

    [Fact]
    public async Task CopyKeepsReusableRoleSettingsButClearsContactsAndHistory()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var source = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-COPY-SRC",
                "原施工班组",
                "原班组",
                "913000000000000001",
                "常用班组",
                [new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, "安装", "按清单计价", "月度结算")],
                [new PartnerContactRequest("原联系人", "13900000000", null, null, true)]),
            CancellationToken.None);

        var copy = await fixture.Service.CopyAsync(
            new CopyBusinessPartnerRequest(source.Id, "BP-COPY-NEW", "新施工班组", "新班组"),
            CancellationToken.None);

        copy.Roles.Should().ContainSingle().Which.TradeCategory.Should().Be("安装");
        copy.Contacts.Should().BeEmpty();
        copy.UnifiedSocialCreditCode.Should().BeNull();
        copy.ProjectCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateChangesMasterDataAndPreservesAuditTrail()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(new CreateBusinessPartnerRequest("BP-UPD", "原单位", "原单位", null, null, [new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, "土建", null, null)], []), CancellationToken.None);

        var updated = await fixture.Service.UpdateAsync("admin", new UpdateBusinessPartnerRequest(partner.Id, partner.PartnerNumber, "修改后单位", "修改后", null, "更新备注", new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, "材料", null, null), new PartnerContactRequest("新联系人", "13800000002", null, null, true), true, partner.ConcurrencyStamp, "维护合作单位"), CancellationToken.None);

        updated.Name.Should().Be("修改后单位");
        updated.Roles.Should().Contain(item => item.RoleType == BusinessPartnerRoleType.ConstructionCrew);
        updated.Roles.Should().Contain(item => item.RoleType == BusinessPartnerRoleType.MaterialSupplier);
        (await fixture.Db.AuditLogs.SingleAsync()).Action.Should().Be("UpdateBusinessPartner");
    }

    private sealed class PartnerFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private PartnerFixture(SqliteConnection connection, ApplicationDbContext db, IBusinessPartnerService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IBusinessPartnerService Service { get; }

        public static async Task<PartnerFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new PartnerFixture(connection, db, new BusinessPartnerService(db));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
