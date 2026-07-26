using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class EquipmentModelTests
{
    [Fact]
    public async Task EquipmentManagingCompanyPurchaseAndQualificationCanBePersisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var company = new LegalEntity
        {
            Code = "EQ-MANAGE",
            Name = "设备管理公司",
            ShortName = "设备管理",
            CompanyCategoryId = CompanyCategoryDefaults.OtherId
        };
        var attachment = new Attachment
        {
            StoredName = "equipment-certificate.pdf",
            OriginalFileName = "设备合格证.pdf",
            ContentType = "application/pdf",
            SizeBytes = 128
        };
        var equipment = new Equipment
        {
            EquipmentNumber = "EQ-CERT-001",
            Name = "合格证测试设备",
            OwnershipType = EquipmentOwnershipType.SelfOwned,
            ManagingLegalEntity = company,
            OwnerLegalEntity = company,
            PurchaseDate = new DateOnly(2026, 6, 1),
            PurchaseAmount = 120000m,
            QualificationCertificateNumber = "CERT-001",
            QualificationIssuedOn = new DateOnly(2026, 6, 2),
            QualificationExpiresOn = new DateOnly(2027, 6, 1),
            QualificationAttachment = attachment
        };
        db.Add(equipment);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var saved = await db.Equipment
            .Include(item => item.ManagingLegalEntity)
            .Include(item => item.QualificationAttachment)
            .SingleAsync(item => item.Id == equipment.Id);
        saved.ManagingLegalEntityId.Should().Be(company.Id);
        saved.ManagingLegalEntity!.Name.Should().Be("设备管理公司");
        saved.PurchaseDate.Should().Be(new DateOnly(2026, 6, 1));
        saved.PurchaseAmount.Should().Be(120000m);
        saved.QualificationCertificateNumber.Should().Be("CERT-001");
        saved.QualificationIssuedOn.Should().Be(new DateOnly(2026, 6, 2));
        saved.QualificationExpiresOn.Should().Be(new DateOnly(2027, 6, 1));
        saved.QualificationAttachment!.OriginalFileName.Should().Be("设备合格证.pdf");
    }

    [Fact]
    public async Task EquipmentLeaseUsagePeriodsSettlementOwnershipAndMaintenanceCanBePersisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var company = new LegalEntity { Code = "EQ-LE", Name = "设备测试公司", ShortName = "设备公司", CompanyCategoryId = CompanyCategoryDefaults.OtherId };
        var partner = new BusinessPartner { PartnerNumber = "EQ-BP", Name = "设备出租方", ShortName = "出租方" };
        partner.Roles.Add(new BusinessPartnerRole { Partner = partner, RoleType = BusinessPartnerRoleType.MiscellaneousSupplier });
        var project = new Project { ProjectNumber = "EQ-P", Name = "设备测试项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var equipment = new Equipment
        {
            EquipmentNumber = "EQ-001",
            Name = "测试挖掘机",
            Model = "TEST-100",
            Category = "挖掘机",
            OwnershipType = EquipmentOwnershipType.Rented,
            LessorBusinessPartner = partner
        };
        var lease = new EquipmentLeaseAgreement { Equipment = equipment, LessorBusinessPartner = partner, ContractNumber = "LEASE-01", StartDate = new DateOnly(2026, 7, 1), RentMode = RentMode.Daily, UnitRate = 100m };
        var usage = new EquipmentProjectUsage { Equipment = equipment, Project = project, LegalEntity = company, LeaseAgreement = lease, EntryDate = new DateOnly(2026, 7, 1), ExitDate = new DateOnly(2026, 7, 10), RentMode = RentMode.Daily, UnitRate = 100m };
        usage.Periods.Add(new EquipmentWorkPeriod { Usage = usage, StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 7, 10), PeriodType = EquipmentPeriodType.Work, IsChargeable = true });
        var settlement = new EquipmentSettlement { Usage = usage, SettlementDate = new DateOnly(2026, 7, 10), BaseAmount = 1000m, TotalAmount = 1000m, ModificationReason = "测试结算" };
        settlement.Adjustments.Add(new EquipmentSettlementAdjustment { Settlement = settlement, Direction = EquipmentAdjustmentDirection.Addition, AdjustmentType = "进退场费", Amount = 100m, Reason = "测试" });
        usage.AdvancePayments.Add(new EquipmentAdvancePayment { Usage = usage, PaymentType = EquipmentAdvancePaymentType.Deposit, PaymentDate = new DateOnly(2026, 7, 1), Amount = 500m });
        equipment.OwnershipHistory.Add(new EquipmentOwnershipHistory { Equipment = equipment, TransferType = EquipmentTransferType.ExternalSale, TransferDate = new DateOnly(2027, 1, 1), ExternalRecipientName = "测试接收方" });
        equipment.MaintenanceRecords.Add(new EquipmentMaintenanceRecord { Equipment = equipment, MaintenanceType = "保养", MaintenanceDate = new DateOnly(2026, 8, 1) });

        db.AddRange(company, partner, project, equipment, lease, usage, settlement);
        await db.SaveChangesAsync();

        (await db.Equipment.SingleAsync()).EquipmentNumber.Should().Be("EQ-001");
        (await db.EquipmentWorkPeriods.SingleAsync()).PeriodType.Should().Be(EquipmentPeriodType.Work);
        (await db.EquipmentSettlements.SingleAsync()).TotalAmount.Should().Be(1000m);
        (await db.EquipmentMaintenanceRecords.SingleAsync()).MaintenanceType.Should().Be("保养");
    }

    [Fact]
    public async Task EquipmentNumberAndSettlementPerUsageAreUnique()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        db.Equipment.AddRange(
            new Equipment { EquipmentNumber = "DUP", Name = "设备一", OwnershipType = EquipmentOwnershipType.SelfOwned },
            new Equipment { EquipmentNumber = "DUP", Name = "设备二", OwnershipType = EquipmentOwnershipType.SelfOwned });

        var action = () => db.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
}
