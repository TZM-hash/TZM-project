using System.Text.Json;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Finance;

public sealed class CentralLedgerCommandService : ICentralLedgerCommandService
{
    private static readonly AccountTransactionSourceType[] CentralCashTransactionSources =
    [
        AccountTransactionSourceType.Collection,
        AccountTransactionSourceType.Payment,
        AccountTransactionSourceType.Refund,
        AccountTransactionSourceType.PaymentReversal,
        AccountTransactionSourceType.TransferOut,
        AccountTransactionSourceType.TransferIn
    ];
    private readonly ApplicationDbContext db;
    private readonly CentralLedgerAllocationService allocationService;

    public CentralLedgerCommandService(ApplicationDbContext db)
        : this(db, new CentralLedgerAllocationService(db))
    {
    }

    public CentralLedgerCommandService(ApplicationDbContext db, CentralLedgerAllocationService allocationService)
    {
        this.db = db;
        this.allocationService = allocationService;
    }

    internal async Task ValidateImportedSettlementAsync(
        CreateSettlementRequest request,
        FinanceSettlement? existing,
        CancellationToken token)
    {
        EnsureNonNegative(request.OriginalAmount, nameof(request.OriginalAmount));
        EnsureNonNegative(request.OriginalInvoiceAmount, nameof(request.OriginalInvoiceAmount));
        EnsureInvoiceAmountNotExceedAmount(request.OriginalAmount, request.OriginalInvoiceAmount);
        EnsureDirectRecordEditable(request.SourceType, "导入结算记录");
        EnsureSourceDescriptor(request.SourceType, request.SourceId);
        await ValidateContextAsync(
            null,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.ProjectId,
            request.ContractId,
            request.ContractLineItemId,
            token);

        if (existing is null)
        {
            if (request.SourceId.HasValue && await db.FinanceSettlements.AnyAsync(
                    item => item.SourceType == request.SourceType && item.SourceId == request.SourceId,
                    token))
            {
                throw new InvalidOperationException("该业务来源已经生成中央账本结算记录。");
            }
            return;
        }

        EnsureDirectRecordEditable(existing.SourceType, "结算记录");
        EnsureActive(existing.Status, "结算记录");
        if (existing.Scope != request.Scope)
        {
            throw new InvalidOperationException("编辑时不能切换账本范围。");
        }
        if (await db.FinanceInvoiceAllocations.AnyAsync(item => item.SettlementId == existing.Id, token) ||
            await db.FinanceCashAllocations.AnyAsync(item => item.SettlementId == existing.Id, token))
        {
            throw new InvalidOperationException("结算记录已发生分摊，只能通过调整或冲销修改。");
        }
    }

    internal async Task ValidateImportedCashAsync(
        CreateFinanceCashRequest request,
        FinanceCashEntry? existing,
        CancellationToken token)
    {
        EnsurePositive(request.Amount, nameof(request.Amount));
        EnsureCashType(request.Scope, request.Direction, request.CashType);
        EnsureDirectRecordEditable(request.SourceType, "导入资金记录");
        EnsureSourceDescriptor(request.SourceType, request.SourceId);
        EnsureInternalAccounts(request.Scope, request.AccountId, request.CounterAccountId);
        await ValidateContextAsync(
            null,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.ProjectId,
            request.ContractId,
            null,
            token);
        await ValidateAccountsAsync(request.LegalEntityId, request.AccountId, request.CounterLegalEntityId, request.CounterAccountId, token);
        await ValidateAllocationsAsync(
            null,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.Amount,
            request.ProjectId,
            request.ContractId,
            request.Allocations,
            token);

        if (existing is null) return;
        EnsureDirectRecordEditable(existing.SourceType, "资金记录");
        EnsureActive(existing.Status, "资金记录");
        if (existing.Scope != request.Scope || existing.Direction != request.Direction || existing.CashType != request.CashType || existing.IsReversal)
        {
            throw new InvalidOperationException("导入记录类型与现有资金记录不一致。");
        }
    }

    internal async Task ValidateImportedInvoiceAsync(
        CreateFinanceInvoiceRequest request,
        FinanceInvoice? existing,
        CancellationToken token)
    {
        var invoiceNumber = NormalizeRequired(request.InvoiceNumber, "发票号码");
        EnsurePositive(request.Amount, nameof(request.Amount));
        ValidateInvoiceAmounts(request.Amount, request.NetAmount, request.TaxAmount, request.TaxRate);
        EnsureDirectRecordEditable(request.SourceType, "导入发票记录");
        EnsureSourceDescriptor(request.SourceType, request.SourceId);
        if (!Enum.IsDefined(request.Status)) throw new ArgumentOutOfRangeException(nameof(request), "发票状态无效。");
        await ValidateProjectTaxConfigurationAsync(request.ProjectId, request.ProjectTaxConfigurationId, request.TaxRate, token);
        await EnsureInvoiceNumberAvailableAsync(request.LegalEntityId, request.Direction, invoiceNumber, existing?.Id, token);
        await ValidateContextAsync(
            null,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.ProjectId,
            request.ContractId,
            null,
            token);
        await ValidateAllocationsAsync(
            null,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.Amount,
            request.ProjectId,
            request.ContractId,
            request.Allocations,
            token);

        if (existing is null) return;
        EnsureDirectRecordEditable(existing.SourceType, "发票记录");
        EnsureActive(existing.Status, "发票记录");
        if (existing.Scope != request.Scope)
        {
            throw new InvalidOperationException("编辑时不能切换账本范围。");
        }
    }

    internal Task ValidateImportedAllocationAsync(
        LedgerDirection direction,
        Guid legalEntityId,
        Guid? businessPartnerId,
        Guid? projectId,
        Guid? contractId,
        FinanceAllocationRequest allocation,
        CancellationToken token) =>
        ValidateAllocationsAsync(
            null,
            LedgerScope.External,
            direction,
            legalEntityId,
            businessPartnerId,
            null,
            allocation.Amount,
            projectId,
            contractId,
            [allocation],
            token);

    internal Task ValidateImportedProjectTaxConfigurationAsync(
        Guid? projectId,
        Guid? configurationId,
        decimal? requestedTaxRate,
        CancellationToken token) =>
        ValidateProjectTaxConfigurationAsync(projectId, configurationId, requestedTaxRate, token);

    internal static void EnsureImportedRecordEditable(LedgerSourceType sourceType, string label) =>
        EnsureDirectRecordEditable(sourceType, label);

    internal Task SyncImportedCashAccountTransactionsAsync(FinanceCashEntry cash, CancellationToken token) =>
        SyncAccountTransactionsAsync(cash, token);

    public async Task<Guid> CreateSettlementAsync(
        CentralLedgerActor actor,
        CreateSettlementRequest request,
        CancellationToken token)
    {
        EnsureNonNegative(request.OriginalAmount, nameof(request.OriginalAmount));
        EnsureNonNegative(request.OriginalInvoiceAmount, nameof(request.OriginalInvoiceAmount));
        EnsureInvoiceAmountNotExceedAmount(request.OriginalAmount, request.OriginalInvoiceAmount);
        EnsureSourceDescriptor(request.SourceType, request.SourceId);
        await ValidateContextAsync(
            actor,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.ProjectId,
            request.ContractId,
            request.ContractLineItemId,
            token);

        if (request.SourceId.HasValue && await db.FinanceSettlements.AnyAsync(
                item => item.SourceType == request.SourceType && item.SourceId == request.SourceId,
                token))
        {
            throw new InvalidOperationException("该业务来源已经生成中央账本结算记录。");
        }

        var settlement = new FinanceSettlement
        {
            Scope = request.Scope,
            Direction = request.Direction,
            SettlementState = request.SettlementState,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            LegalEntityId = request.LegalEntityId,
            BusinessPartnerId = request.BusinessPartnerId,
            CounterLegalEntityId = request.CounterLegalEntityId,
            ProjectId = request.ProjectId,
            ContractId = request.ContractId,
            ContractLineItemId = request.ContractLineItemId,
            BusinessDate = request.BusinessDate,
            DueDate = request.DueDate,
            SettlementDate = request.SettlementState == LedgerSettlementState.Final ? request.BusinessDate : null,
            OriginalAmount = request.OriginalAmount,
            OriginalInvoiceAmount = request.OriginalInvoiceAmount,
            Notes = NormalizeOptional(request.Notes),
            CreatedByUserId = actor.UserId
        };
        db.FinanceSettlements.Add(settlement);
        AddAudit(actor, "Create", nameof(FinanceSettlement), settlement.Id, null, SettlementSnapshot(settlement), request.ProjectId);
        await db.SaveChangesAsync(token);
        return settlement.Id;
    }

