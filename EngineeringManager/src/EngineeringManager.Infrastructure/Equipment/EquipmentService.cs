using System.Text.Json;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Equipment;

public sealed class EquipmentService(ApplicationDbContext db) : IEquipmentService
{
    public async Task<EquipmentDetailsDto> SaveEquipmentAsync(EquipmentActor actor, SaveEquipmentRequest request, CancellationToken token)
    {
        EnsureManage(actor);
        var number = Required(request.EquipmentNumber, "设备编号");
        var name = Required(request.Name, "设备名称");
        var reason = Required(request.Reason, "修改原因");
        if (request.OwnershipType == EquipmentOwnershipType.SelfOwned && !request.OwnerLegalEntityId.HasValue)
            throw new ArgumentException("自有设备必须选择所属公司。", nameof(request));
        if (request.OwnershipType == EquipmentOwnershipType.Rented && !request.LessorBusinessPartnerId.HasValue)
            throw new ArgumentException("租赁设备必须选择出租方。", nameof(request));
        if (request.OwnerLegalEntityId.HasValue && !await AuthorizedCompanies(actor).AnyAsync(item => item.Id == request.OwnerLegalEntityId, token))
            throw new InvalidOperationException("所属公司不存在或无权访问。");
        if (request.LessorBusinessPartnerId.HasValue && !await db.BusinessPartners.AnyAsync(item => item.Id == request.LessorBusinessPartnerId && item.IsActive, token))
            throw new InvalidOperationException("出租方不存在或已停用。");
        if (await db.Equipment.AnyAsync(item => item.EquipmentNumber == number && item.Id != request.Id, token))
            throw new InvalidOperationException($"设备编号已存在：{number}");

        Data.Equipment entity;
        string? before = null;
        if (request.Id.HasValue)
        {
            entity = await AuthorizedEquipment(actor).SingleOrDefaultAsync(item => item.Id == request.Id, token)
                ?? throw new KeyNotFoundException("设备不存在或无权访问。");
            if (!request.ConcurrencyStamp.HasValue || request.ConcurrencyStamp != entity.ConcurrencyStamp)
                throw new DbUpdateConcurrencyException("设备档案已被其他用户修改。");
            before = JsonSerializer.Serialize(Snapshot(entity));
        }
        else
        {
            entity = new Data.Equipment();
            db.Equipment.Add(entity);
        }
        entity.EquipmentNumber = number;
        entity.Name = name;
        entity.Model = Optional(request.Model);
        entity.Category = Optional(request.Category);
        entity.OwnershipType = request.OwnershipType;
        entity.OwnerLegalEntityId = request.OwnershipType == EquipmentOwnershipType.SelfOwned ? request.OwnerLegalEntityId : null;
        entity.LessorBusinessPartnerId = request.OwnershipType == EquipmentOwnershipType.Rented ? request.LessorBusinessPartnerId : null;
        entity.InternalDailyRate = request.InternalDailyRate;
        entity.Notes = Optional(request.Notes);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.ConcurrencyStamp = Guid.NewGuid();
        AddAudit(actor, request.Id.HasValue ? "Update" : "Create", nameof(Data.Equipment), entity.Id, reason, before, JsonSerializer.Serialize(Snapshot(entity)));
        await db.SaveChangesAsync(token);
        return ToDto(entity);
    }

    public async Task<EquipmentDetailsDto> CopyEquipmentAsync(EquipmentActor actor, Guid sourceId, CancellationToken token)
    {
        var source = await AuthorizedEquipment(actor).AsNoTracking().SingleOrDefaultAsync(item => item.Id == sourceId, token)
            ?? throw new KeyNotFoundException("设备不存在或无权访问。");
        return new EquipmentDetailsDto(Guid.Empty, string.Empty, $"{source.Name} - 副本", source.Model, source.Category,
            source.OwnershipType, EquipmentStatus.Idle, source.OwnerLegalEntityId, source.LessorBusinessPartnerId,
            source.InternalDailyRate, Guid.Empty, source.Notes);
    }

