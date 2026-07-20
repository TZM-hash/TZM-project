using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Finance;

public sealed class FinancePostingService(ApplicationDbContext db) : IFinancePostingService
{
    internal async Task SynchronizeProjectQuantityReceivablesAsync(
        string actorUserId,
        string? actorUserName,
        Guid projectId,
        CancellationToken token)
    {
        if (!await db.Projects.AnyAsync(item => item.Id == projectId, token)) throw new KeyNotFoundException("项目不存在。");
        var existingSourceIds = await db.FinanceSettlements.AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.SourceType == LedgerSourceType.ProjectQuantity)
            .Select(item => item.SourceId)
            .ToListAsync(token);
        var legalEntityIds = await db.ProjectLegalEntities.AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .Select(item => item.LegalEntityId)
            .Concat(db.ContractLegalEntityAllocations.AsNoTracking()
                .Where(item => item.Contract.ProjectId == projectId)
                .Select(item => item.LegalEntityId))
            .Distinct()
            .ToListAsync(token);
        if (legalEntityIds.Count == 0)
        {
            if (existingSourceIds.Count > 0) throw new InvalidOperationException("已有工程量财务记录的项目不能在变更阶段时清空签约公司。");
            return;
        }
        var lineItemIds = await db.ContractLineItems.AsNoTracking()
            .Where(item => item.Contract.ProjectId == projectId && item.Contract.BusinessPartnerId.HasValue)
            .Select(item => item.Id)
            .ToListAsync(token);
        if (existingSourceIds.Any(sourceId => !sourceId.HasValue || !lineItemIds.Contains(sourceId.Value)))
            throw new InvalidOperationException("已有工程量财务记录缺少有效的合同客户，请先恢复合同维度或人工核对中央账本。");
        var actor = new CentralLedgerActor(
            actorUserId,
            actorUserName,
            legalEntityIds.ToHashSet(),
            new HashSet<Guid> { projectId },
            true,
            false,
            false,
            false);
        foreach (var lineItemId in lineItemIds)
            await UpsertProjectQuantityReceivableAsync(actor, lineItemId, token);
    }

    public async Task<Guid> UpsertProjectQuantityReceivableAsync(
        CentralLedgerActor actor,
        Guid lineItemId,
        CancellationToken token)
    {
        var lineItem = await db.ContractLineItems
            .Include(item => item.Contract).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.Contract).ThenInclude(item => item.LegalEntityAllocations)
            .Include(item => item.Contract).ThenInclude(item => item.Project).ThenInclude(item => item.LegalEntities)
            .SingleOrDefaultAsync(item => item.Id == lineItemId, token)
            ?? throw new KeyNotFoundException("工程量明细不存在。");
        var contract = lineItem.Contract;
        var project = contract.Project;
        var legalEntityId = contract.LegalEntityAllocations
            .OrderByDescending(item => item.Amount ?? 0m)
            .ThenByDescending(item => item.Percentage ?? 0m)
            .Select(item => (Guid?)item.LegalEntityId)
            .FirstOrDefault()
            ?? project.LegalEntities.OrderByDescending(item => item.IsPrimary).Select(item => (Guid?)item.LegalEntityId).FirstOrDefault()
            ?? throw new InvalidOperationException("项目工程量自动入账前必须配置签约公司。");
        var businessPartnerId = contract.BusinessPartnerId
            ?? throw new InvalidOperationException("项目工程量自动入账前必须配置合同客户。");
        EnsureActor(actor, legalEntityId, project.Id);

        var isSettlement = project.Stage is EngineeringManager.Domain.Projects.ProjectStage.PartiallySettled or EngineeringManager.Domain.Projects.ProjectStage.SettledArchived;
        var state = isSettlement ? LedgerSettlementState.Final : LedgerSettlementState.Provisional;
        var amount = (lineItem.Quantity ?? 0m) * (lineItem.UnitPrice ?? 0m);
        var invoiceAmount = lineItem.RequiresInvoice ? amount : 0m;
        if (amount < 0m) throw new InvalidOperationException("工程量结算金额不能为负数。");

        var existing = await db.FinanceSettlements
            .Include(item => item.Adjustments)
            .SingleOrDefaultAsync(item => item.SourceType == LedgerSourceType.ProjectQuantity && item.SourceId == lineItem.Id, token);
        if (existing is null)
        {
            return await new CentralLedgerCommandService(db).CreateSettlementAsync(
                actor,
                new CreateSettlementRequest(
                    LedgerScope.External,
                    LedgerDirection.Receivable,
                    state,
                    LedgerSourceType.ProjectQuantity,
                    lineItem.Id,
                    legalEntityId,
                    businessPartnerId,
                    null,
                    project.Id,
                    contract.Id,
                    lineItem.Id,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    amount,
                    invoiceAmount,
                    lineItem.Notes),
                token);
        }

        if (existing.LegalEntityId != legalEntityId || existing.BusinessPartnerId != businessPartnerId || existing.ProjectId != project.Id)
        {
            throw new InvalidOperationException("工程量来源对应的中央账本维度已变化，需要先人工核对原记录。");
        }

        var currentAmount = existing.OriginalAmount + existing.Adjustments
            .Where(item => item.Status == LedgerRecordStatus.Active)
            .Sum(item => item.AmountDelta);
        var currentInvoiceAmount = existing.OriginalInvoiceAmount + existing.Adjustments
            .Where(item => item.Status == LedgerRecordStatus.Active)
            .Sum(item => item.InvoiceAmountDelta);
        if (state == LedgerSettlementState.Provisional)
        {
            existing.SettlementState = LedgerSettlementState.Provisional;
            existing.SettlementDate = null;
            existing.OriginalAmount = amount;
            existing.OriginalInvoiceAmount = invoiceAmount;
            existing.Notes = lineItem.Notes;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.ConcurrencyStamp = Guid.NewGuid();
            foreach (var adjustment in existing.Adjustments.Where(item => item.Status == LedgerRecordStatus.Active))
            {
                adjustment.Status = LedgerRecordStatus.Voided;
                adjustment.ConcurrencyStamp = Guid.NewGuid();
            }
            await db.SaveChangesAsync(token);
            return existing.Id;
        }

        if (existing.SettlementState == LedgerSettlementState.Provisional && state == LedgerSettlementState.Final)
        {
            await new CentralLedgerCommandService(db).FinalizeSettlementAsync(
                actor,
                new FinalizeSettlementRequest(
                    existing.Id,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    amount,
                    invoiceAmount,
                    "工程量确认形成最终结算差额",
                    existing.ConcurrencyStamp),
                token);
            return existing.Id;
        }

        if (currentAmount != amount || currentInvoiceAmount != invoiceAmount)
        {
            var adjustment = new FinanceSettlementAdjustment
            {
                Settlement = existing,
                AdjustmentType = LedgerAdjustmentType.Correction,
                AmountDelta = amount - currentAmount,
                InvoiceAmountDelta = invoiceAmount - currentInvoiceAmount,
                BusinessDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Reason = "工程量修改形成结算修正",
                ActorUserId = actor.UserId,
                ActorUserName = actor.UserName,
                SourceType = LedgerSourceType.ProjectQuantity,
                SourceId = lineItem.Id
            };
            db.FinanceSettlementAdjustments.Add(adjustment);
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.ConcurrencyStamp = Guid.NewGuid();
            await db.SaveChangesAsync(token);
        }
        return existing.Id;
    }

    public Task<Guid> CreateCrewPayableAsync(
        CentralLedgerActor actor,
        CreateCrewPayableRequest request,
        CancellationToken token)
    {
        return CreatePayableAsync(
            actor,
            request.CrewBusinessPartnerId,
            request.LegalEntityId,
            request.ProjectId,
            request.ContractId,
            request.BusinessDate,
            request.SettlementState,
            request.Amount,
            request.InvoiceAmount,
            request.Notes,
            LedgerSourceType.Crew,
            token);
    }

    public Task<Guid> CreatePartnerPayableAsync(
        CentralLedgerActor actor,
        CreatePartnerPayableRequest request,
        CancellationToken token)
    {
        return CreatePayableAsync(
            actor,
            request.BusinessPartnerId,
            request.LegalEntityId,
            request.ProjectId,
            request.ContractId,
            request.BusinessDate,
            request.SettlementState,
            request.Amount,
            request.InvoiceAmount,
            request.Notes,
            LedgerSourceType.Partner,
            token);
    }

    private Task<Guid> CreatePayableAsync(
        CentralLedgerActor actor,
        Guid businessPartnerId,
        Guid legalEntityId,
        Guid? projectId,
        Guid? contractId,
        DateOnly businessDate,
        LedgerSettlementState settlementState,
        decimal amount,
        decimal invoiceAmount,
        string? notes,
        LedgerSourceType sourceType,
        CancellationToken token)
    {
        return new CentralLedgerCommandService(db).CreateSettlementAsync(
            actor,
            new CreateSettlementRequest(
                LedgerScope.External,
                LedgerDirection.Payable,
                settlementState,
                sourceType,
                null,
                legalEntityId,
                businessPartnerId,
                null,
                projectId,
                contractId,
                null,
                businessDate,
                amount,
                invoiceAmount,
                notes),
            token);
    }

    private static void EnsureActor(CentralLedgerActor actor, Guid legalEntityId, Guid projectId)
    {
        if (!actor.CanManageExternal || !actor.LegalEntityIds.Contains(legalEntityId) || !actor.ProjectIds.Contains(projectId))
        {
            throw new UnauthorizedAccessException("无权将该项目工程量写入中央账本。");
        }
    }
}