    public async Task UpdateSettlementAsync(
        CentralLedgerActor actor,
        UpdateSettlementRequest request,
        CancellationToken token)
    {
        EnsureNonNegative(request.OriginalAmount, nameof(request.OriginalAmount));
        EnsureNonNegative(request.OriginalInvoiceAmount, nameof(request.OriginalInvoiceAmount));
        var settlement = await db.FinanceSettlements
            .Include(item => item.InvoiceAllocations)
            .Include(item => item.CashAllocations)
            .SingleOrDefaultAsync(item => item.Id == request.SettlementId, token)
            ?? throw new KeyNotFoundException("中央账本结算记录不存在。");
        EnsureCanManage(actor, settlement.Scope, settlement.LegalEntityId, settlement.CounterLegalEntityId, settlement.ProjectId);
        EnsureCurrent(settlement.ConcurrencyStamp, request.ConcurrencyStamp, "结算记录");
        EnsureDirectRecordEditable(settlement.SourceType, "结算记录");
        EnsureActive(settlement.Status, "结算记录");
        EnsureNoAllocations(settlement.InvoiceAllocations.Count + settlement.CashAllocations.Count, "结算记录已发生分摊，只能通过调整或冲销修改。");
        EnsureInvoiceAmountNotExceedAmount(request.OriginalAmount, request.OriginalInvoiceAmount);
        if (request.SettlementState != settlement.SettlementState)
        {
            throw new InvalidOperationException("结算状态变更必须通过最终结算操作完成。");
        }
        if (request.Scope != settlement.Scope)
            throw new InvalidOperationException("编辑时不能切换账本范围。");

        await ValidateContextAsync(
            actor,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.ProjectId,
            request.ContractId,
            null,
            token);

        var reason = NormalizeRequired(request.Reason, "修改原因");
        var before = SettlementSnapshot(settlement);
        settlement.Direction = request.Direction;
        settlement.SettlementState = request.SettlementState;
        settlement.LegalEntityId = request.LegalEntityId;
        settlement.BusinessPartnerId = request.BusinessPartnerId;
        settlement.CounterLegalEntityId = request.CounterLegalEntityId;
        settlement.ProjectId = request.ProjectId;
        settlement.ContractId = request.ContractId;
        settlement.BusinessDate = request.BusinessDate;
        settlement.DueDate = request.DueDate;
        settlement.SettlementDate = request.SettlementState == LedgerSettlementState.Final ? request.BusinessDate : null;
        settlement.OriginalAmount = request.OriginalAmount;
        settlement.OriginalInvoiceAmount = request.OriginalInvoiceAmount;
        settlement.Notes = NormalizeOptional(request.Notes);
        settlement.UpdatedAt = DateTimeOffset.UtcNow;
        settlement.ConcurrencyStamp = Guid.NewGuid();
        AddAudit(actor, "Update", nameof(FinanceSettlement), settlement.Id, before, SettlementSnapshot(settlement), settlement.ProjectId, reason);
        await db.SaveChangesAsync(token);
    }

    public async Task FinalizeSettlementAsync(
        CentralLedgerActor actor,
        FinalizeSettlementRequest request,
        CancellationToken token)
    {
        EnsureNonNegative(request.FinalAmount, nameof(request.FinalAmount));
        EnsureNonNegative(request.FinalInvoiceAmount, nameof(request.FinalInvoiceAmount));
        var reason = NormalizeRequired(request.Reason, "最终结算原因");
        var settlement = await db.FinanceSettlements
            .Include(item => item.Adjustments)
            .SingleOrDefaultAsync(item => item.Id == request.SettlementId, token)
            ?? throw new KeyNotFoundException("中央账本结算记录不存在。");
        EnsureCanManage(actor, settlement.Scope, settlement.LegalEntityId, settlement.CounterLegalEntityId, settlement.ProjectId);
        EnsureCurrent(settlement.ConcurrencyStamp, request.ConcurrencyStamp, "结算记录");
        EnsureActive(settlement.Status, "结算记录");
        if (settlement.SettlementState == LedgerSettlementState.Final)
        {
            throw new InvalidOperationException("正式结算不能重复最终化。");
        }
        EnsureInvoiceAmountNotExceedAmount(request.FinalAmount, request.FinalInvoiceAmount);

        var before = SettlementSnapshot(settlement);
        var currentAmount = settlement.OriginalAmount + settlement.Adjustments
            .Where(item => item.Status == LedgerRecordStatus.Active)
            .Sum(item => item.AmountDelta);
        var currentInvoiceAmount = settlement.OriginalInvoiceAmount + settlement.Adjustments
            .Where(item => item.Status == LedgerRecordStatus.Active)
            .Sum(item => item.InvoiceAmountDelta);
        var adjustment = new FinanceSettlementAdjustment
        {
            Settlement = settlement,
            AdjustmentType = LedgerAdjustmentType.FinalSettlement,
            AmountDelta = request.FinalAmount - currentAmount,
            InvoiceAmountDelta = request.FinalInvoiceAmount - currentInvoiceAmount,
            BusinessDate = request.BusinessDate,
            Reason = reason,
            ActorUserId = actor.UserId,
            ActorUserName = actor.UserName
        };
        db.FinanceSettlementAdjustments.Add(adjustment);
        settlement.SettlementState = LedgerSettlementState.Final;
        settlement.SettlementDate = request.BusinessDate;
        settlement.UpdatedAt = DateTimeOffset.UtcNow;
        settlement.ConcurrencyStamp = Guid.NewGuid();
        AddAudit(actor, "Finalize", nameof(FinanceSettlement), settlement.Id, before, SettlementSnapshot(settlement), settlement.ProjectId, reason);
        await db.SaveChangesAsync(token);
    }

    public async Task<Guid> AddDeductionAsync(
        CentralLedgerActor actor,
        AddFinanceDeductionRequest request,
        CancellationToken token)
    {
        EnsurePositive(request.Amount, nameof(request.Amount));
        var reason = NormalizeRequired(request.Reason, "扣款原因");
        var settlement = await db.FinanceSettlements.SingleOrDefaultAsync(item => item.Id == request.SettlementId, token)
            ?? throw new KeyNotFoundException("中央账本结算记录不存在。");
        EnsureCanManage(actor, settlement.Scope, settlement.LegalEntityId, settlement.CounterLegalEntityId, settlement.ProjectId);
        EnsureCurrent(settlement.ConcurrencyStamp, request.SettlementConcurrencyStamp, "结算记录");
        EnsureActive(settlement.Status, "结算记录");
        var currentGross = settlement.OriginalAmount + await db.FinanceSettlementAdjustments
            .Where(item => item.SettlementId == settlement.Id && item.Status == LedgerRecordStatus.Active)
            .SumAsync(item => (decimal?)item.AmountDelta, token) ?? settlement.OriginalAmount;
        var deductedAmount = await db.FinanceDeductions
            .Where(item => item.SettlementId == settlement.Id && item.Status == LedgerRecordStatus.Active)
            .SumAsync(item => (decimal?)item.Amount, token) ?? 0m;
        if (request.Amount > Math.Max(currentGross - deductedAmount, 0m))
        {
            throw new InvalidOperationException("扣款金额不能超过当前结算可扣金额。");
        }

        var deduction = new FinanceDeduction
        {
            Settlement = settlement,
            BusinessDate = request.BusinessDate,
            Amount = request.Amount,
            ReduceInvoiceAmount = request.ReduceInvoiceAmount,
            Reason = reason,
            CreatedByUserId = actor.UserId
        };
        settlement.UpdatedAt = DateTimeOffset.UtcNow;
        settlement.ConcurrencyStamp = Guid.NewGuid();
        db.FinanceDeductions.Add(deduction);
        AddAudit(actor, "Create", nameof(FinanceDeduction), deduction.Id, null, DeductionSnapshot(deduction), settlement.ProjectId, reason);
        await db.SaveChangesAsync(token);
        return deduction.Id;
    }

