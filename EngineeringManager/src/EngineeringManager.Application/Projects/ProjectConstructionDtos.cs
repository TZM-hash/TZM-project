using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Application.Projects;

public sealed record ProjectConstructionActor(string UserId, string? UserName);

public sealed record ProjectConstructionOptionDto(Guid Id, string Label);

public sealed record ProjectConstructionRecordDto(
    Guid Id, Guid ProjectId, ProjectConstructionRecordType RecordType, Guid SubjectId, string SubjectLabel,
    Guid? TransferFromProjectId, string? TransferFromProjectName, DateOnly? EntryDate, DateOnly? ExitDate,
    int TotalDays, int StopDays, int WorkDays, Guid? TransferToProjectId, string? TransferToProjectName,
    string? Notes, bool IsDraft, Guid ConcurrencyStamp, bool ShowInProjectOverview = false);

public sealed record ProjectConstructionWorkspaceDto(
    IReadOnlyList<ProjectConstructionRecordDto> Records,
    IReadOnlyList<ProjectConstructionOptionDto> Equipment,
    IReadOnlyList<ProjectConstructionOptionDto> Crews,
    IReadOnlyList<ProjectConstructionOptionDto> Projects);

public sealed record SaveProjectConstructionRecordRequest(
    Guid? Id, Guid ProjectId, ProjectConstructionRecordType RecordType, Guid? EquipmentId,
    Guid? CrewBusinessPartnerId, Guid? TransferFromProjectId, Guid? TransferToProjectId,
    DateOnly? EntryDate, DateOnly? ExitDate, int StopDays, string? Notes, bool AutoConnectPrevious,
    Guid? ConcurrencyStamp, string Reason, bool ShowInProjectOverview = false);

public sealed record CreateProjectEquipmentRequest(
    string EquipmentNumber, string Name, string? Model, string? Category,
    EquipmentOwnershipType OwnershipType, Guid? OwnerLegalEntityId, Guid? LessorBusinessPartnerId,
    decimal? InternalDailyRate, string Reason);

public sealed record CreateProjectCrewRequest(
    string PartnerNumber, string Name, string? ContactName, string? ContactPhone, string Reason);