    public async Task<EquipmentUsageDto> SaveUsageAsync(EquipmentActor actor, SaveEquipmentUsageRequest request, CancellationToken token)
    {
        EnsureManage(actor);
        var reason = Required(request.Reason, "修改原因");
        var equipment = await AuthorizedEquipment(actor).SingleOrDefaultAsync(item => item.Id == request.EquipmentId, token)
            ?? throw new KeyNotFoundException("设备不存在或无权访问。");
        if (!actor.CanAccessAll && !actor.AccessibleProjectIds.Contains(request.ProjectId))
            throw new UnauthorizedAccessException("无权操作当前项目。");
        if (!await db.ProjectLegalEntities.AnyAsync(item => item.ProjectId == request.ProjectId && item.LegalEntityId == request.LegalEntityId, token))
            throw new InvalidOperationException("所选公司不是当前项目的签约公司。");
        if (request.SharedUsageOverride && (!actor.CanOverrideSharedUsage || string.IsNullOrWhiteSpace(request.SharedUsageReason)))
            throw new InvalidOperationException("共享使用必须由管理员填写原因。");
        if (request.ExitDate.HasValue)
            EquipmentUsageCalculator.Calculate(request.EntryDate, request.ExitDate.Value, request.Periods.Select(ToInput));
        else if (request.Periods.Count > 0)
            throw new InvalidOperationException("未填写退场日期时不能提交施工或停工日期段。");

        var end = request.ExitDate ?? DateOnly.MaxValue;
        var overlaps = await db.EquipmentProjectUsages.AsNoTracking().AnyAsync(item => item.EquipmentId == request.EquipmentId && item.Id != request.Id &&
            item.EntryDate <= end && (item.ExitDate == null || item.ExitDate >= request.EntryDate), token);
        if (overlaps && !request.SharedUsageOverride) throw new InvalidOperationException("设备使用日期与现有项目记录重叠。");

        EquipmentProjectUsage usage;
        string? before = null;
        if (request.Id.HasValue)
        {
            usage = await db.EquipmentProjectUsages.Include(item => item.Periods).SingleOrDefaultAsync(item => item.Id == request.Id, token)
                ?? throw new KeyNotFoundException("设备使用记录不存在。");
            if (!request.ConcurrencyStamp.HasValue || request.ConcurrencyStamp != usage.ConcurrencyStamp)
                throw new DbUpdateConcurrencyException("设备使用记录已被其他用户修改。");
            before = JsonSerializer.Serialize(UsageSnapshot(usage));
            db.EquipmentWorkPeriods.RemoveRange(usage.Periods);
        }
        else
        {
            usage = new EquipmentProjectUsage();
            db.EquipmentProjectUsages.Add(usage);
        }
        usage.EquipmentId = request.EquipmentId;
        usage.ProjectId = request.ProjectId;
        usage.LegalEntityId = request.LegalEntityId;
        usage.LeaseAgreementId = request.LeaseAgreementId;
        usage.EntryDate = request.EntryDate;
        usage.ExitDate = request.ExitDate;
        usage.RentMode = request.RentMode;
        usage.MonthlyProrationMode = request.MonthlyProrationMode;
        usage.UnitRate = request.UnitRate;
        usage.SharedUsageOverride = request.SharedUsageOverride;
        usage.SharedUsageReason = Optional(request.SharedUsageReason);
        usage.Notes = null;
        usage.ConcurrencyStamp = Guid.NewGuid();
        usage.Periods = request.Periods.Select(item => new EquipmentWorkPeriod { Usage = usage, StartDate = item.StartDate, EndDate = item.EndDate, PeriodType = item.PeriodType, IsChargeable = item.IsChargeable, Notes = Optional(item.Notes) }).ToList();
        equipment.Status = request.ExitDate.HasValue ? EquipmentStatus.Idle : EquipmentStatus.InUse;
        equipment.UpdatedAt = DateTimeOffset.UtcNow;
        equipment.ConcurrencyStamp = Guid.NewGuid();
        AddAudit(actor, request.Id.HasValue ? "Update" : "Create", nameof(EquipmentProjectUsage), usage.Id, reason, before, JsonSerializer.Serialize(UsageSnapshot(usage)));
        await db.SaveChangesAsync(token);
        return ToUsageDto(usage);
    }

