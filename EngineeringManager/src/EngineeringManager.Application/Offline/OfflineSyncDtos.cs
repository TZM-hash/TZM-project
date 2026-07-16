using EngineeringManager.Domain.Offline;
using EngineeringManager.Domain.StageResults;

namespace EngineeringManager.Application.Offline;

public sealed record OfflineSyncActor(string UserId, bool CanAccessAllProjects);

public sealed record OfflineLineItemRequest(Guid ContractLineItemId, decimal PeriodQuantity, string? Notes);

public sealed record OfflineDraftSyncRequest(
    Guid ClientDraftId,
    Guid OperationId,
    Guid? ServerStageResultId,
    Guid? BaseServerVersion,
    Guid ProjectId,
    Guid? ContractId,
    string Title,
    StageResultType ResultType,
    DateOnly ResultDate,
    string? Description,
    QualityResult QualityResult,
    IReadOnlyCollection<OfflineLineItemRequest> Lines);

public sealed record OfflineLineSnapshotDto(Guid ContractLineItemId, decimal PeriodQuantity, string? Notes);

public sealed record OfflineDraftSnapshotDto(
    Guid StageResultId,
    Guid ServerVersion,
    Guid ProjectId,
    Guid? ContractId,
    string Title,
    StageResultType ResultType,
    DateOnly ResultDate,
    string? Description,
    QualityResult QualityResult,
    IReadOnlyList<OfflineLineSnapshotDto> Lines);

public sealed record OfflineDraftSyncResultDto(
    OfflineSyncStatus Status,
    Guid ServerStageResultId,
    Guid ServerVersion,
    bool IsIdempotent,
    OfflineDraftSnapshotDto? ServerSnapshot,
    string? ErrorMessage);

public sealed record OfflinePhotoSyncRequest(
    Guid ClientDraftId,
    Guid ClientAttachmentId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Stream Content,
    AttachmentCategory Category,
    string? Description);

public sealed record OfflinePhotoSyncResultDto(Guid AttachmentId, bool IsIdempotent);
public sealed record OfflineFailureReport(Guid ClientDraftId, string ErrorMessage);

public sealed record OfflineLineItemOptionDto(Guid Id, string Code, string Name, string Unit);
public sealed record OfflineContractOptionDto(Guid Id, string Number, string Name, IReadOnlyList<OfflineLineItemOptionDto> LineItems);
public sealed record OfflineProjectOptionDto(Guid Id, string Number, string Name, IReadOnlyList<OfflineContractOptionDto> Contracts);
