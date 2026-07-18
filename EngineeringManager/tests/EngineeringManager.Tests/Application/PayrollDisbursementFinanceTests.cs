using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class PayrollDisbursementFinanceTests
{
    [Fact]
    public async Task CrewWorkerLinesCountAsProjectPaymentWithoutSecondCashOutflow()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var company = new LegalEntity { Code = "PAY-FIN-LE", Name = "工资财务公司", ShortName = "工资公司" };
        var project = new Project { ProjectNumber = "PAY-FIN-P", Name = "工资财务项目", Stage = ProjectStage.UnderConstruction };
        var account = new FinancialAccount { LegalEntity = company, AccountName = "工资财务账户", AccountType = FinancialAccountType.Bank };
        var crew = new BusinessPartner { PartnerNumber = "PAY-FIN-C", Name = "工资财务班组", ShortName = "财务班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        var worker = new ConstructionWorker { Name = "财务班组工人" };
        worker.Memberships.Add(new ConstructionCrewMembership { Worker = worker, CrewBusinessPartner = crew, StartDate = new DateOnly(2026, 7, 1), IsPrimary = true });
        var batch = new PayrollBatch
        {
            BatchNumber = "PAY-FIN-B",
            Name = "班组工资代发",
            BatchType = PayrollBatchType.Temporary,
            StartDate = new DateOnly(2026, 7, 18),
            EndDate = new DateOnly(2026, 7, 18),
            PaymentDate = new DateOnly(2026, 7, 18),
            Project = project,
            LegalEntity = company,
            Account = account,
            ActualAmount = 4_000m,
            IsUnifiedDisbursement = true,
            Status = PayrollBatchStatus.Confirmed
        };
        batch.Payments.Add(new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.CrewWorker,
            RecipientKey = $"crew-worker:{worker.Id:N}",
            ConstructionWorker = worker,
            CrewBusinessPartner = crew,
            Amount = 4_000m,
            PayeeName = worker.Name,
            RecipientNameSnapshot = worker.Name,
            CrewNameSnapshot = crew.Name
        });
        var transaction = new AccountTransaction
        {
            Account = account,
            Direction = AccountTransactionDirection.Outflow,
            SourceType = AccountTransactionSourceType.PayrollPayment,
            SourceId = batch.Id,
            TransactionDate = batch.PaymentDate.Value,
            Amount = batch.ActualAmount
        };
        batch.AccountTransactionId = transaction.Id;
        db.AddRange(company, project, crew, worker, account, batch, transaction);
        await db.SaveChangesAsync();
        var service = new FinanceLedgerService(db);

        var projectSummary = await service.GetProjectSummaryAsync(project.Id, CancellationToken.None);
        var crewSummary = await service.GetSummaryAsync(new FinanceSummaryFilter(project.Id, BusinessPartnerId: crew.Id), CancellationToken.None);

        projectSummary.PaidAmount.Should().Be(4_000m);
        crewSummary.PaidAmount.Should().Be(4_000m);
        (await db.AccountTransactions.CountAsync()).Should().Be(1);
        (await db.PaymentEntries.CountAsync()).Should().Be(0);
    }
}
