using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.StageResults;

namespace EngineeringManager.Application.EquipmentOffline;

public sealed record EquipmentOfflineSyncRequest(Guid ClientDraftId, Guid OperationId, Guid? BaseServerVersion, SaveEquipmentUsageRequest Usage);
public sealed record EquipmentOfflineSyncResult(Guid UsageId, Guid ServerVersion, bool IsIdempotent, bool IsConflict, string? Message);
public sealed record EquipmentOfflinePhotoRequest(Guid ClientDraftId, Guid ClientAttachmentId, string OriginalFileName, string ContentType, long SizeBytes, Stream Content, AttachmentCategory Category, string? Description);
public sealed record EquipmentOfflinePhotoResult(Guid AttachmentId, bool IsIdempotent);

public interface IEquipmentOfflineService
{
    Task<EquipmentOfflineSyncResult> SyncAsync(EquipmentActor actor, EquipmentOfflineSyncRequest request, CancellationToken token);
    Task<EquipmentOfflinePhotoResult> SyncPhotoAsync(EquipmentActor actor, EquipmentOfflinePhotoRequest request, CancellationToken token);
}
