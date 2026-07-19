using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Finance;

public sealed class FinancePostingService(ApplicationDbContext db) : IFinancePostingService
{
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

        var state = lineItem.IsSettlementConfirmed ? LedgerSettlementState.Final : LedgerSettlementState.Provisional;
        var amount = lineItem.IsSettlementConfirmed
            ? (lineItem.SettledQuantity ?? 0m) * (lineItem.SettledUnitPrice ?? 0m)
            : (lineItem.EstimatedQuantity ?? 0m) * (lineItem.EstimatedUnitPrice ?? 0m);
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
                    amount,
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
        if (existing.SettlementState == LedgerSettlementState.Provisional && state == LedgerSettlementState.Provisional)
        {
            existing.OriginalAmount = amount;
            existing.OriginalInvoiceAmount = amount;
            existing.Notes = lineItem.Notes;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.ConcurrencyStamp = Guid.NewGuid();
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
                    amount,
                    "工程量确认形成最终结算差额",
                    existing.ConcurrencyStamp),
                token);
            return existing.Id;
        }

        if (currentAmount != amount || currentInvoiceAmount != amount)
        {
            var adjustment = new FinanceSettlementAdjustment
            {
                Settlement = existing,
                AdjustmentType = LedgerAdjustmentType.Correction,
                AmountDelta = amount - currentAmount,
                InvoiceAmountDelta = amount - currentInvoiceAmount,
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
