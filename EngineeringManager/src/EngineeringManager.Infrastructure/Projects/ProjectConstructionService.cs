using System.Text.Json;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Application.Partners;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Projects;

public sealed class ProjectConstructionService(
    ApplicationDbContext db,
    IEquipmentService equipmentService,
    IBusinessPartnerService partnerService) : IProjectConstructionService
{
    public async Task<ProjectConstructionWorkspaceDto> GetWorkspaceAsync(Guid projectId, DateOnly today, CancellationToken token)
    {
        var records = await db.ProjectConstructionRecords.AsNoTracking()
            .Include(item => item.Equipment).Include(item => item.CrewBusinessPartner)
            .Include(item => item.TransferFromProject).Include(item => item.TransferToProject)
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.EntryDate).ThenByDescending(item => item.Id)
            .ToListAsync(token);
        var equipment = await db.Equipment.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.EquipmentNumber)
            .Select(item => new ProjectConstructionOptionDto(item.Id, item.EquipmentNumber + " · " + item.Name)).ToListAsync(token);
        var crews = await db.BusinessPartnerRoles.AsNoTracking().Where(item => item.RoleType == BusinessPartnerRoleType.ConstructionCrew && item.Partner.IsActive)
            .OrderBy(item => item.Partner.PartnerNumber).Select(item => new ProjectConstructionOptionDto(item.Partner.Id, item.Partner.PartnerNumber + " · " + item.Partner.ShortName)).ToListAsync(token);
        var projects = await db.Projects.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.ProjectNumber)
            .Select(item => new ProjectConstructionOptionDto(item.Id, item.ProjectNumber + " · " + item.Name)).ToListAsync(token);
        return new ProjectConstructionWorkspaceDto(records.Select(item => ToDto(item, today)).ToArray(), equipment, crews, projects);
    }

    public async Task<ProjectConstructionRecordDto> SaveAsync(ProjectConstructionActor actor, SaveProjectConstructionRecordRequest request, DateOnly today, CancellationToken token)
    {
        var reason = Required(request.Reason, "请填写修改原因。");
        ValidateSubject(request.RecordType, request.EquipmentId, request.CrewBusinessPartnerId);
        if (request.RecordType == ProjectConstructionRecordType.ConstructionCrew && request.ShowInProjectOverview)
            throw new ArgumentException("施工班组不能显示在项目总览。", nameof(request));
        ProjectConstructionCalculator.Calculate(request.EntryDate, request.ExitDate, request.StopDays, today);
        if (request.TransferFromProjectId == request.ProjectId || request.TransferToProjectId == request.ProjectId)
            throw new ArgumentException("调入或调出项目不能是当前项目。");
        var projectIds = new[] { (Guid?)request.ProjectId, request.TransferFromProjectId, request.TransferToProjectId }.Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        if (await db.Projects.CountAsync(item => projectIds.Contains(item.Id) && item.IsActive, token) != projectIds.Length)
            throw new InvalidOperationException("当前、调入或调出项目不存在或已停用。");
        await ValidateSubjectExistsAsync(request.RecordType, request.EquipmentId, request.CrewBusinessPartnerId, token);

        await using var transaction = await db.Database.BeginTransactionAsync(token);
        ProjectConstructionRecord record;
        object? before = null;
        if (request.Id.HasValue)
        {
            record = await db.ProjectConstructionRecords.SingleOrDefaultAsync(item => item.Id == request.Id, token)
                ?? throw new InvalidOperationException("施工详情记录不存在。");
            if (record.ProjectId != request.ProjectId) throw new InvalidOperationException("施工详情记录不属于当前项目。");
            if (record.ConcurrencyStamp != request.ConcurrencyStamp) throw new DbUpdateConcurrencyException("施工详情已被其他用户修改，请刷新后重试。");
            before = Snapshot(record);
            await UnlinkNextAsync(record, request.TransferToProjectId, token);
        }
        else
        {
            record = new ProjectConstructionRecord { ProjectId = request.ProjectId };
            db.ProjectConstructionRecords.Add(record);
        }

        record.RecordType = request.RecordType;
        record.EquipmentId = request.EquipmentId;
        record.CrewBusinessPartnerId = request.CrewBusinessPartnerId;
        record.TransferFromProjectId = request.TransferFromProjectId;
        record.TransferToProjectId = request.TransferToProjectId;
        record.EntryDate = request.EntryDate;
        record.ExitDate = request.ExitDate;
        record.StopDays = request.StopDays;
        record.Notes = Optional(request.Notes);
        record.IsDraft = !request.EntryDate.HasValue;
        record.ShowInProjectOverview = request.RecordType == ProjectConstructionRecordType.Equipment && request.ShowInProjectOverview;
        record.UpdatedAt = DateTimeOffset.UtcNow;
        record.ConcurrencyStamp = Guid.NewGuid();

        if (request.AutoConnectPrevious)
        {
            var previous = await MatchingRecords(request.RecordType, request.EquipmentId, request.CrewBusinessPartnerId)
                .Where(item => item.Id != record.Id && item.ProjectId != request.ProjectId && item.ExitDate != null && (request.EntryDate == null || item.ExitDate <= request.EntryDate))
                .OrderByDescending(item => item.ExitDate).ThenByDescending(item => item.Id).FirstOrDefaultAsync(token);
            if (previous is not null)
            {
                if (previous.NextRecordId.HasValue && previous.NextRecordId != record.Id) throw new InvalidOperationException("匹配到的上个项目记录已经连接到其他记录。");
                record.PreviousRecordId = previous.Id;
                record.TransferFromProjectId = previous.ProjectId;
                previous.NextRecordId = record.Id;
                previous.TransferToProjectId = record.ProjectId;
                previous.UpdatedAt = DateTimeOffset.UtcNow;
                previous.ConcurrencyStamp = Guid.NewGuid();
            }
        }

        if (record.TransferToProjectId.HasValue)
        {
            if (await WouldCreateCycleAsync(record, record.TransferToProjectId.Value, token)) throw new InvalidOperationException("项目流转不能形成循环。");
            var next = await MatchingRecords(record.RecordType, record.EquipmentId, record.CrewBusinessPartnerId)
                .SingleOrDefaultAsync(item => item.ProjectId == record.TransferToProjectId && item.PreviousRecordId == record.Id, token);
            if (next is null)
            {
                await db.SaveChangesAsync(token);
                next = new ProjectConstructionRecord
                {
                    ProjectId = record.TransferToProjectId.Value,
                    RecordType = record.RecordType,
                    EquipmentId = record.EquipmentId,
                    CrewBusinessPartnerId = record.CrewBusinessPartnerId,
                    TransferFromProjectId = record.ProjectId,
                    PreviousRecordId = record.Id,
                    IsDraft = true
                };
                db.ProjectConstructionRecords.Add(next);
                await db.SaveChangesAsync(token);
            }
            record.NextRecordId = next.Id;
        }

        db.AuditLogs.Add(new AuditLog
        {
            UserId = Required(actor.UserId, "操作人不能为空。"), UserName = Optional(actor.UserName),
            Action = request.Id.HasValue ? "UpdateProjectConstruction" : "CreateProjectConstruction",
            EntityType = nameof(ProjectConstructionRecord), EntityId = record.Id.ToString(), RelatedProjectId = record.ProjectId.ToString(),
            Reason = reason, BeforeJson = before is null ? null : JsonSerializer.Serialize(before), AfterJson = JsonSerializer.Serialize(Snapshot(record))
        });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        var saved = await db.ProjectConstructionRecords.AsNoTracking().Include(item => item.Equipment).Include(item => item.CrewBusinessPartner)
            .Include(item => item.TransferFromProject).Include(item => item.TransferToProject).SingleAsync(item => item.Id == record.Id, token);
        return ToDto(saved, today);
    }

    public async Task<ProjectConstructionOptionDto> CreateEquipmentAsync(ProjectConstructionActor actor, CreateProjectEquipmentRequest request, CancellationToken token)
    {
        var saved = await equipmentService.SaveEquipmentAsync(EquipmentActor.Administrator(actor.UserId), new SaveEquipmentRequest(
            null, request.EquipmentNumber, request.Name, request.Model, request.Category, request.OwnershipType,
            request.OwnerLegalEntityId, request.LessorBusinessPartnerId, request.InternalDailyRate, null, request.Reason), token);
        return new ProjectConstructionOptionDto(saved.Id, saved.EquipmentNumber + " · " + saved.Name);
    }

    public async Task<ProjectConstructionOptionDto> CreateCrewAsync(ProjectConstructionActor actor, CreateProjectCrewRequest request, CancellationToken token)
    {
        var contacts = string.IsNullOrWhiteSpace(request.ContactName)
            ? Array.Empty<PartnerContactRequest>()
            : [new PartnerContactRequest(request.ContactName.Trim(), Optional(request.ContactPhone), null, null, true)];
        var saved = await partnerService.CreateAsync(new CreateBusinessPartnerRequest(
            request.PartnerNumber, request.Name, request.Name, null, request.Reason,
            [new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, null, null, null)], contacts), token);
        return new ProjectConstructionOptionDto(saved.Id, saved.PartnerNumber + " · " + saved.ShortName);
    }

    private IQueryable<ProjectConstructionRecord> MatchingRecords(ProjectConstructionRecordType type, Guid? equipmentId, Guid? crewId) =>
        db.ProjectConstructionRecords.Where(item => item.RecordType == type &&
            (type == ProjectConstructionRecordType.Equipment ? item.EquipmentId == equipmentId : item.CrewBusinessPartnerId == crewId));

    private async Task UnlinkNextAsync(ProjectConstructionRecord record, Guid? requestedTargetProjectId, CancellationToken token)
    {
        if (!record.NextRecordId.HasValue || record.TransferToProjectId == requestedTargetProjectId) return;
        var oldNext = await db.ProjectConstructionRecords.SingleOrDefaultAsync(item => item.Id == record.NextRecordId, token);
        if (oldNext is not null && oldNext.PreviousRecordId == record.Id)
        {
            oldNext.PreviousRecordId = null;
            oldNext.TransferFromProjectId = null;
            oldNext.UpdatedAt = DateTimeOffset.UtcNow;
            oldNext.ConcurrencyStamp = Guid.NewGuid();
        }
        record.NextRecordId = null;
    }

    private async Task<bool> WouldCreateCycleAsync(ProjectConstructionRecord current, Guid targetProjectId, CancellationToken token)
    {
        var candidate = await MatchingRecords(current.RecordType, current.EquipmentId, current.CrewBusinessPartnerId)
            .Where(item => item.ProjectId == targetProjectId).OrderByDescending(item => item.EntryDate).ThenByDescending(item => item.Id).FirstOrDefaultAsync(token);
        var visited = new HashSet<Guid>();
        while (candidate is not null && visited.Add(candidate.Id))
        {
            if (candidate.Id == current.Id || candidate.ProjectId == current.ProjectId) return true;
            candidate = candidate.NextRecordId.HasValue
                ? await db.ProjectConstructionRecords.SingleOrDefaultAsync(item => item.Id == candidate.NextRecordId, token)
                : null;
        }
        return false;
    }

    private async Task ValidateSubjectExistsAsync(ProjectConstructionRecordType type, Guid? equipmentId, Guid? crewId, CancellationToken token)
    {
        if (type == ProjectConstructionRecordType.Equipment && !await db.Equipment.AnyAsync(item => item.Id == equipmentId && item.IsActive, token))
            throw new InvalidOperationException("设备不存在或已停用。");
        if (type == ProjectConstructionRecordType.ConstructionCrew && !await db.BusinessPartnerRoles.AnyAsync(item => item.BusinessPartnerId == crewId && item.RoleType == BusinessPartnerRoleType.ConstructionCrew && item.Partner.IsActive, token))
            throw new InvalidOperationException("施工班组不存在、已停用或未设置施工班组角色。");
    }

    private static void ValidateSubject(ProjectConstructionRecordType type, Guid? equipmentId, Guid? crewId)
    {
        if (type == ProjectConstructionRecordType.Equipment && (!equipmentId.HasValue || crewId.HasValue)) throw new ArgumentException("设备记录必须且只能选择一台设备。");
        if (type == ProjectConstructionRecordType.ConstructionCrew && (!crewId.HasValue || equipmentId.HasValue)) throw new ArgumentException("施工班组记录必须且只能选择一个班组。");
    }

    private static ProjectConstructionRecordDto ToDto(ProjectConstructionRecord item, DateOnly today)
    {
        var duration = ProjectConstructionCalculator.Calculate(item.EntryDate, item.ExitDate, item.StopDays, today);
        return new ProjectConstructionRecordDto(item.Id, item.ProjectId, item.RecordType,
            item.RecordType == ProjectConstructionRecordType.Equipment ? item.EquipmentId!.Value : item.CrewBusinessPartnerId!.Value,
            item.RecordType == ProjectConstructionRecordType.Equipment ? item.Equipment!.EquipmentNumber + " · " + item.Equipment.Name : item.CrewBusinessPartner!.PartnerNumber + " · " + item.CrewBusinessPartner.ShortName,
            item.TransferFromProjectId, item.TransferFromProject?.Name, item.EntryDate, item.ExitDate, duration.TotalDays, item.StopDays, duration.WorkDays,
            item.TransferToProjectId, item.TransferToProject?.Name, item.Notes, item.IsDraft, item.ConcurrencyStamp, item.ShowInProjectOverview);
    }

    private static object Snapshot(ProjectConstructionRecord item) => new { item.ProjectId, item.RecordType, item.EquipmentId, item.CrewBusinessPartnerId, item.TransferFromProjectId, item.TransferToProjectId, item.PreviousRecordId, item.NextRecordId, item.EntryDate, item.ExitDate, item.StopDays, item.Notes, item.IsDraft, item.ShowInProjectOverview, item.ConcurrencyStamp };
    private static string Required(string? value, string message) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentException(message);
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
