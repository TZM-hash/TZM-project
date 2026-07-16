using System.Text.Json;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Equipment;

public sealed class EquipmentSettlementService(ApplicationDbContext db) : IEquipmentSettlementService
{
    public async Task<EquipmentSettlementDto> FinalizeAsync(EquipmentActor actor, FinalizeEquipmentSettlementRequest request, CancellationToken token)
    {
        if (!actor.CanSettle) throw new UnauthorizedAccessException("当前用户没有设备结算权限。");
        var reason = Required(request.ModificationReason);
        var usage = await db.EquipmentProjectUsages.Include(item => item.Periods).Include(item => item.AdvancePayments).Include(item => item.Equipment)
            .SingleOrDefaultAsync(item => item.Id == request.UsageId, token) ?? throw new KeyNotFoundException("设备使用记录不存在。");
        if (!usage.ExitDate.HasValue) throw new InvalidOperationException("设备退场后才能进行最终结算。");
        if (!actor.CanAccessAll && (!actor.AccessibleCompanyIds.Contains(usage.LegalEntityId) || !actor.AccessibleProjectIds.Contains(usage.ProjectId)))
            throw new UnauthorizedAccessException("无权结算当前设备使用记录。");
        var periods = usage.Periods.Select(item => new EquipmentUsagePeriodInput(item.StartDate, item.EndDate, item.PeriodType, item.IsChargeable)).ToArray();
        var usageResult = EquipmentUsageCalculator.Calculate(usage.EntryDate, usage.ExitDate.Value, periods);
        if (usageResult.UnclassifiedDays > 0) throw new InvalidOperationException("存在未分类日期，不能进行最终结算。");
        var rent = EquipmentRentCalculator.Calculate(new EquipmentRentInput(usage.RentMode, usage.UnitRate, usage.MonthlyProrationMode, usage.EntryDate, usage.ExitDate.Value, periods,
            request.Adjustments.Select(item => new EquipmentRentAdjustmentInput(item.Direction, item.Amount)).ToArray()));
        var offset = usage.AdvancePayments.Where(item => item.PaymentType is EquipmentAdvancePaymentType.Prepayment or EquipmentAdvancePaymentType.TemporaryPayment).Sum(item => item.Amount)
            - usage.AdvancePayments.Where(item => item.PaymentType == EquipmentAdvancePaymentType.DepositReturn).Sum(item => item.Amount);

        var settlement = await db.EquipmentSettlements.Include(item => item.Adjustments).SingleOrDefaultAsync(item => item.UsageId == usage.Id, token);
        string? before = null;
        if (settlement is null)
        {
            settlement = new EquipmentSettlement { UsageId = usage.Id };
            db.EquipmentSettlements.Add(settlement);
        }
        else
        {
            if (!request.ConcurrencyStamp.HasValue || request.ConcurrencyStamp != settlement.ConcurrencyStamp)
                throw new DbUpdateConcurrencyException("设备结算已被其他用户修改。");
            before = JsonSerializer.Serialize(Snapshot(settlement));
            db.EquipmentSettlementAdjustments.RemoveRange(settlement.Adjustments);
        }
        settlement.SettlementDate = request.SettlementDate;
        settlement.BaseAmount = rent.BaseAmount;
        settlement.TotalAmount = rent.TotalAmount;
        settlement.OffsetAmount = offset;
        settlement.ModificationReason = reason;
        settlement.PreviousSnapshotJson = before;
        settlement.UpdatedAt = DateTimeOffset.UtcNow;
        settlement.ConcurrencyStamp = Guid.NewGuid();
        settlement.Adjustments = request.Adjustments.Select(item => new EquipmentSettlementAdjustment { Settlement = settlement, Direction = item.Direction, AdjustmentType = Required(item.AdjustmentType), Amount = item.Amount, Reason = Optional(item.Reason) }).ToList();
        db.AuditLogs.Add(new AuditLog { UserId = actor.UserId, Action = before is null ? "Finalize" : "Revise", EntityType = nameof(EquipmentSettlement), EntityId = settlement.Id.ToString(), Reason = reason, BeforeJson = before, AfterJson = JsonSerializer.Serialize(Snapshot(settlement)) });
        await db.SaveChangesAsync(token);
        if (request.GeneratePayable) await GeneratePayableAsync(actor, settlement.Id, token);
        return ToDto(settlement);
    }

    public async Task<Guid> GeneratePayableAsync(EquipmentActor actor, Guid settlementId, CancellationToken token)
    {
        if (!actor.CanSettle) throw new UnauthorizedAccessException("当前用户没有设备结算权限。");
        var settlement = await db.EquipmentSettlements.Include(item => item.Usage).ThenInclude(item => item.Equipment).SingleOrDefaultAsync(item => item.Id == settlementId, token)
            ?? throw new KeyNotFoundException("设备结算不存在。");
        if (settlement.PayableEntryId.HasValue) return settlement.PayableEntryId.Value;
        var partnerId = settlement.Usage.Equipment.LessorBusinessPartnerId ?? throw new InvalidOperationException("自有设备不生成对外应付。");
        var amount = settlement.TotalAmount - settlement.OffsetAmount;
        if (amount <= 0m) throw new InvalidOperationException("抵扣后应付金额必须大于零。");
        var payable = new PayableEntry { ProjectId = settlement.Usage.ProjectId, LegalEntityId = settlement.Usage.LegalEntityId, BusinessPartnerId = partnerId, SourceType = PayableSourceType.Settlement, EntryDate = settlement.SettlementDate, Amount = amount, Description = $"设备结算：{settlement.Usage.Equipment.Name}" };
        db.PayableEntries.Add(payable);
        settlement.PayableEntryId = payable.Id;
        await db.SaveChangesAsync(token);
        return payable.Id;
    }

    private static EquipmentSettlementDto ToDto(EquipmentSettlement item) => new(item.Id, item.UsageId, item.BaseAmount, item.TotalAmount, item.OffsetAmount, item.TotalAmount - item.OffsetAmount, item.PayableEntryId, item.ConcurrencyStamp);
    private static object Snapshot(EquipmentSettlement item) => new { item.SettlementDate, item.BaseAmount, item.TotalAmount, item.OffsetAmount, item.PayableEntryId, item.ModificationReason, item.ConcurrencyStamp, Adjustments = item.Adjustments.Select(adjustment => new { adjustment.Direction, adjustment.AdjustmentType, adjustment.Amount, adjustment.Reason }) };
    private static string Required(string? value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。") : value.Trim();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
