namespace EngineeringManager.Application.Offline;

public interface IOfflineStageResultService
{
    Task<IReadOnlyList<OfflineProjectOptionDto>> GetProjectOptionsAsync(OfflineSyncActor actor, CancellationToken cancellationToken);
    Task<OfflineDraftSyncResultDto> SyncDraftAsync(OfflineSyncActor actor, OfflineDraftSyncRequest request, CancellationToken cancellationToken);
    Task<OfflinePhotoSyncResultDto> SyncPhotoAsync(OfflineSyncActor actor, OfflinePhotoSyncRequest request, CancellationToken cancellationToken);
    Task ReportFailureAsync(OfflineSyncActor actor, Guid clientDraftId, string errorMessage, CancellationToken cancellationToken);
}