    public async Task<EquipmentDashboardDto> GetDashboardAsync(EquipmentActor actor, EquipmentFilter filter, CancellationToken token)
    {
        var query = AuthorizedEquipment(actor).AsNoTracking();
        if (filter.CompanyId.HasValue) query = query.Where(item => item.OwnerLegalEntityId == filter.CompanyId || item.ProjectUsages.Any(usage => usage.LegalEntityId == filter.CompanyId));
        if (filter.ProjectId.HasValue) query = query.Where(item => item.ProjectUsages.Any(usage => usage.ProjectId == filter.ProjectId));
        if (filter.Status.HasValue) query = query.Where(item => item.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Keyword)) { var keyword = filter.Keyword.Trim(); query = query.Where(item => item.EquipmentNumber.Contains(keyword) || item.Name.Contains(keyword)); }
        var items = await query.OrderBy(item => item.EquipmentNumber).ToListAsync(token);
        var ids = items.Select(item => item.Id).ToHashSet();
        var settled = await db.EquipmentSettlements.AsNoTracking().Where(item => ids.Contains(item.Usage.EquipmentId)).SumAsync(item => (decimal?)item.TotalAmount, token) ?? 0m;
        var distribution = items.GroupBy(item => item.Status.ToString()).ToDictionary(group => group.Key, group => group.Count());
        return new EquipmentDashboardDto(items.Count, items.Count(item => item.Status == EquipmentStatus.InUse), items.Count(item => item.Status == EquipmentStatus.Idle), items.Count(item => item.OwnershipType == EquipmentOwnershipType.Rented), settled, distribution, items.Select(ToDto).ToArray());
    }

    public async Task TransferOwnershipAsync(EquipmentActor actor, TransferEquipmentOwnershipRequest request, CancellationToken token)
    {
        EnsureManage(actor);
        var equipment = await AuthorizedEquipment(actor).SingleOrDefaultAsync(item => item.Id == request.EquipmentId, token)
            ?? throw new KeyNotFoundException("设备不存在或无权访问。");
        if (equipment.OwnershipType != EquipmentOwnershipType.SelfOwned) throw new InvalidOperationException("仅自有设备可办理权属转让。");
        if (request.TransferType == EquipmentTransferType.InternalCompany && !request.ToLegalEntityId.HasValue)
            throw new ArgumentException("内部公司转让必须选择接收公司。", nameof(request));
        if (request.TransferType == EquipmentTransferType.ExternalSale && string.IsNullOrWhiteSpace(request.ExternalRecipientName))
            throw new ArgumentException("外部出售必须填写接收方。", nameof(request));
        if (request.ToLegalEntityId.HasValue && !await AuthorizedCompanies(actor).AnyAsync(item => item.Id == request.ToLegalEntityId, token))
            throw new InvalidOperationException("接收公司不存在或无权访问。");
        var history = new EquipmentOwnershipHistory
        {
            EquipmentId = equipment.Id,
            TransferType = request.TransferType,
            TransferDate = request.TransferDate,
            FromLegalEntityId = equipment.OwnerLegalEntityId,
            ToLegalEntityId = request.ToLegalEntityId,
            ExternalRecipientName = Optional(request.ExternalRecipientName),
            TransferAmount = request.TransferAmount,
            Notes = Required(request.Reason, "修改原因")
        };
        db.EquipmentOwnershipHistories.Add(history);
        equipment.OwnerLegalEntityId = request.TransferType == EquipmentTransferType.InternalCompany ? request.ToLegalEntityId : null;
        equipment.Status = request.TransferType == EquipmentTransferType.ExternalSale ? EquipmentStatus.TransferredOut : EquipmentStatus.Idle;
        equipment.ConcurrencyStamp = Guid.NewGuid();
        equipment.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit(actor, "Transfer", nameof(Data.Equipment), equipment.Id, history.Notes, null, JsonSerializer.Serialize(new { history.TransferType, history.TransferDate, history.FromLegalEntityId, history.ToLegalEntityId, history.ExternalRecipientName, history.TransferAmount }));
        await db.SaveChangesAsync(token);
    }

    public async Task<Guid> SaveMaintenanceAsync(EquipmentActor actor, SaveEquipmentMaintenanceRequest request, CancellationToken token)
    {
        EnsureManage(actor);
        if (!await AuthorizedEquipment(actor).AnyAsync(item => item.Id == request.EquipmentId, token)) throw new KeyNotFoundException("设备不存在或无权访问。");
        EquipmentMaintenanceRecord record;
        if (request.Id.HasValue)
        {
            record = await db.EquipmentMaintenanceRecords.SingleOrDefaultAsync(item => item.Id == request.Id && item.EquipmentId == request.EquipmentId, token)
                ?? throw new KeyNotFoundException("维保记录不存在。");
        }
        else
        {
            record = new EquipmentMaintenanceRecord { EquipmentId = request.EquipmentId };
            db.EquipmentMaintenanceRecords.Add(record);
        }
        record.MaintenanceType = Optional(request.MaintenanceType);
        record.MaintenanceDate = request.MaintenanceDate;
        record.NextDueDate = request.NextDueDate;
        record.Amount = request.Amount;
        record.Provider = Optional(request.Provider);
        record.Notes = Optional(request.Notes);
        AddAudit(actor, request.Id.HasValue ? "UpdateMaintenance" : "CreateMaintenance", nameof(EquipmentMaintenanceRecord), record.Id, Required(request.Reason, "修改原因"), null, JsonSerializer.Serialize(new { record.EquipmentId, record.MaintenanceType, record.MaintenanceDate, record.NextDueDate, record.Amount, record.Provider, record.Notes }));
        await db.SaveChangesAsync(token);
        return record.Id;
    }

    private IQueryable<Data.Equipment> AuthorizedEquipment(EquipmentActor actor)
    {
        var query = db.Equipment.AsQueryable();
        if (!actor.CanAccessAll)
        {
            var companies = actor.AccessibleCompanyIds.ToHashSet();
            var projects = actor.AccessibleProjectIds.ToHashSet();
            query = query.Where(item => (item.OwnerLegalEntityId.HasValue && companies.Contains(item.OwnerLegalEntityId.Value)) || item.ProjectUsages.Any(usage => companies.Contains(usage.LegalEntityId) && projects.Contains(usage.ProjectId)));
        }
        return query;
    }
    private IQueryable<Domain.Organization.LegalEntity> AuthorizedCompanies(EquipmentActor actor) => actor.CanAccessAll ? db.LegalEntities : db.LegalEntities.Where(item => actor.AccessibleCompanyIds.Contains(item.Id));
    private static EquipmentUsagePeriodInput ToInput(EquipmentPeriodRequest item) => new(item.StartDate, item.EndDate, item.PeriodType, item.IsChargeable);
    private static EquipmentDetailsDto ToDto(Data.Equipment item) => new(item.Id, item.EquipmentNumber, item.Name, item.Model, item.Category, item.OwnershipType, item.Status, item.OwnerLegalEntityId, item.LessorBusinessPartnerId, item.InternalDailyRate, item.ConcurrencyStamp, item.Notes);
    private static EquipmentUsageDto ToUsageDto(EquipmentProjectUsage item)
    {
        var calculation = item.ExitDate.HasValue ? EquipmentUsageCalculator.Calculate(item.EntryDate, item.ExitDate.Value, item.Periods.Select(period => new EquipmentUsagePeriodInput(period.StartDate, period.EndDate, period.PeriodType, period.IsChargeable))) : new EquipmentUsageCalculation(0, 0, 0, 0, 0, 0);
        return new EquipmentUsageDto(item.Id, item.EquipmentId, item.ProjectId, item.LegalEntityId, item.EntryDate, item.ExitDate, calculation.TotalDays, calculation.WorkDays, calculation.StopDays, calculation.UnclassifiedDays, item.ConcurrencyStamp);
    }
    private void AddAudit(EquipmentActor actor, string action, string type, Guid id, string reason, string? before, string? after) => db.AuditLogs.Add(new AuditLog { UserId = actor.UserId, Action = action, EntityType = type, EntityId = id.ToString(), Reason = reason, BeforeJson = before, AfterJson = after });
    private static object Snapshot(Data.Equipment item) => new { item.EquipmentNumber, item.Name, item.Model, item.Category, item.OwnershipType, item.Status, item.OwnerLegalEntityId, item.LessorBusinessPartnerId, item.InternalDailyRate, item.Notes };
    private static object UsageSnapshot(EquipmentProjectUsage item) => new { item.EquipmentId, item.ProjectId, item.LegalEntityId, item.EntryDate, item.ExitDate, item.RentMode, item.UnitRate, item.SharedUsageOverride, Periods = item.Periods.Select(period => new { period.StartDate, period.EndDate, period.PeriodType, period.IsChargeable }) };
    private static void EnsureManage(EquipmentActor actor) { if (!actor.CanManage) throw new UnauthorizedAccessException("当前用户没有设备管理权限。"); }
    private static string Required(string? value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name}不能为空。") : value.Trim();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
