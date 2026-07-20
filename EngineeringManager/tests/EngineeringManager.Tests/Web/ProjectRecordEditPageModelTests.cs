using System.Security.Claims;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Tests.Application;
using EngineeringManager.Web.Pages.Projects.Records;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace EngineeringManager.Tests.Web;

public sealed class ProjectRecordEditPageModelTests
{
    [Fact]
    public async Task UnassignedProjectManagerCannotEditProjectFinanceRecord()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var settlement = Settlement(fixture.Project.Id, fixture.LegalEntity.Id, fixture.Client.Id);
        fixture.Db.FinanceSettlements.Add(settlement);
        await fixture.Db.SaveChangesAsync();
        var finance = new RecordingFinanceLedgerService();
        var page = CreatePage(fixture.Db, finance, "unassigned-manager", SystemRoles.ProjectManager);
        page.ProjectId = fixture.Project.Id;
        page.Section = "collection";
        page.FinanceEdit = ReceivableInput(settlement.Id, fixture.LegalEntity.Id);

        var result = await page.OnPostFinanceAsync(CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        finance.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task ResponsibleProjectManagerCanEditProjectFinanceRecord()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        fixture.Db.Users.Add(new ApplicationUser { Id = "assigned-manager", UserName = "assigned-manager", DisplayName = "项目负责人" });
        fixture.Project.ResponsibleUserId = "assigned-manager";
        var settlement = Settlement(fixture.Project.Id, fixture.LegalEntity.Id, fixture.Client.Id);
        fixture.Db.FinanceSettlements.Add(settlement);
        await fixture.Db.SaveChangesAsync();
        var finance = new RecordingFinanceLedgerService();
        var page = CreatePage(fixture.Db, finance, "assigned-manager", SystemRoles.ProjectManager);
        page.ProjectId = fixture.Project.Id;
        page.Section = "collection";
        page.FinanceEdit = ReceivableInput(settlement.Id, fixture.LegalEntity.Id);

        var result = await page.OnPostFinanceAsync(CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>();
        finance.UpdateCount.Should().Be(1);
    }

    [Fact]
    public async Task FinanceKindMustMatchRequestedSection()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var settlement = Settlement(fixture.Project.Id, fixture.LegalEntity.Id, fixture.Client.Id);
        fixture.Db.FinanceSettlements.Add(settlement);
        await fixture.Db.SaveChangesAsync();
        var finance = new RecordingFinanceLedgerService();
        var page = CreatePage(fixture.Db, finance, "admin", SystemRoles.SystemAdministrator);
        page.ProjectId = fixture.Project.Id;
        page.Section = "invoice";
        page.FinanceEdit = ReceivableInput(settlement.Id, fixture.LegalEntity.Id);

        var result = await page.OnPostFinanceAsync(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        finance.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task EmptySectionIsRejectedWithoutInvokingFinanceUpdate()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var finance = new RecordingFinanceLedgerService();
        var page = CreatePage(fixture.Db, finance, "admin", SystemRoles.SystemAdministrator);
        page.ProjectId = fixture.Project.Id;
        page.Section = null!;
        page.FinanceEdit = ReceivableInput(Guid.NewGuid(), fixture.LegalEntity.Id);

        var result = await page.OnPostFinanceAsync(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        finance.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task FinanceRecordMustBelongToRequestedProject()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var otherProject = new Project { ProjectNumber = "OTHER", Name = "其他项目" };
        var settlement = Settlement(otherProject.Id, fixture.LegalEntity.Id, fixture.Client.Id);
        fixture.Db.AddRange(otherProject, settlement);
        await fixture.Db.SaveChangesAsync();
        var finance = new RecordingFinanceLedgerService();
        var page = CreatePage(fixture.Db, finance, "admin", SystemRoles.SystemAdministrator);
        page.ProjectId = fixture.Project.Id;
        page.Section = "collection";
        page.FinanceEdit = ReceivableInput(settlement.Id, fixture.LegalEntity.Id);

        var result = await page.OnPostFinanceAsync(CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        finance.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task CashRecordSharedAcrossProjectsCannotBeEditedFromProjectPage()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var otherProject = new Project { ProjectNumber = "OTHER", Name = "其他项目" };
        var currentSettlement = Settlement(fixture.Project.Id, fixture.LegalEntity.Id, fixture.Client.Id);
        var otherSettlement = Settlement(otherProject.Id, fixture.LegalEntity.Id, fixture.Client.Id);
        var cash = new FinanceCashEntry
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            CashType = LedgerCashType.Collection,
            LegalEntityId = fixture.LegalEntity.Id,
            BusinessPartnerId = fixture.Client.Id,
            AccountId = fixture.CollectionAccount.Id,
            BusinessDate = new DateOnly(2026, 7, 20),
            Amount = 100m
        };
        cash.Allocations.Add(new FinanceCashAllocation { CashEntry = cash, Settlement = currentSettlement, ProjectId = fixture.Project.Id, Amount = 50m, AllocationOrder = 1 });
        cash.Allocations.Add(new FinanceCashAllocation { CashEntry = cash, Settlement = otherSettlement, ProjectId = otherProject.Id, Amount = 50m, AllocationOrder = 2 });
        fixture.Db.AddRange(otherProject, currentSettlement, otherSettlement, cash);
        await fixture.Db.SaveChangesAsync();
        var finance = new RecordingFinanceLedgerService();
        var page = CreatePage(fixture.Db, finance, "finance", SystemRoles.Finance);
        page.ProjectId = fixture.Project.Id;
        page.Section = "collection";
        page.FinanceEdit = new EditModel.FinanceEditInput
        {
            Id = cash.Id,
            Kind = FinanceEntryKind.Collection,
            RelatedEntryId = currentSettlement.Id,
            LegalEntityId = fixture.LegalEntity.Id,
            AccountId = fixture.CollectionAccount.Id,
            EntryDate = cash.BusinessDate,
            Amount = cash.Amount,
            ConcurrencyStamp = cash.ConcurrencyStamp
        };

        var result = await page.OnPostFinanceAsync(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        finance.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task InvoiceSharedAcrossProjectsCannotBeEditedFromProjectPage()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var otherProject = new Project { ProjectNumber = "OTHER", Name = "其他项目" };
        var currentSettlement = Settlement(fixture.Project.Id, fixture.LegalEntity.Id, fixture.Client.Id);
        var otherSettlement = Settlement(otherProject.Id, fixture.LegalEntity.Id, fixture.Client.Id);
        var invoice = new FinanceInvoice
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            LegalEntityId = fixture.LegalEntity.Id,
            BusinessPartnerId = fixture.Client.Id,
            InvoiceNumber = "SHARED-INV",
            InvoiceDate = new DateOnly(2026, 7, 20),
            Amount = 100m,
            NetAmount = 100m,
            TaxAmount = 0m
        };
        invoice.Allocations.Add(new FinanceInvoiceAllocation { Invoice = invoice, Settlement = currentSettlement, ProjectId = fixture.Project.Id, Amount = 50m, AllocationOrder = 1 });
        invoice.Allocations.Add(new FinanceInvoiceAllocation { Invoice = invoice, Settlement = otherSettlement, ProjectId = otherProject.Id, Amount = 50m, AllocationOrder = 2 });
        fixture.Db.AddRange(otherProject, currentSettlement, otherSettlement, invoice);
        await fixture.Db.SaveChangesAsync();
        var finance = new RecordingFinanceLedgerService();
        var page = CreatePage(fixture.Db, finance, "finance", SystemRoles.Finance);
        page.ProjectId = fixture.Project.Id;
        page.Section = "invoice";
        page.FinanceEdit = new EditModel.FinanceEditInput
        {
            Id = invoice.Id,
            Kind = FinanceEntryKind.Invoice,
            LegalEntityId = fixture.LegalEntity.Id,
            ProjectTaxConfigurationId = Guid.NewGuid(),
            InvoiceNumber = invoice.InvoiceNumber,
            EntryDate = invoice.InvoiceDate,
            Amount = invoice.Amount,
            NetAmount = invoice.NetAmount ?? 0m,
            TaxAmount = invoice.TaxAmount ?? 0m,
            ConcurrencyStamp = invoice.ConcurrencyStamp
        };

        var result = await page.OnPostFinanceAsync(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        finance.UpdateCount.Should().Be(0);
    }

    private static EditModel CreatePage(ApplicationDbContext db, RecordingFinanceLedgerService finance, string userId, string role)
    {
        var services = new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton<IProjectWorkspaceService, StubProjectWorkspaceService>()
            .AddSingleton<IProjectConstructionService, StubProjectConstructionService>()
            .AddSingleton<IProjectRecordAttachmentService, StubProjectRecordAttachmentService>()
            .AddSingleton<IFinanceLedgerService>(finance)
            .BuildServiceProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId),
            new Claim(ClaimTypes.Role, role)
        ], "Test"));
        var context = new DefaultHttpContext { User = principal, RequestServices = services };
        var page = ActivatorUtilities.CreateInstance<EditModel>(services);
        page.PageContext = new PageContext { HttpContext = context };
        return page;
    }

    private static FinanceSettlement Settlement(Guid projectId, Guid legalEntityId, Guid businessPartnerId) => new()
    {
        Scope = LedgerScope.External,
        Direction = LedgerDirection.Receivable,
        SettlementState = LedgerSettlementState.Provisional,
        SourceType = LedgerSourceType.CentralLedger,
        LegalEntityId = legalEntityId,
        BusinessPartnerId = businessPartnerId,
        ProjectId = projectId,
        BusinessDate = new DateOnly(2026, 7, 20),
        OriginalAmount = 100m,
        OriginalInvoiceAmount = 100m
    };

    private static EditModel.FinanceEditInput ReceivableInput(Guid id, Guid legalEntityId) => new()
    {
        Id = id,
        Kind = FinanceEntryKind.Receivable,
        LegalEntityId = legalEntityId,
        EntryDate = new DateOnly(2026, 7, 20),
        Amount = 100m,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private sealed class StubProjectWorkspaceService : IProjectWorkspaceService
    {
        public Task<ProjectWorkspaceDto?> GetAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<ProjectWorkspaceDto?>(null);
        public Task<ProjectEditOptionsDto> GetEditOptionsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProjectWorkspaceDto> UpdateAsync(ProjectWorkspaceActor actor, UpdateProjectRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubProjectConstructionService : IProjectConstructionService
    {
        public Task<ProjectConstructionWorkspaceDto> GetWorkspaceAsync(Guid projectId, DateOnly today, CancellationToken token) => throw new NotSupportedException();
        public Task<ProjectConstructionRecordDto> SaveAsync(ProjectConstructionActor actor, SaveProjectConstructionRecordRequest request, DateOnly today, CancellationToken token) => throw new NotSupportedException();
        public Task<ProjectConstructionOptionDto> CreateEquipmentAsync(ProjectConstructionActor actor, CreateProjectEquipmentRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ProjectConstructionOptionDto> CreateCrewAsync(ProjectConstructionActor actor, CreateProjectCrewRequest request, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class StubProjectRecordAttachmentService : IProjectRecordAttachmentService
    {
        public Task<IReadOnlyList<ProjectRecordAttachmentDto>> ListAsync(Guid projectId, ProjectRecordAttachmentType recordType, Guid recordId, CancellationToken token) => throw new NotSupportedException();
        public Task<ProjectRecordAttachmentDto> UploadAsync(ProjectRecordAttachmentActor actor, ProjectRecordAttachmentUpload upload, CancellationToken token) => throw new NotSupportedException();
        public Task<ProjectRecordAttachmentFile> DownloadAsync(Guid projectId, Guid attachmentId, CancellationToken token) => throw new NotSupportedException();
        public Task DeleteAsync(ProjectRecordAttachmentActor actor, Guid projectId, Guid attachmentId, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class RecordingFinanceLedgerService : IFinanceLedgerService
    {
        public int UpdateCount { get; private set; }
        public Task UpdateReceivableAsync(FinanceRecordActor actor, UpdateReceivableRequest request, CancellationToken cancellationToken) { UpdateCount++; return Task.CompletedTask; }
        public Task UpdateCollectionAsync(FinanceRecordActor actor, UpdateCollectionRequest request, CancellationToken cancellationToken) { UpdateCount++; return Task.CompletedTask; }
        public Task UpdateInvoiceAsync(FinanceRecordActor actor, UpdateInvoiceRequest request, CancellationToken cancellationToken) { UpdateCount++; return Task.CompletedTask; }
        public Task UpdatePayableAsync(FinanceRecordActor actor, UpdatePayableRequest request, CancellationToken cancellationToken) { UpdateCount++; return Task.CompletedTask; }
        public Task UpdatePaymentAsync(FinanceRecordActor actor, UpdatePaymentRequest request, CancellationToken cancellationToken) { UpdateCount++; return Task.CompletedTask; }
        public Task<Guid> CreateAccountAsync(CreateFinancialAccountRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<FinancialAccountDto>> ListAccountsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProjectFinanceListItemDto>> ListProjectSummariesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProjectFinanceListItemDto>> ListProjectSummariesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FinanceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FinanceOverviewPageDto> SearchOverviewAsync(FinanceOverviewQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FinanceEntryOptionsDto> GetEntryOptionsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> AddReceivableAsync(CreateReceivableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordCollectionAsync(RecordCollectionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordRefundAsync(RecordRefundRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> AddPayableAsync(CreatePayableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> AddDeductionAsync(CreateDeductionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordPaymentReversalAsync(RecordPaymentReversalRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> TransferAsync(CreateAccountTransferRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> AddInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FinanceProjectSummaryDto> GetSummaryAsync(FinanceSummaryFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FinanceProjectSummaryDto> GetProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
