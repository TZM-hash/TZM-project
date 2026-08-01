using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class LegacyDataRepairServiceTests
{
    [Fact]
    public async Task RepairAsyncShortensLegacyNumbersWithoutChangingRelationshipsAndIsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var reservedCompany = new LegalEntity { Code = "GS0001", Name = "保留公司", ShortName = "保留公司" };
        var company = new LegalEntity { Code = "OLD-PENDING-COMPANY", Name = "待确认签约公司（旧资料补导）", ShortName = "待确认签约公司" };
        var account = new FinancialAccount
        {
            LegalEntity = company,
            AccountName = "待确认账户（旧资料补导）",
            AccountType = FinancialAccountType.Bank
        };
        var reservedPartner = new BusinessPartner { PartnerNumber = "HZ0001", Name = "保留单位", ShortName = "保留单位" };
        var partner = new BusinessPartner { PartnerNumber = "OLD-IMP-PARTNER-ABC", Name = "待确认合作单位（旧资料补导）", ShortName = "待确认合作单位" };
        var reservedProject = new Project { ProjectNumber = "XM0001", Name = "保留项目" };
        var project = new Project { ProjectNumber = "OLD-2300000000000001", Name = "历史项目", Stage = ProjectStage.UnderConstruction };
        var contract = new Contract
        {
            Project = project,
            BusinessPartner = partner,
            ContractNumber = "OLD-CONTRACT-OLD-2300000000000001",
            Name = "历史项目-原始主合同（待确认）"
        };
        var firstLine = new ContractLineItem
        {
            Contract = contract,
            Code = "OLD-LINE-100",
            Name = "待补工程量-OLD-LINE-100",
            Unit = "项",
            Quantity = 1m,
            UnitPrice = 60m,
            Notes = "工程量名称原文为空，系统暂用待补名称，待后续核实"
        };
        var secondLine = new ContractLineItem { Contract = contract, Code = "OLD-LINE-200", Name = "第二项", Unit = "项", Quantity = 1m, UnitPrice = 40m };
        contract.LineItems.Add(firstLine);
        contract.LineItems.Add(secondLine);
        project.Contracts.Add(contract);
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var reservedEmployee = new Employee { EmployeeNumber = "YG0001", Name = "保留员工", EmployeeType = EmployeeType.Formal };
        var employee = new Employee { EmployeeNumber = "OLD-EMP-330100199001010000", Name = "历史员工", EmployeeType = EmployeeType.Formal };
        var reservedInvoice = new FinanceInvoice
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            LegalEntity = company,
            BusinessPartner = partner,
            Project = project,
            Contract = contract,
            InvoiceNumber = "FP000001",
            InvoiceDate = new DateOnly(2026, 1, 1),
            Amount = 10m
        };
        var invoice = new FinanceInvoice
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            LegalEntity = company,
            BusinessPartner = partner,
            Project = project,
            Contract = contract,
            InvoiceNumber = "OLD-INV-2300000000000001",
            InvoiceDate = new DateOnly(2026, 1, 2),
            Amount = 20m
        };
        db.AddRange(reservedCompany, company, account, reservedPartner, partner, reservedProject, project, reservedEmployee, employee, reservedInvoice, invoice);
        await db.SaveChangesAsync();

        var projectId = project.Id;
        var contractId = contract.Id;
        var firstLineId = firstLine.Id;
        var secondLineId = secondLine.Id;
        var employeeId = employee.Id;
        var partnerId = partner.Id;
        var invoiceId = invoice.Id;
        var companyId = company.Id;
        var accountId = account.Id;

        var firstResult = await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);
        db.ChangeTracker.Clear();

        (await db.Projects.SingleAsync(item => item.Id == projectId)).ProjectNumber.Should().Be("XM0002");
        (await db.Contracts.SingleAsync(item => item.Id == contractId)).ContractNumber.Should().Be("XM0002-C01");
        (await db.ContractLineItems.SingleAsync(item => item.Id == firstLineId)).Code.Should().Be("QD001");
        (await db.ContractLineItems.SingleAsync(item => item.Id == secondLineId)).Code.Should().Be("QD002");
        (await db.Employees.SingleAsync(item => item.Id == employeeId)).EmployeeNumber.Should().Be("YG0002");
        (await db.BusinessPartners.SingleAsync(item => item.Id == partnerId)).PartnerNumber.Should().Be("HZ0002");
        (await db.FinanceInvoices.SingleAsync(item => item.Id == invoiceId)).InvoiceNumber.Should().Be("FP000002");
        (await db.LegalEntities.SingleAsync(item => item.Id == companyId)).Code.Should().Be("GS0002");
        (await db.Contracts.SingleAsync(item => item.Id == contractId)).Name.Should().Be("主合同（待确认）");
        (await db.ContractLineItems.SingleAsync(item => item.Id == firstLineId)).Name.Should().Be("待确认工程量1");
        (await db.BusinessPartners.SingleAsync(item => item.Id == partnerId)).Name.Should().Be("待确认合作单位");
        (await db.LegalEntities.SingleAsync(item => item.Id == companyId)).Name.Should().Be("待确认签约公司");
        (await db.FinancialAccounts.SingleAsync(item => item.Id == accountId)).AccountName.Should().Be("待确认账户");
        (await db.Contracts.SingleAsync(item => item.Id == contractId)).ProjectId.Should().Be(projectId);
        (await db.FinanceInvoices.SingleAsync(item => item.Id == invoiceId)).ProjectId.Should().Be(projectId);
        firstResult.TotalChanges.Should().Be(13);

        var secondResult = await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);

        secondResult.TotalChanges.Should().Be(0);
    }

    [Fact]
    public async Task RepairAsyncShortensGeneratedNamesAfterLegacyNumbersWereAlreadyRepaired()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var project = new Project { ProjectNumber = "XM0001", Name = "已修复项目", Stage = ProjectStage.UnderConstruction };
        var contract = new Contract
        {
            Project = project,
            ContractNumber = "XM0001-C01",
            Name = "已修复项目-原始主合同（待确认）"
        };
        contract.LineItems.Add(new ContractLineItem
        {
            Code = "QD001",
            Name = "待补工程量-OLD-LINE-100",
            Unit = "项",
            Quantity = 1m,
            UnitPrice = 60m,
            Notes = "工程量名称原文为空，系统暂用待补名称，待后续核实"
        });
        contract.LineItems.Add(new ContractLineItem
        {
            Code = "QD002",
            Name = "待补工程量-OLD-LINE-200",
            Unit = "项",
            Quantity = 1m,
            UnitPrice = 40m,
            Notes = "工程量名称原文为空，系统暂用待补名称，待后续核实"
        });
        project.Contracts.Add(contract);
        db.Add(project);
        await db.SaveChangesAsync();

        var firstResult = await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);
        db.ChangeTracker.Clear();

        (await db.Contracts.SingleAsync()).Name.Should().Be("主合同（待确认）");
        (await db.ContractLineItems.OrderBy(item => item.Code).Select(item => item.Name).ToArrayAsync())
            .Should().Equal("待确认工程量1", "待确认工程量2");
        firstResult.TotalChanges.Should().Be(3);

        var secondResult = await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);

        secondResult.TotalChanges.Should().Be(0);
    }

    [Fact]
    public async Task RepairAsyncShortensLongGeneratedPartnerNamesAfterNumbersWereAlreadyRepaired()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var originalName = "木板费用 黄玉南黄玉南330124196510310819 临安农商银行板桥支行6228580199043377588";
        db.BusinessPartners.Add(new BusinessPartner
        {
            PartnerNumber = "HZ0002",
            Name = originalName,
            ShortName = originalName,
            Notes = "从旧资料原始合作单位补建，名称后续请核实。"
        });
        await db.SaveChangesAsync();

        var firstResult = await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);
        db.ChangeTracker.Clear();

        var partner = await db.BusinessPartners.SingleAsync();
        partner.Name.Should().Be("待确认单位0002");
        partner.ShortName.Should().Be("待确认单位0002");
        firstResult.TotalChanges.Should().Be(3);

        var secondResult = await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);

        secondResult.TotalChanges.Should().Be(0);
    }

    [Fact]
    public async Task RepairAsyncShortensEveryGeneratedPartnerNameEvenWhenSourceTextIsShort()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        db.BusinessPartners.Add(new BusinessPartner
        {
            PartnerNumber = "HZ0123",
            Name = "灌桩工程量",
            ShortName = "灌桩工程量",
            Notes = "从旧资料原始合作单位补建，名称后续请核实。"
        });
        await db.SaveChangesAsync();

        var firstResult = await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);
        db.ChangeTracker.Clear();

        var partner = await db.BusinessPartners.SingleAsync();
        partner.Name.Should().Be("待确认单位0123");
        partner.ShortName.Should().Be("待确认单位0123");
        firstResult.TotalChanges.Should().Be(3);

        var secondResult = await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);

        secondResult.TotalChanges.Should().Be(0);
    }

    [Fact]
    public async Task RepairAsyncShortensGeneratedOfficialCompanyCodes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        db.LegalEntities.AddRange(
            new LegalEntity { Code = "GS0001", Name = "保留公司", ShortName = "保留公司" },
            new LegalEntity { Code = "OFFICIAL-WANGXIQIANG", Name = "杭州临安王锡强机械租赁经营部", ShortName = "王锡强" });
        await db.SaveChangesAsync();

        var firstResult = await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);
        db.ChangeTracker.Clear();

        (await db.LegalEntities.SingleAsync(item => item.ShortName == "王锡强")).Code.Should().Be("GS0002");
        firstResult.TotalChanges.Should().Be(1);

        var secondResult = await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);

        secondResult.TotalChanges.Should().Be(0);
    }

    [Fact]
    public async Task RepairAsyncDoesNotRewriteAContractNameThatDoesNotMatchItsProject()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var project = new Project { ProjectNumber = "XM0001", Name = "当前项目", Stage = ProjectStage.UnderConstruction };
        project.Contracts.Add(new Contract
        {
            Project = project,
            ContractNumber = "XM0001-C01",
            Name = "另一个项目-原始主合同（待确认）"
        });
        db.Add(project);
        await db.SaveChangesAsync();

        var result = await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);

        result.TotalChanges.Should().Be(0);
        (await db.Contracts.SingleAsync()).Name.Should().Be("另一个项目-原始主合同（待确认）");
    }

    [Fact]
    public async Task RepairAsyncDoesNotOverwriteAConfirmedPartnerNameOnRerun()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        db.BusinessPartners.Add(new BusinessPartner
        {
            PartnerNumber = "HZ0001",
            Name = "原始付款单位",
            ShortName = "原始付款单位",
            Notes = "从旧资料原始合作单位补建，名称后续请核实。"
        });
        await db.SaveChangesAsync();

        await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);
        var partner = await db.BusinessPartners.SingleAsync();
        partner.Name = "人工确认单位";
        partner.ShortName = "人工确认单位";
        await db.SaveChangesAsync();

        var result = await new LegacyDataRepairService(db).RepairAsync(CancellationToken.None);

        result.TotalChanges.Should().Be(0);
        partner = await db.BusinessPartners.SingleAsync();
        partner.Name.Should().Be("人工确认单位");
        partner.ShortName.Should().Be("人工确认单位");
    }
}
