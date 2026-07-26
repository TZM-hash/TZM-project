using EngineeringManager.Application.Certificates;

namespace EngineeringManager.Application.Equipment;

public interface IEquipmentService
{
    Task<EquipmentDetailsDto> GetEquipmentAsync(EquipmentActor actor, Guid id, CancellationToken token);
    Task<CertificateFileDto> DownloadQualificationAttachmentAsync(EquipmentActor actor, Guid equipmentId, CancellationToken token);
    Task<EquipmentDetailsDto> SaveEquipmentAsync(EquipmentActor actor, SaveEquipmentRequest request, CancellationToken token);
    Task DeleteEquipmentAsync(EquipmentActor actor, Guid id, Guid concurrencyStamp, string confirmationNumber, string reason, CancellationToken token);
    Task<EquipmentDetailsDto> CopyEquipmentAsync(EquipmentActor actor, Guid sourceId, CancellationToken token);
    Task<EquipmentUsageDto> SaveUsageAsync(EquipmentActor actor, SaveEquipmentUsageRequest request, CancellationToken token);
    Task<IReadOnlyList<EquipmentUsageHistoryDto>> ListUsagesAsync(EquipmentActor actor, EquipmentUsageFilter filter, CancellationToken token);
    Task<EquipmentDashboardDto> GetDashboardAsync(EquipmentActor actor, EquipmentFilter filter, CancellationToken token);
    Task TransferOwnershipAsync(EquipmentActor actor, TransferEquipmentOwnershipRequest request, CancellationToken token);
    Task<Guid> SaveMaintenanceAsync(EquipmentActor actor, SaveEquipmentMaintenanceRequest request, CancellationToken token);
}
