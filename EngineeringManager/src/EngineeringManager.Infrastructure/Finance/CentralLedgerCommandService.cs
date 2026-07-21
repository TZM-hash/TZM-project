using System.Text.Json;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Finance;

public sealed class CentralLedgerCommandService : ICentralLedgerCommandService
{
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

    public async Task<Guid> CreateSettlementAsync(
        CentralLedgerActor actor,
        CreateSettlementRequest request,
        CancellationToken token)
    {
        EnsureNonNegative(request.OriginalAmount, nameof(request.OriginalAmount));
        EnsureNonNegative(request.OriginalInvoiceAmount, nameof(request.OriginalInvoiceAmount));
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
            SettlementDate = request.BusinessDate,
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
        await ValidateContextAsync(
            actor,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            null,
            null,
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
        AddAudit(actor, "Create", nameof(FinanceInvoice), invoice.Id, null, InvoiceSnapshot(invoice), null);
        await db.SaveChangesAsync(token);
        return invoice.Id;
    }

    public async Task<Guid> CreateCashAsync(
        CentralLedgerActor actor,
        CreateFinanceCashRequest request,
        CancellationToken token)
    {
        EnsurePositive(request.Amount, nameof(request.Amount));
        await ValidateContextAsync(
            actor,
            request.Scope,
            request.Direction,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.CounterLegalEntityId,
            null,
            null,
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
        if (cash.AccountId.HasValue && cash.CashType != LedgerCashType.InternalTransfer)
        {
            db.AccountTransactions.Add(new AccountTransaction
            {
                AccountId = cash.AccountId.Value,
                Direction = cash.CashType == LedgerCashType.Collection
                    ? AccountTransactionDirection.Inflow
                    : AccountTransactionDirection.Outflow,
                SourceType = cash.CashType == LedgerCashType.Collection
                    ? AccountTransactionSourceType.Collection
                    : AccountTransactionSourceType.Payment,
                SourceId = cash.Id,
                TransactionDate = cash.BusinessDate,
                Amount = cash.Amount,
                Description = cash.Notes
            });
        }
        AddAudit(actor, "Create", nameof(FinanceCashEntry), cash.Id, null, CashSnapshot(cash), null);
        await db.SaveChangesAsync(token);
        return cash.Id;
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
        EnsureCanManage(actor, invoice.Scope, invoice.LegalEntityId, invoice.CounterLegalEntityId, null);
        EnsureCurrent(invoice.ConcurrencyStamp, request.ConcurrencyStamp, "发票记录");
        var targets = await ValidateAllocationsAsync(
            actor,
            invoice.Scope,
            invoice.Direction,
            invoice.LegalEntityId,
            invoice.BusinessPartnerId,
            invoice.CounterLegalEntityId,
            invoice.Amount,
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
        AddAudit(actor, "ReplaceAllocations", nameof(FinanceInvoice), invoice.Id, before, InvoiceSnapshot(invoice), null, reason);
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
        EnsureCanManage(actor, cash.Scope, cash.LegalEntityId, cash.CounterLegalEntityId, null);
        EnsureCurrent(cash.ConcurrencyStamp, request.ConcurrencyStamp, "资金记录");
        var targets = await ValidateAllocationsAsync(
            actor,
            cash.Scope,
            cash.Direction,
            cash.LegalEntityId,
            cash.BusinessPartnerId,
            cash.CounterLegalEntityId,
            cash.Amount,
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
        AddAudit(actor, "ReplaceAllocations", nameof(FinanceCashEntry), cash.Id, before, CashSnapshot(cash), null, reason);
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
        EnsureCanManage(actor, invoice.Scope, invoice.LegalEntityId, invoice.CounterLegalEntityId, null);
        EnsureCurrent(invoice.ConcurrencyStamp, request.ConcurrencyStamp, "发票记录");
        var beforeMetrics = await SumSettlementMetricsAsync(invoice.Allocations.Select(item => item.SettlementId), null, null, token);
        var afterMetrics = await SumSettlementMetricsAsync(invoice.Allocations.Select(item => item.SettlementId), FinanceRecordType.Invoice, invoice.Id, token);
        var snapshot = InvoiceSnapshot(invoice);
        db.FinanceInvoices.Remove(invoice);
        AddDeletionLog(actor, request, reason, entryPoint, snapshot, beforeMetrics, afterMetrics, invoice);
        AddAudit(actor, "Delete", nameof(FinanceInvoice), invoice.Id, snapshot, null, null, reason);
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
        EnsureCanManage(actor, cash.Scope, cash.LegalEntityId, cash.CounterLegalEntityId, null);
        EnsureCurrent(cash.ConcurrencyStamp, request.ConcurrencyStamp, "资金记录");
        var beforeMetrics = await SumSettlementMetricsAsync(cash.Allocations.Select(item => item.SettlementId), null, null, token);
        var afterMetrics = await SumSettlementMetricsAsync(cash.Allocations.Select(item => item.SettlementId), FinanceRecordType.Cash, cash.Id, token);
        var snapshot = CashSnapshot(cash);
        db.FinanceCashEntries.Remove(cash);
        AddDeletionLog(actor, request, reason, entryPoint, snapshot, beforeMetrics, afterMetrics, cash);
        AddAudit(actor, "Delete", nameof(FinanceCashEntry), cash.Id, snapshot, null, null, reason);
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
        CentralLedgerActor actor,
        LedgerScope scope,
        LedgerDirection direction,
        Guid legalEntityId,
        Guid? businessPartnerId,
        Guid? counterLegalEntityId,
        decimal headerAmount,
        IReadOnlyList<FinanceAllocationRequest> allocations,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        if (allocations.Sum(item => item.Amount) > headerAmount)
        {
            throw new ArgumentException("分摊金额合计不能超过单据有效金额。", nameof(allocations));
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
        var targets = await db.FinanceSettlements
            .Where(item => settlementIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, token);
        if (targets.Count != settlementIds.Length)
        {
            throw new InvalidOperationException("部分分摊目标结算记录不存在。");
        }

        foreach (var target in targets.Values)
        {
            EnsureCanManage(actor, target.Scope, target.LegalEntityId, target.CounterLegalEntityId, target.ProjectId);
            if (target.Scope != scope || target.Direction != direction || target.LegalEntityId != legalEntityId ||
                target.BusinessPartnerId != businessPartnerId || target.CounterLegalEntityId != counterLegalEntityId)
            {
                throw new InvalidOperationException("分摊不能跨越账本范围、方向、自有公司或往来单位。");
            }
        }

        return targets;
    }

    private async Task ValidateContextAsync(
        CentralLedgerActor actor,
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
        EnsureCanManage(actor, scope, legalEntityId, counterLegalEntityId, projectId);

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
            if (!actor.LegalEntityIds.Contains(counterLegalEntityId.Value) ||
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
        if (contractId.HasValue && !await db.Contracts.AnyAsync(item => item.Id == contractId && item.ProjectId == projectId, token))
        {
            throw new InvalidOperationException("合同不属于所选项目。");
        }
        if (contractLineItemId.HasValue && !await db.ContractLineItems.AnyAsync(
                item => item.Id == contractLineItemId && item.Contract.ProjectId == projectId && (!contractId.HasValue || item.ContractId == contractId),
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
            FinanceInvoice item => (item.LegalEntityId, item.BusinessPartnerId, item.CounterLegalEntityId, (Guid?)null, (Guid?)null),
            FinanceCashEntry item => (item.LegalEntityId, item.BusinessPartnerId, item.CounterLegalEntityId, (Guid?)null, (Guid?)null),
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
