using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Finance;

public sealed class CentralLedgerAllocationService(ApplicationDbContext db)
{
    public Task<IReadOnlyList<FinanceAllocationRequest>> BuildAutomaticInvoiceAllocationsAsync(
        CentralLedgerActor actor,
        LedgerScope scope,
        LedgerDirection direction,
        Guid legalEntityId,
        Guid? businessPartnerId,
        Guid? counterLegalEntityId,
        decimal headerAmount,
        CancellationToken token)
    {
        return BuildAutomaticAllocationsAsync(
            actor,
            scope,
            direction,
            legalEntityId,
            businessPartnerId,
            counterLegalEntityId,
            headerAmount,
            forInvoice: true,
            token);
    }

    public Task<IReadOnlyList<FinanceAllocationRequest>> BuildAutomaticCashAllocationsAsync(
        CentralLedgerActor actor,
        LedgerScope scope,
        LedgerDirection direction,
        Guid legalEntityId,
        Guid? businessPartnerId,
        Guid? counterLegalEntityId,
        decimal headerAmount,
        CancellationToken token)
    {
        return BuildAutomaticAllocationsAsync(
            actor,
            scope,
            direction,
            legalEntityId,
            businessPartnerId,
            counterLegalEntityId,
            headerAmount,
            forInvoice: false,
            token);
    }

    private async Task<IReadOnlyList<FinanceAllocationRequest>> BuildAutomaticAllocationsAsync(
        CentralLedgerActor actor,
        LedgerScope scope,
        LedgerDirection direction,
        Guid legalEntityId,
        Guid? businessPartnerId,
        Guid? counterLegalEntityId,
        decimal headerAmount,
        bool forInvoice,
        CancellationToken token)
    {
        if (headerAmount <= 0m) throw new ArgumentOutOfRangeException(nameof(headerAmount), "单据金额必须大于零。");

        var settlements = await db.FinanceSettlements
            .AsNoTracking()
            .Where(item => item.Status == LedgerRecordStatus.Active &&
                item.Scope == scope &&
                item.Direction == direction &&
                item.LegalEntityId == legalEntityId &&
                item.BusinessPartnerId == businessPartnerId &&
                item.CounterLegalEntityId == counterLegalEntityId &&
                (!item.ProjectId.HasValue || actor.ProjectIds.Contains(item.ProjectId.Value)))
            .Include(item => item.Adjustments)
            .Include(item => item.Deductions)
            .Include(item => item.InvoiceAllocations).ThenInclude(item => item.Invoice)
            .Include(item => item.CashAllocations).ThenInclude(item => item.CashEntry)
            .OrderBy(item => item.BusinessDate)
            .ThenBy(item => item.Id)
            .ToListAsync(token);

        var remaining = headerAmount;
        var order = 1;
        var result = new List<FinanceAllocationRequest>();
        foreach (var settlement in settlements)
        {
            if (remaining <= 0m) break;
            var adjustments = settlement.Adjustments.Where(item => item.Status == LedgerRecordStatus.Active).ToArray();
            var deductions = settlement.Deductions.Where(item => item.Status == LedgerRecordStatus.Active).ToArray();
            var gross = settlement.OriginalAmount + adjustments.Sum(item => item.AmountDelta);
            var actual = Math.Max(gross - deductions.Sum(item => item.Amount), 0m);
            var shouldInvoice = Math.Max(
                settlement.OriginalInvoiceAmount + adjustments.Sum(item => item.InvoiceAmountDelta) -
                deductions.Where(item => item.ReduceInvoiceAmount).Sum(item => item.Amount),
                0m);
            var alreadyAllocated = forInvoice
                ? settlement.InvoiceAllocations.Where(item => item.Invoice.Status == LedgerRecordStatus.Active).Sum(item => item.Amount)
                : settlement.CashAllocations.Where(item => item.CashEntry.Status == LedgerRecordStatus.Active)
                    .Sum(item => item.CashEntry.IsReversal ? -item.Amount : item.Amount);
            var capacity = Math.Max((forInvoice ? shouldInvoice : actual) - alreadyAllocated, 0m);
            if (capacity <= 0m) continue;
            var amount = Math.Min(remaining, capacity);
            result.Add(new FinanceAllocationRequest(settlement.Id, amount, order++));
            remaining -= amount;
        }

        return result;
    }
}