    public async Task<Guid> CreateInvoiceAsync(
        CentralLedgerActor actor,
        CreateFinanceInvoiceRequest request,
        CancellationToken token)
    {
        var invoiceNumber = NormalizeRequired(request.InvoiceNumber, "发票号码");
        EnsurePositive(request.Amount, nameof(request.Amount));
        ValidateInvoiceAmounts(request.Amount, request.NetAmount, request.TaxAmount, request.TaxRate);
        if (!Enum.IsDefined(request.Status)) throw new ArgumentOutOfRangeException(nameof(request), "发票状态无效。");
        await ValidateProjectTaxConfigurationAsync(request.ProjectId, request.ProjectTaxConfigurationId, request.TaxRate, token);
        EnsureSourceDescriptor(request.SourceType, request.SourceId);
        await EnsureInvoiceNumberAvailableAsync(request.LegalEntityId, request.Direction, invoiceNumber, null, token);
        await ValidateContextAsync(
            actor,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.ProjectId,
            request.ContractId,
            null,
            token);
        var allocations = request.AutoAllocate && request.Allocations.Count == 0
            ? await allocationService.BuildAutomaticInvoiceAllocationsAsync(
                actor,
                request.Scope,
                request.Direction,
                request.LegalEntityId,
                request.BusinessPartnerId,
                request.CounterLegalEntityId,
                request.Amount,
                request.ProjectId,
                request.ContractId,
                token)
            : request.Allocations;
        var targets = await ValidateAllocationsAsync(
            actor,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.Amount,
            request.ProjectId,
            request.ContractId,
            allocations,
            token);

        var invoice = new FinanceInvoice
        {
            Scope = request.Scope,
            Direction = request.Direction,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            LegalEntityId = request.LegalEntityId,
            BusinessPartnerId = request.BusinessPartnerId,
            CounterLegalEntityId = request.CounterLegalEntityId,
            ProjectId = request.ProjectId,
            ContractId = request.ContractId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = request.InvoiceDate,
            ProjectTaxConfigurationId = request.ProjectTaxConfigurationId,
            InvoiceType = NormalizeOptional(request.InvoiceType),
            Amount = request.Amount,
            NetAmount = request.NetAmount,
            TaxAmount = request.TaxAmount,
            TaxRate = request.TaxRate,
            Notes = NormalizeOptional(request.Notes),
            Status = request.Status,
            CreatedByUserId = actor.UserId
        };
        foreach (var allocation in allocations)
        {
            var target = targets[allocation.SettlementId];
            invoice.Allocations.Add(new FinanceInvoiceAllocation
            {
                Invoice = invoice,
                Settlement = target,
                ProjectId = target.ProjectId,
                ContractId = target.ContractId,
                ContractLineItemId = target.ContractLineItemId,
                BusinessPartnerId = target.BusinessPartnerId,
                CounterLegalEntityId = target.CounterLegalEntityId,
                Amount = allocation.Amount,
                AllocationOrder = allocation.AllocationOrder
            });
        }

        db.FinanceInvoices.Add(invoice);
        AddAudit(actor, "Create", nameof(FinanceInvoice), invoice.Id, null, InvoiceSnapshot(invoice), request.ProjectId);
        await db.SaveChangesAsync(token);
        return invoice.Id;
    }

    public async Task UpdateInvoiceAsync(
        CentralLedgerActor actor,
        UpdateFinanceInvoiceRequest request,
        CancellationToken token)
    {
        var invoiceNumber = NormalizeRequired(request.InvoiceNumber, "发票号码");
        EnsurePositive(request.Amount, nameof(request.Amount));
        ValidateInvoiceAmounts(request.Amount, request.NetAmount, request.TaxAmount, request.TaxRate);
        var invoice = await db.FinanceInvoices
            .Include(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.Id == request.InvoiceId, token)
            ?? throw new KeyNotFoundException("发票记录不存在。");
        EnsureCanManage(actor, invoice.Scope, invoice.LegalEntityId, invoice.CounterLegalEntityId, invoice.ProjectId);
        EnsureCurrent(invoice.ConcurrencyStamp, request.ConcurrencyStamp, "发票记录");
        EnsureDirectRecordEditable(invoice.SourceType, "发票记录");
        EnsureActive(invoice.Status, "发票记录");
        EnsureNoAllocations(invoice.Allocations.Count, "发票已发生分摊，只能调整分摊关系，不能直接修改核心字段。");
        await EnsureInvoiceNumberAvailableAsync(request.LegalEntityId, request.Direction, invoiceNumber, invoice.Id, token);
        if (request.Scope != invoice.Scope)
            throw new InvalidOperationException("编辑时不能切换账本范围。");
        await ValidateContextAsync(
            actor,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.ProjectId,
            request.ContractId,
            null,
            token);
        await ValidateProjectTaxConfigurationAsync(request.ProjectId, invoice.ProjectTaxConfigurationId, request.TaxRate, token);

        var reason = NormalizeRequired(request.Reason, "修改原因");
        var before = InvoiceSnapshot(invoice);
        invoice.Direction = request.Direction;
        invoice.LegalEntityId = request.LegalEntityId;
        invoice.BusinessPartnerId = request.BusinessPartnerId;
        invoice.CounterLegalEntityId = request.CounterLegalEntityId;
        invoice.ProjectId = request.ProjectId;
        invoice.ContractId = request.ContractId;
        invoice.InvoiceNumber = invoiceNumber;
        invoice.InvoiceDate = request.InvoiceDate;
        invoice.Amount = request.Amount;
        invoice.NetAmount = request.NetAmount;
        invoice.TaxAmount = request.TaxAmount;
        invoice.TaxRate = request.TaxRate;
        invoice.InvoiceType = NormalizeOptional(request.InvoiceType);
        invoice.Notes = NormalizeOptional(request.Notes);
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        invoice.ConcurrencyStamp = Guid.NewGuid();
        AddAudit(actor, "Update", nameof(FinanceInvoice), invoice.Id, before, InvoiceSnapshot(invoice), request.ProjectId, reason);
        await db.SaveChangesAsync(token);
    }

    public async Task<Guid> CreateCashAsync(
        CentralLedgerActor actor,
        CreateFinanceCashRequest request,
        CancellationToken token)
    {
        EnsurePositive(request.Amount, nameof(request.Amount));
        EnsureCashType(request.Scope, request.Direction, request.CashType);
        EnsureSourceDescriptor(request.SourceType, request.SourceId);
        EnsureInternalAccounts(request.Scope, request.AccountId, request.CounterAccountId);
        await ValidateContextAsync(
            actor,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.ProjectId,
            request.ContractId,
            null,
            token);
        await ValidateAccountsAsync(request.LegalEntityId, request.AccountId, request.CounterLegalEntityId, request.CounterAccountId, token);
        var allocations = request.AutoAllocate && request.Allocations.Count == 0
            ? await allocationService.BuildAutomaticCashAllocationsAsync(
                actor,
                request.Scope,
                request.Direction,
                request.LegalEntityId,
                request.BusinessPartnerId,
                request.CounterLegalEntityId,
                request.Amount,
                request.ProjectId,
                request.ContractId,
                token)
            : request.Allocations;
        var targets = await ValidateAllocationsAsync(
            actor,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.Amount,
            request.ProjectId,
            request.ContractId,
            allocations,
            token);

        var cash = new FinanceCashEntry
        {
            Id = request.EntryId ?? Guid.NewGuid(),
            Scope = request.Scope,
            Direction = request.Direction,
            CashType = request.CashType,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            LegalEntityId = request.LegalEntityId,
            BusinessPartnerId = request.BusinessPartnerId,
            CounterLegalEntityId = request.CounterLegalEntityId,
            ProjectId = request.ProjectId,
            ContractId = request.ContractId,
            AccountId = request.AccountId,
            CounterAccountId = request.CounterAccountId,
            BusinessDate = request.BusinessDate,
            Amount = request.Amount,
            PaymentMethod = NormalizeOptional(request.PaymentMethod),
            Notes = NormalizeOptional(request.Notes),
            CreatedByUserId = actor.UserId
        };
        foreach (var allocation in allocations)
        {
            var target = targets[allocation.SettlementId];
            cash.Allocations.Add(new FinanceCashAllocation
            {
                CashEntry = cash,
                Settlement = target,
                ProjectId = target.ProjectId,
                ContractId = target.ContractId,
                ContractLineItemId = target.ContractLineItemId,
                BusinessPartnerId = target.BusinessPartnerId,
                CounterLegalEntityId = target.CounterLegalEntityId,
                Amount = allocation.Amount,
                AllocationOrder = allocation.AllocationOrder
            });
        }

        db.FinanceCashEntries.Add(cash);
        await SyncAccountTransactionsAsync(cash, token);
        AddAudit(actor, "Create", nameof(FinanceCashEntry), cash.Id, null, CashSnapshot(cash), request.ProjectId);
        await db.SaveChangesAsync(token);
        return cash.Id;
    }

    public async Task UpdateCashAsync(
        CentralLedgerActor actor,
        UpdateFinanceCashRequest request,
        CancellationToken token)
    {
        EnsurePositive(request.Amount, nameof(request.Amount));
        EnsureCashType(request.Scope, request.Direction, request.CashType);
        var cash = await db.FinanceCashEntries
            .Include(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.Id == request.CashEntryId, token)
            ?? throw new KeyNotFoundException("资金记录不存在。");
        EnsureCanManage(actor, cash.Scope, cash.LegalEntityId, cash.CounterLegalEntityId, cash.ProjectId);
        EnsureCurrent(cash.ConcurrencyStamp, request.ConcurrencyStamp, "资金记录");
        EnsureDirectRecordEditable(cash.SourceType, "资金记录");
        EnsureActive(cash.Status, "资金记录");
        EnsureInternalAccounts(request.Scope, request.AccountId, request.CounterAccountId);
        EnsureNoAllocations(cash.Allocations.Count, "资金记录已发生分摊，只能调整分摊关系，不能直接修改核心字段。");
        if (request.Scope != cash.Scope)
            throw new InvalidOperationException("编辑时不能切换账本范围。");
        await ValidateContextAsync(
            actor,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            request.ProjectId,
            request.ContractId,
            null,
            token);
        await ValidateAccountsAsync(request.LegalEntityId, request.AccountId, request.CounterLegalEntityId, request.CounterAccountId, token);

        var reason = NormalizeRequired(request.Reason, "修改原因");
        var before = CashSnapshot(cash);
        cash.Direction = request.Direction;
        cash.CashType = request.CashType;
        cash.LegalEntityId = request.LegalEntityId;
        cash.BusinessPartnerId = request.BusinessPartnerId;
        cash.CounterLegalEntityId = request.CounterLegalEntityId;
        cash.ProjectId = request.ProjectId;
        cash.ContractId = request.ContractId;
        cash.AccountId = request.AccountId;
        cash.CounterAccountId = request.CounterAccountId;
        cash.BusinessDate = request.BusinessDate;
        cash.Amount = request.Amount;
        cash.PaymentMethod = NormalizeOptional(request.PaymentMethod);
        cash.Notes = NormalizeOptional(request.Notes);
        cash.UpdatedAt = DateTimeOffset.UtcNow;
        cash.ConcurrencyStamp = Guid.NewGuid();
        await SyncAccountTransactionsAsync(cash, token);
        AddAudit(actor, "Update", nameof(FinanceCashEntry), cash.Id, before, CashSnapshot(cash), request.ProjectId, reason);
        await db.SaveChangesAsync(token);
    }

    public async Task ReplaceInvoiceAllocationsAsync(
        CentralLedgerActor actor,
        ReplaceInvoiceAllocationsRequest request,
        CancellationToken token)
    {
        var reason = NormalizeRequired(request.Reason, "分摊调整原因");
        var invoice = await db.FinanceInvoices.Include(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.Id == request.InvoiceId, token)
            ?? throw new KeyNotFoundException("发票记录不存在。");
        EnsureCanManage(actor, invoice.Scope, invoice.LegalEntityId, invoice.CounterLegalEntityId, invoice.ProjectId);
        EnsureCurrent(invoice.ConcurrencyStamp, request.ConcurrencyStamp, "发票记录");
        EnsureDirectRecordEditable(invoice.SourceType, "发票记录");
        EnsureActive(invoice.Status, "发票记录");
        var targets = await ValidateAllocationsAsync(
            actor,
            invoice.Scope,
            invoice.Direction,
            invoice.LegalEntityId,
            invoice.BusinessPartnerId,
            invoice.CounterLegalEntityId,
            invoice.Amount,
            invoice.ProjectId,
            invoice.ContractId,
            request.Allocations,
            token);
        var before = InvoiceSnapshot(invoice);
        db.FinanceInvoiceAllocations.RemoveRange(invoice.Allocations);
        invoice.Allocations.Clear();
        var newAllocations = request.Allocations.Select(allocation =>
        {
            var target = targets[allocation.SettlementId];
            return new FinanceInvoiceAllocation
            {
                Invoice = invoice,
                Settlement = target,
                ProjectId = target.ProjectId,
                ContractId = target.ContractId,
                ContractLineItemId = target.ContractLineItemId,
                BusinessPartnerId = target.BusinessPartnerId,
                CounterLegalEntityId = target.CounterLegalEntityId,
                Amount = allocation.Amount,
                AllocationOrder = allocation.AllocationOrder
            };
        }).ToList();
        db.FinanceInvoiceAllocations.AddRange(newAllocations);
        invoice.ConcurrencyStamp = Guid.NewGuid();
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit(actor, "ReplaceAllocations", nameof(FinanceInvoice), invoice.Id, before, InvoiceSnapshot(invoice), invoice.ProjectId, reason);
        await db.SaveChangesAsync(token);
    }

    public async Task ReplaceCashAllocationsAsync(
        CentralLedgerActor actor,
        ReplaceCashAllocationsRequest request,
        CancellationToken token)
    {
        var reason = NormalizeRequired(request.Reason, "分摊调整原因");
        var cash = await db.FinanceCashEntries.Include(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.Id == request.CashEntryId, token)
            ?? throw new KeyNotFoundException("资金记录不存在。");
        EnsureCanManage(actor, cash.Scope, cash.LegalEntityId, cash.CounterLegalEntityId, cash.ProjectId);
        EnsureCurrent(cash.ConcurrencyStamp, request.ConcurrencyStamp, "资金记录");
        EnsureDirectRecordEditable(cash.SourceType, "资金记录");
        EnsureActive(cash.Status, "资金记录");
        var targets = await ValidateAllocationsAsync(
            actor,
            cash.Scope,
            cash.Direction,
            cash.LegalEntityId,
            cash.BusinessPartnerId,
            cash.CounterLegalEntityId,
            cash.Amount,
            cash.ProjectId,
            cash.ContractId,
            request.Allocations,
            token);
        var before = CashSnapshot(cash);
        db.FinanceCashAllocations.RemoveRange(cash.Allocations);
        cash.Allocations.Clear();
        var newAllocations = request.Allocations.Select(allocation =>
        {
            var target = targets[allocation.SettlementId];
            return new FinanceCashAllocation
            {
                CashEntry = cash,
                Settlement = target,
                ProjectId = target.ProjectId,
                ContractId = target.ContractId,
                ContractLineItemId = target.ContractLineItemId,
                BusinessPartnerId = target.BusinessPartnerId,
                CounterLegalEntityId = target.CounterLegalEntityId,
                Amount = allocation.Amount,
                AllocationOrder = allocation.AllocationOrder
            };
        }).ToList();
        db.FinanceCashAllocations.AddRange(newAllocations);
        cash.ConcurrencyStamp = Guid.NewGuid();
        cash.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit(actor, "ReplaceAllocations", nameof(FinanceCashEntry), cash.Id, before, CashSnapshot(cash), cash.ProjectId, reason);
        await db.SaveChangesAsync(token);
    }

    public async Task DeleteAsync(
        CentralLedgerActor actor,
        DeleteFinanceRecordRequest request,
        CancellationToken token)
    {
        var reason = NormalizeRequired(request.Reason, "删除原因");
        var entryPoint = NormalizeRequired(request.EntryPoint, "删除入口");
        switch (request.RecordType)
        {
            case FinanceRecordType.Settlement:
                await DeleteSettlementAsync(actor, request, reason, entryPoint, token);
                break;
            case FinanceRecordType.Deduction:
                await DeleteDeductionAsync(actor, request, reason, entryPoint, token);
                break;
            case FinanceRecordType.Invoice:
                await DeleteInvoiceAsync(actor, request, reason, entryPoint, token);
                break;
            case FinanceRecordType.Cash:
                await DeleteCashAsync(actor, request, reason, entryPoint, token);
                break;
            case FinanceRecordType.Adjustment:
                await DeleteAdjustmentAsync(actor, request, reason, entryPoint, token);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.RecordType, "不支持的财务记录类型。");
        }
    }

    private async Task DeleteSettlementAsync(
        CentralLedgerActor actor,
        DeleteFinanceRecordRequest request,
        string reason,
        string entryPoint,
        CancellationToken token)
    {
        var settlement = await db.FinanceSettlements
            .Include(item => item.Adjustments)
            .Include(item => item.Deductions)
            .Include(item => item.InvoiceAllocations)
            .Include(item => item.CashAllocations)
            .SingleOrDefaultAsync(item => item.Id == request.RecordId, token)
            ?? throw new KeyNotFoundException("中央账本结算记录不存在。");
        EnsureCanManage(actor, settlement.Scope, settlement.LegalEntityId, settlement.CounterLegalEntityId, settlement.ProjectId);
        EnsureCurrent(settlement.ConcurrencyStamp, request.ConcurrencyStamp, "结算记录");
        EnsureDirectRecordEditable(settlement.SourceType, "结算记录");
        EnsureActive(settlement.Status, "结算记录");
        var beforeMetrics = await CalculateSettlementAsync(settlement.Id, null, null, token);
        var snapshot = new
        {
            Header = SettlementSnapshot(settlement),
            Adjustments = settlement.Adjustments.Select(AdjustmentSnapshot).ToArray(),
            Deductions = settlement.Deductions.Select(DeductionSnapshot).ToArray(),
            InvoiceAllocations = settlement.InvoiceAllocations.Select(AllocationSnapshot).ToArray(),
            CashAllocations = settlement.CashAllocations.Select(AllocationSnapshot).ToArray()
        };

        db.FinanceInvoiceAllocations.RemoveRange(settlement.InvoiceAllocations);
        db.FinanceCashAllocations.RemoveRange(settlement.CashAllocations);
        db.FinanceDeductions.RemoveRange(settlement.Deductions);
        db.FinanceSettlementAdjustments.RemoveRange(settlement.Adjustments);
        db.FinanceSettlements.Remove(settlement);
        AddDeletionLog(actor, request, reason, entryPoint, snapshot, beforeMetrics, CentralLedgerMetrics.Zero, settlement);
        AddAudit(actor, "Delete", nameof(FinanceSettlement), settlement.Id, snapshot, null, settlement.ProjectId, reason);
        await db.SaveChangesAsync(token);
    }

    private async Task DeleteDeductionAsync(
        CentralLedgerActor actor,
        DeleteFinanceRecordRequest request,
        string reason,
        string entryPoint,
        CancellationToken token)
    {
        var deduction = await db.FinanceDeductions.Include(item => item.Settlement)
            .SingleOrDefaultAsync(item => item.Id == request.RecordId, token)
            ?? throw new KeyNotFoundException("扣款记录不存在。");
        EnsureCanManage(actor, deduction.Settlement.Scope, deduction.Settlement.LegalEntityId, deduction.Settlement.CounterLegalEntityId, deduction.Settlement.ProjectId);
        EnsureCurrent(deduction.ConcurrencyStamp, request.ConcurrencyStamp, "扣款记录");
        EnsureDirectRecordEditable(deduction.SourceType, "扣款记录");
        EnsureActive(deduction.Status, "扣款记录");
        var beforeMetrics = await CalculateSettlementAsync(deduction.SettlementId, null, null, token);
        var afterMetrics = await CalculateSettlementAsync(deduction.SettlementId, FinanceRecordType.Deduction, deduction.Id, token);
        var snapshot = DeductionSnapshot(deduction);
        db.FinanceDeductions.Remove(deduction);
        deduction.Settlement.ConcurrencyStamp = Guid.NewGuid();
        deduction.Settlement.UpdatedAt = DateTimeOffset.UtcNow;
        AddDeletionLog(actor, request, reason, entryPoint, snapshot, beforeMetrics, afterMetrics, deduction.Settlement);
        AddAudit(actor, "Delete", nameof(FinanceDeduction), deduction.Id, snapshot, null, deduction.Settlement.ProjectId, reason);
        await db.SaveChangesAsync(token);
    }

    private async Task DeleteInvoiceAsync(
        CentralLedgerActor actor,
        DeleteFinanceRecordRequest request,
        string reason,
        string entryPoint,
        CancellationToken token)
    {
        var invoice = await db.FinanceInvoices.Include(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.Id == request.RecordId, token)
            ?? throw new KeyNotFoundException("发票记录不存在。");
        EnsureCanManage(actor, invoice.Scope, invoice.LegalEntityId, invoice.CounterLegalEntityId, invoice.ProjectId);
        EnsureCurrent(invoice.ConcurrencyStamp, request.ConcurrencyStamp, "发票记录");
        EnsureDirectRecordEditable(invoice.SourceType, "发票记录");
        EnsureActive(invoice.Status, "发票记录");
        var beforeMetrics = await SumSettlementMetricsAsync(invoice.Allocations.Select(item => item.SettlementId), null, null, token);
        var afterMetrics = await SumSettlementMetricsAsync(invoice.Allocations.Select(item => item.SettlementId), FinanceRecordType.Invoice, invoice.Id, token);
        var snapshot = InvoiceSnapshot(invoice);
        db.FinanceInvoices.Remove(invoice);
        AddDeletionLog(actor, request, reason, entryPoint, snapshot, beforeMetrics, afterMetrics, invoice);
        AddAudit(actor, "Delete", nameof(FinanceInvoice), invoice.Id, snapshot, null, invoice.ProjectId, reason);
        await db.SaveChangesAsync(token);
    }

    private async Task DeleteCashAsync(
        CentralLedgerActor actor,
        DeleteFinanceRecordRequest request,
        string reason,
        string entryPoint,
        CancellationToken token)
    {
        var cash = await db.FinanceCashEntries.Include(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.Id == request.RecordId, token)
            ?? throw new KeyNotFoundException("资金记录不存在。");
        EnsureCanManage(actor, cash.Scope, cash.LegalEntityId, cash.CounterLegalEntityId, cash.ProjectId);
        EnsureCurrent(cash.ConcurrencyStamp, request.ConcurrencyStamp, "资金记录");
        EnsureDirectRecordEditable(cash.SourceType, "资金记录");
        EnsureActive(cash.Status, "资金记录");
        var beforeMetrics = await SumSettlementMetricsAsync(cash.Allocations.Select(item => item.SettlementId), null, null, token);
        var afterMetrics = await SumSettlementMetricsAsync(cash.Allocations.Select(item => item.SettlementId), FinanceRecordType.Cash, cash.Id, token);
        var snapshot = CashSnapshot(cash);
        await RemoveAccountTransactionsAsync(cash.Id, token);
        db.FinanceCashEntries.Remove(cash);
        AddDeletionLog(actor, request, reason, entryPoint, snapshot, beforeMetrics, afterMetrics, cash);
        AddAudit(actor, "Delete", nameof(FinanceCashEntry), cash.Id, snapshot, null, cash.ProjectId, reason);
        await db.SaveChangesAsync(token);
    }

    private async Task DeleteAdjustmentAsync(
        CentralLedgerActor actor,
        DeleteFinanceRecordRequest request,
        string reason,
        string entryPoint,
        CancellationToken token)
    {
        var adjustment = await db.FinanceSettlementAdjustments.Include(item => item.Settlement)
            .SingleOrDefaultAsync(item => item.Id == request.RecordId, token)
            ?? throw new KeyNotFoundException("结算调整记录不存在。");
        EnsureCanManage(actor, adjustment.Settlement.Scope, adjustment.Settlement.LegalEntityId, adjustment.Settlement.CounterLegalEntityId, adjustment.Settlement.ProjectId);
        EnsureCurrent(adjustment.ConcurrencyStamp, request.ConcurrencyStamp, "结算调整记录");
        EnsureDirectRecordEditable(adjustment.SourceType, "结算调整记录");
        EnsureActive(adjustment.Status, "结算调整记录");
        var beforeMetrics = await CalculateSettlementAsync(adjustment.SettlementId, null, null, token);
        var afterMetrics = await CalculateSettlementAsync(adjustment.SettlementId, FinanceRecordType.Adjustment, adjustment.Id, token);
        var snapshot = AdjustmentSnapshot(adjustment);
        db.FinanceSettlementAdjustments.Remove(adjustment);
        adjustment.Settlement.ConcurrencyStamp = Guid.NewGuid();
        adjustment.Settlement.UpdatedAt = DateTimeOffset.UtcNow;
        AddDeletionLog(actor, request, reason, entryPoint, snapshot, beforeMetrics, afterMetrics, adjustment.Settlement);
        AddAudit(actor, "Delete", nameof(FinanceSettlementAdjustment), adjustment.Id, snapshot, null, adjustment.Settlement.ProjectId, reason);
        await db.SaveChangesAsync(token);
    }

    private async Task<Dictionary<Guid, FinanceSettlement>> ValidateAllocationsAsync(
        CentralLedgerActor? actor,
        LedgerScope scope,
        LedgerDirection direction,
        Guid legalEntityId,
        Guid? businessPartnerId,
        Guid? counterLegalEntityId,
        decimal headerAmount,
        Guid? projectId,
        Guid? contractId,
        IReadOnlyList<FinanceAllocationRequest> allocations,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        if (allocations.Sum(item => item.Amount) > headerAmount)
        {
            throw new ArgumentException("分摊金额合计不能超过单据有效金额。", nameof(allocations));
        }

        if (allocations.Select(item => item.SettlementId).Distinct().Count() != allocations.Count)
        {
            throw new ArgumentException("分摊目标不能重复。", nameof(allocations));
        }

        if (allocations.Select(item => item.AllocationOrder).Distinct().Count() != allocations.Count)
        {
            throw new ArgumentException("分摊顺序不能重复。", nameof(allocations));
        }

        foreach (var allocation in allocations)
        {
            EnsurePositive(allocation.Amount, nameof(allocation.Amount));
        }

        var settlementIds = allocations.Select(item => item.SettlementId).Distinct().ToArray();
        var targets = db.FinanceSettlements.Local
            .Where(item => settlementIds.Contains(item.Id))
            .ToDictionary(item => item.Id);
        var missingSettlementIds = settlementIds.Where(item => !targets.ContainsKey(item)).ToArray();
        if (missingSettlementIds.Length > 0)
        {
            var storedTargets = await db.FinanceSettlements
                .Where(item => missingSettlementIds.Contains(item.Id))
                .ToListAsync(token);
            foreach (var target in storedTargets)
            {
                targets[target.Id] = target;
            }
        }
        if (targets.Count != settlementIds.Length)
        {
            throw new InvalidOperationException("部分分摊目标结算记录不存在。");
        }

        foreach (var target in targets.Values)
        {
            if (actor is not null)
            {
                EnsureCanManage(actor, target.Scope, target.LegalEntityId, target.CounterLegalEntityId, target.ProjectId);
            }
            if (target.Status != LedgerRecordStatus.Active)
            {
                throw new InvalidOperationException("分摊目标结算记录已作废。" );
            }
            if (target.Scope != scope || target.Direction != direction || target.LegalEntityId != legalEntityId ||
                target.BusinessPartnerId != businessPartnerId || target.CounterLegalEntityId != counterLegalEntityId)
            {
                throw new InvalidOperationException("分摊不能跨越账本范围、方向、自有公司或往来单位。");
            }
            if (projectId.HasValue && target.ProjectId != projectId)
            {
                throw new InvalidOperationException("分摊目标不属于标题项目。");
            }
            if (contractId.HasValue && target.ContractId != contractId)
            {
                throw new InvalidOperationException("分摊目标不属于标题合同。");
            }
        }

        return targets;
    }

    private async Task ValidateContextAsync(
        CentralLedgerActor? actor,
        LedgerScope scope,
        LedgerDirection direction,
        Guid legalEntityId,
        Guid? businessPartnerId,
        Guid? counterLegalEntityId,
        Guid? projectId,
        Guid? contractId,
        Guid? contractLineItemId,
        CancellationToken token)
    {
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (actor is not null)
        {
            EnsureCanManage(actor, scope, legalEntityId, counterLegalEntityId, projectId);
        }

        if (!await db.LegalEntities.AnyAsync(item => item.Id == legalEntityId && item.IsActive, token))
        {
            throw new InvalidOperationException("自有公司不存在或已停用。");
        }

        if (scope == LedgerScope.External)
        {
            if (!businessPartnerId.HasValue || counterLegalEntityId.HasValue)
            {
                throw new ArgumentException("外部账本必须选择合作单位，且不能选择内部往来公司。");
            }
            if (!await db.BusinessPartners.AnyAsync(item => item.Id == businessPartnerId && item.IsActive, token))
            {
                throw new InvalidOperationException("合作单位不存在或已停用。");
            }
        }
        else
        {
            if (businessPartnerId.HasValue || !counterLegalEntityId.HasValue || counterLegalEntityId == legalEntityId)
            {
                throw new ArgumentException("内部账本必须选择另一家自有公司，且不能选择外部合作单位。");
            }
            if ((actor is not null && !actor.LegalEntityIds.Contains(counterLegalEntityId.Value)) ||
                !await db.LegalEntities.AnyAsync(item => item.Id == counterLegalEntityId && item.IsActive, token))
            {
                throw new UnauthorizedAccessException("无权管理内部往来公司的中央账本记录。");
            }
        }

        if (!projectId.HasValue)
        {
            if (contractId.HasValue || contractLineItemId.HasValue)
            {
                throw new ArgumentException("选择合同或合同清单前必须选择项目。");
            }
            return;
        }

        if (!await db.Projects.AnyAsync(item => item.Id == projectId && item.IsActive, token))
        {
            throw new InvalidOperationException("项目不存在或已停用。");
        }
        if (!await db.ProjectLegalEntities.AnyAsync(item => item.ProjectId == projectId && item.LegalEntityId == legalEntityId, token))
        {
            throw new InvalidOperationException("所选签约公司未关联当前项目。");
        }
        if (contractId.HasValue && !await db.Contracts.AnyAsync(item => item.Id == contractId && item.ProjectId == projectId && item.IsActive, token))
        {
            throw new InvalidOperationException("合同不属于所选项目或已停用。");
        }
        if (scope == LedgerScope.External && direction == LedgerDirection.Receivable && contractId.HasValue && businessPartnerId.HasValue &&
            await db.Contracts.AnyAsync(item => item.Id == contractId && item.BusinessPartnerId.HasValue && item.BusinessPartnerId != businessPartnerId, token))
        {
            throw new InvalidOperationException("所选合作单位与当前合同不一致。");
        }
        if (contractId.HasValue && await db.ContractLegalEntityAllocations.AnyAsync(item => item.ContractId == contractId && item.LegalEntityId == legalEntityId, token) == false &&
            await db.ContractLegalEntityAllocations.AnyAsync(item => item.ContractId == contractId, token))
        {
            throw new InvalidOperationException("所选签约公司未配置在当前合同的公司分摊中。");
        }
        if (contractLineItemId.HasValue && !await db.ContractLineItems.AnyAsync(
                item => item.Id == contractLineItemId && item.Contract.IsActive && item.Contract.ProjectId == projectId && (!contractId.HasValue || item.ContractId == contractId),
                token))
        {
            throw new InvalidOperationException("合同清单不属于所选项目或合同。");
        }
    }

    private async Task ValidateAccountsAsync(
        Guid legalEntityId,
        Guid? accountId,
        Guid? counterLegalEntityId,
        Guid? counterAccountId,
        CancellationToken token)
    {
        if (accountId.HasValue && !await db.FinancialAccounts.AnyAsync(
                item => item.Id == accountId && item.LegalEntityId == legalEntityId && item.IsActive,
                token))
        {
            throw new InvalidOperationException("资金账户不属于所选自有公司或已停用。");
        }
        if (counterAccountId.HasValue && (!counterLegalEntityId.HasValue || !await db.FinancialAccounts.AnyAsync(
                item => item.Id == counterAccountId && item.LegalEntityId == counterLegalEntityId && item.IsActive,
                token)))
        {
            throw new InvalidOperationException("对方资金账户不属于内部往来公司或已停用。");
        }
    }

    private async Task<CentralLedgerMetrics> SumSettlementMetricsAsync(
        IEnumerable<Guid> settlementIds,
        FinanceRecordType? excludedType,
        Guid? excludedId,
        CancellationToken token)
    {
        var total = CentralLedgerMetrics.Zero;
        foreach (var settlementId in settlementIds.Distinct())
        {
            total = CentralLedgerCalculator.Add(total, await CalculateSettlementAsync(settlementId, excludedType, excludedId, token));
        }
        return total;
    }

    private async Task<CentralLedgerMetrics> CalculateSettlementAsync(
        Guid settlementId,
        FinanceRecordType? excludedType,
        Guid? excludedId,
        CancellationToken token)
    {
        var settlement = await db.FinanceSettlements.AsNoTracking().SingleAsync(item => item.Id == settlementId, token);
        var adjustments = await db.FinanceSettlementAdjustments.AsNoTracking()
            .Where(item => item.SettlementId == settlementId && item.Status == LedgerRecordStatus.Active &&
                (excludedType != FinanceRecordType.Adjustment || item.Id != excludedId))
            .ToListAsync(token);
        var deductions = await db.FinanceDeductions.AsNoTracking()
            .Where(item => item.SettlementId == settlementId && item.Status == LedgerRecordStatus.Active &&
                (excludedType != FinanceRecordType.Deduction || item.Id != excludedId))
            .ToListAsync(token);
        var invoicedAmount = await db.FinanceInvoiceAllocations.AsNoTracking()
            .Where(item => item.SettlementId == settlementId && item.Invoice.Status == LedgerRecordStatus.Active &&
                (excludedType != FinanceRecordType.Invoice || item.InvoiceId != excludedId))
            .SumAsync(item => (decimal?)item.Amount, token) ?? 0m;
        var cashAmount = await db.FinanceCashAllocations.AsNoTracking()
            .Where(item => item.SettlementId == settlementId && item.CashEntry.Status == LedgerRecordStatus.Active &&
                (excludedType != FinanceRecordType.Cash || item.CashEntryId != excludedId))
            .SumAsync(item => (decimal?)(item.CashEntry.IsReversal ? -item.Amount : item.Amount), token) ?? 0m;

        return CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(
            settlement.OriginalAmount + adjustments.Sum(item => item.AmountDelta),
            deductions.Sum(item => item.Amount),
            deductions.Where(item => item.ReduceInvoiceAmount).Sum(item => item.Amount),
            settlement.OriginalInvoiceAmount + adjustments.Sum(item => item.InvoiceAmountDelta),
            invoicedAmount,
            cashAmount));
    }

    private async Task SyncAccountTransactionsAsync(FinanceCashEntry cash, CancellationToken token)
    {
        var existing = await db.AccountTransactions
            .Where(item => item.SourceId == cash.Id && CentralCashTransactionSources.Contains(item.SourceType))
            .ToListAsync(token);
        var expected = BuildAccountTransactionProjection(cash);
        var remaining = existing.ToList();

        foreach (var projection in expected)
        {
            var transaction = remaining.FirstOrDefault(item => item.SourceType == projection.SourceType);
            if (transaction is null)
            {
                transaction = new AccountTransaction
                {
                    SourceId = cash.Id,
                    SourceType = projection.SourceType
                };
                db.AccountTransactions.Add(transaction);
            }
            else
            {
                remaining.Remove(transaction);
            }

            transaction.AccountId = projection.AccountId;
            transaction.Direction = projection.Direction;
            transaction.TransactionDate = cash.BusinessDate;
            transaction.Amount = cash.Amount;
            transaction.Description = cash.Notes;
        }

        if (remaining.Count > 0)
        {
            db.AccountTransactions.RemoveRange(remaining);
        }
    }

    private async Task RemoveAccountTransactionsAsync(Guid cashEntryId, CancellationToken token)
    {
        var transactions = await db.AccountTransactions
            .Where(item => item.SourceId == cashEntryId && CentralCashTransactionSources.Contains(item.SourceType))
            .ToListAsync(token);
        if (transactions.Count > 0)
        {
            db.AccountTransactions.RemoveRange(transactions);
        }
    }

    private static List<AccountTransactionProjection> BuildAccountTransactionProjection(FinanceCashEntry cash)
    {
        if (cash.CashType == LedgerCashType.InternalTransfer)
        {
            var accountDirection = cash.Direction == LedgerDirection.Payable
                ? (AccountTransactionDirection.Outflow, AccountTransactionSourceType.TransferOut)
                : (AccountTransactionDirection.Inflow, AccountTransactionSourceType.TransferIn);
            var counterDirection = cash.Direction == LedgerDirection.Payable
                ? (AccountTransactionDirection.Inflow, AccountTransactionSourceType.TransferIn)
                : (AccountTransactionDirection.Outflow, AccountTransactionSourceType.TransferOut);
            var projections = new List<AccountTransactionProjection>();
            if (cash.AccountId.HasValue)
            {
                projections.Add(new AccountTransactionProjection(cash.AccountId.Value, accountDirection.Item1, accountDirection.Item2));
            }
            if (cash.CounterAccountId.HasValue)
            {
                projections.Add(new AccountTransactionProjection(cash.CounterAccountId.Value, counterDirection.Item1, counterDirection.Item2));
            }
            return projections;
        }

        if (!cash.AccountId.HasValue) return [];
        var isCollection = cash.CashType == LedgerCashType.Collection;
        var direction = isCollection
            ? (cash.IsReversal ? AccountTransactionDirection.Outflow : AccountTransactionDirection.Inflow)
            : (cash.IsReversal ? AccountTransactionDirection.Inflow : AccountTransactionDirection.Outflow);
        var sourceType = isCollection
            ? (cash.IsReversal ? AccountTransactionSourceType.Refund : AccountTransactionSourceType.Collection)
            : (cash.IsReversal ? AccountTransactionSourceType.PaymentReversal : AccountTransactionSourceType.Payment);
        return [new AccountTransactionProjection(cash.AccountId.Value, direction, sourceType)];
    }

    private sealed record AccountTransactionProjection(
        Guid AccountId,
        AccountTransactionDirection Direction,
        AccountTransactionSourceType SourceType);

    private async Task EnsureInvoiceNumberAvailableAsync(
        Guid legalEntityId,
        LedgerDirection direction,
        string invoiceNumber,
        Guid? excludedInvoiceId,
        CancellationToken token)
    {
        var trackedDuplicate = db.FinanceInvoices.Local.Any(item =>
            db.Entry(item).State != EntityState.Deleted &&
            item.LegalEntityId == legalEntityId &&
            item.Direction == direction &&
            item.InvoiceNumber == invoiceNumber &&
            (!excludedInvoiceId.HasValue || item.Id != excludedInvoiceId.Value));
        if (trackedDuplicate)
        {
            throw new InvalidOperationException($"发票号码已存在：{invoiceNumber}");
        }

        var query = db.FinanceInvoices.Where(item =>
            item.LegalEntityId == legalEntityId &&
            item.Direction == direction &&
            item.InvoiceNumber == invoiceNumber);
        if (excludedInvoiceId.HasValue)
        {
            query = query.Where(item => item.Id != excludedInvoiceId.Value);
        }

        if (await query.AnyAsync(token))
        {
            throw new InvalidOperationException($"发票号码已存在：{invoiceNumber}");
        }
    }

    private static void EnsureCashType(LedgerScope scope, LedgerDirection direction, LedgerCashType cashType)
    {
        if (scope == LedgerScope.Internal)
        {
            if (cashType != LedgerCashType.InternalTransfer)
            {
                throw new ArgumentException("内部账本资金必须使用 InternalTransfer。", nameof(cashType));
            }

            return;
        }

        var expected = direction == LedgerDirection.Receivable
            ? LedgerCashType.Collection
            : LedgerCashType.Payment;
        if (cashType != expected)
        {
            throw new ArgumentException("外部账本收款必须使用 Collection，付款必须使用 Payment。", nameof(cashType));
        }
    }

    private void AddDeletionLog(
        CentralLedgerActor actor,
        DeleteFinanceRecordRequest request,
        string reason,
        string entryPoint,
        object snapshot,
        CentralLedgerMetrics beforeMetrics,
        CentralLedgerMetrics afterMetrics,
        object source)
    {
        var related = source switch
        {
            FinanceSettlement item => (item.LegalEntityId, item.BusinessPartnerId, item.CounterLegalEntityId, item.ProjectId, item.ContractId),
            FinanceDeduction item => (item.Settlement.LegalEntityId, item.Settlement.BusinessPartnerId, item.Settlement.CounterLegalEntityId, item.Settlement.ProjectId, item.Settlement.ContractId),
            FinanceSettlementAdjustment item => (item.Settlement.LegalEntityId, item.Settlement.BusinessPartnerId, item.Settlement.CounterLegalEntityId, item.Settlement.ProjectId, item.Settlement.ContractId),
            FinanceInvoice item => (item.LegalEntityId, item.BusinessPartnerId, item.CounterLegalEntityId, item.ProjectId, item.ContractId),
            FinanceCashEntry item => (item.LegalEntityId, item.BusinessPartnerId, item.CounterLegalEntityId, item.ProjectId, item.ContractId),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        db.FinanceDeletionLogs.Add(new FinanceDeletionLog
        {
            RecordType = request.RecordType,
            RecordId = request.RecordId,
            DeletedByUserId = actor.UserId,
            DeletedByUserName = actor.UserName,
            EntryPoint = entryPoint,
            Reason = reason,
            SnapshotJson = Serialize(snapshot),
            BeforeMetricsJson = Serialize(beforeMetrics),
            AfterMetricsJson = Serialize(afterMetrics),
            LegalEntityId = related.Item1,
            BusinessPartnerId = related.Item2,
            CounterLegalEntityId = related.Item3,
            ProjectId = related.Item4,
            ContractId = related.Item5
        });
    }

    private void AddAudit(
        CentralLedgerActor actor,
        string action,
        string entityType,
        Guid entityId,
        object? before,
        object? after,
        Guid? projectId,
        string? reason = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actor.UserId,
            UserName = actor.UserName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            RelatedProjectId = projectId?.ToString(),
            Reason = reason,
            BeforeJson = before is null ? null : Serialize(before),
            AfterJson = after is null ? null : Serialize(after)
        });
    }

    private static object SettlementSnapshot(FinanceSettlement item) => new
    {
        item.Id,
        item.Scope,
        item.Direction,
        item.SettlementState,
        item.SourceType,
        item.SourceId,
        item.LegalEntityId,
        item.BusinessPartnerId,
        item.CounterLegalEntityId,
        item.ProjectId,
        item.ContractId,
        item.ContractLineItemId,
        item.BusinessDate,
        item.DueDate,
        item.SettlementDate,
        item.OriginalAmount,
        item.OriginalInvoiceAmount,
        item.Status,
        item.Notes,
        item.ConcurrencyStamp
    };

    private static object AdjustmentSnapshot(FinanceSettlementAdjustment item) => new
    {
        item.Id,
        item.SettlementId,
        item.AdjustmentType,
        item.AmountDelta,
        item.InvoiceAmountDelta,
        item.BusinessDate,
        item.Reason,
        item.Status,
        item.ConcurrencyStamp
    };

    private static object DeductionSnapshot(FinanceDeduction item) => new
    {
        item.Id,
        item.SettlementId,
        item.BusinessDate,
        item.Amount,
        item.ReduceInvoiceAmount,
        item.Reason,
        item.Status,
        item.ConcurrencyStamp
    };

    private static object InvoiceSnapshot(FinanceInvoice item) => new
    {
        item.Id,
        item.Scope,
        item.Direction,
        item.LegalEntityId,
        item.BusinessPartnerId,
        item.CounterLegalEntityId,
        item.ProjectId,
        item.ContractId,
        item.InvoiceNumber,
        item.InvoiceDate,
        item.Amount,
        item.Status,
        item.ConcurrencyStamp,
        Allocations = item.Allocations.Select(AllocationSnapshot).ToArray()
    };

    private static object CashSnapshot(FinanceCashEntry item) => new
    {
        item.Id,
        item.Scope,
        item.Direction,
        item.CashType,
        item.LegalEntityId,
        item.BusinessPartnerId,
        item.CounterLegalEntityId,
        item.ProjectId,
        item.ContractId,
        item.AccountId,
        item.CounterAccountId,
        item.BusinessDate,
        item.Amount,
        item.Status,
        item.ConcurrencyStamp,
        Allocations = item.Allocations.Select(AllocationSnapshot).ToArray()
    };

    private static object AllocationSnapshot(FinanceInvoiceAllocation item) => new
    {
        item.Id,
        item.InvoiceId,
        item.SettlementId,
        item.ProjectId,
        item.ContractId,
        item.ContractLineItemId,
        item.Amount,
        item.AllocationOrder,
        item.ConcurrencyStamp
    };

    private static object AllocationSnapshot(FinanceCashAllocation item) => new
    {
        item.Id,
        item.CashEntryId,
        item.SettlementId,
        item.ProjectId,
        item.ContractId,
        item.ContractLineItemId,
        item.Amount,
        item.AllocationOrder,
        item.ConcurrencyStamp
    };

    private static string Serialize(object value) => JsonSerializer.Serialize(value);

    private static void EnsureCanManage(
        CentralLedgerActor actor,
        LedgerScope scope,
        Guid legalEntityId,
        Guid? counterLegalEntityId,
        Guid? projectId)
    {
        var canManage = scope == LedgerScope.External ? actor.CanManageExternal : actor.CanManageInternal;
        if (!canManage || !actor.LegalEntityIds.Contains(legalEntityId) ||
            (counterLegalEntityId.HasValue && !actor.LegalEntityIds.Contains(counterLegalEntityId.Value)) ||
            (projectId.HasValue && !actor.ProjectIds.Contains(projectId.Value)))
        {
            throw new UnauthorizedAccessException("无权管理所选中央账本范围。");
        }
    }

    private static void EnsureDirectRecordEditable(LedgerSourceType sourceType, string label)
    {
        if (sourceType != LedgerSourceType.CentralLedger)
            throw new InvalidOperationException($"{label}由来源模块生成，只能返回来源模块修改。");
    }

    private static void EnsureSourceDescriptor(LedgerSourceType sourceType, Guid? sourceId)
    {
        if (!Enum.IsDefined(sourceType)) throw new ArgumentOutOfRangeException(nameof(sourceType));
        if (sourceType == LedgerSourceType.LegacyMigration)
        {
            throw new InvalidOperationException("历史迁移记录不能通过中央账本录入接口重复生成。");
        }
        if (sourceType is LedgerSourceType.CentralLedger && sourceId.HasValue)
        {
            throw new ArgumentException("中央账本直接录入不能携带来源记录编号。", nameof(sourceId));
        }
        if ((sourceType is LedgerSourceType.ProjectQuantity or LedgerSourceType.ProjectCollection) && !sourceId.HasValue)
        {
            throw new ArgumentException("来源模块记录必须携带来源编号。", nameof(sourceId));
        }
    }

    private static void EnsureActive(LedgerRecordStatus status, string label)
    {
        if (status != LedgerRecordStatus.Active)
        {
            throw new InvalidOperationException($"{label}已作废，不能继续执行该操作。");
        }
    }

    private static void EnsureInvoiceAmountNotExceedAmount(decimal amount, decimal invoiceAmount)
    {
        if (invoiceAmount > amount)
        {
            throw new ArgumentException("应开票金额不能超过结算金额。", nameof(invoiceAmount));
        }
    }

    private static void ValidateInvoiceAmounts(
        decimal grossAmount,
        decimal? netAmount,
        decimal? taxAmount,
        decimal? taxRate)
    {
        if (!netAmount.HasValue && !taxAmount.HasValue && !taxRate.HasValue) return;
        if (!netAmount.HasValue || !taxAmount.HasValue || !taxRate.HasValue)
        {
            throw new ArgumentException("填写发票税务信息时必须同时填写不含税金额、税额和税率。");
        }
        InvoiceAmountValidator.Validate(netAmount.Value, taxAmount.Value, grossAmount, taxRate.Value);
    }

    private async Task ValidateProjectTaxConfigurationAsync(
        Guid? projectId,
        Guid? configurationId,
        decimal? requestedTaxRate,
        CancellationToken token)
    {
        if (!configurationId.HasValue) return;
        if (!projectId.HasValue)
        {
            throw new InvalidOperationException("税务配置必须属于指定项目。");
        }
        var configuration = await db.ProjectTaxConfigurations.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == configurationId && item.ProjectId == projectId && item.IsActive, token);
        if (configuration is null)
        {
            throw new InvalidOperationException("税务配置不存在、已停用或不属于当前项目。");
        }
        if (requestedTaxRate.HasValue && requestedTaxRate.Value != configuration.TaxRate)
        {
            throw new InvalidOperationException("发票税率与项目税务配置不一致。");
        }
    }

    private static void EnsureInternalAccounts(LedgerScope scope, Guid? accountId, Guid? counterAccountId)
    {
        if (scope == LedgerScope.Internal && (!accountId.HasValue || !counterAccountId.HasValue))
        {
            throw new ArgumentException("内部转账必须同时选择转出和转入账户。");
        }
    }

    private static void EnsureNoAllocations(int allocationCount, string message)
    {
        if (allocationCount > 0) throw new InvalidOperationException(message);
    }

    private static void EnsureCurrent(Guid actual, Guid expected, string label)
    {
        if (actual != expected)
        {
            throw new DbUpdateConcurrencyException($"{label}已被其他用户修改，请刷新后重试。");
        }
    }

    private static void EnsurePositive(decimal amount, string parameterName)
    {
        if (amount <= 0m) throw new ArgumentOutOfRangeException(parameterName, "金额必须大于零。");
    }

    private static void EnsureNonNegative(decimal amount, string parameterName)
    {
        if (amount < 0m) throw new ArgumentOutOfRangeException(parameterName, "金额不能为负数。");
    }

    private static string NormalizeRequired(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{label}不能为空。", nameof(value));
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
