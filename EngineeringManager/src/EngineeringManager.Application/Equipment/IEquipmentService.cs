namespace EngineeringManager.Application.Equipment;

public interface IEquipmentService
{
    Task<EquipmentDetailsDto> SaveEquipmentAsync(EquipmentActor actor, SaveEquipmentRequest request, CancellationToken token);
    Task<EquipmentDetailsDto> CopyEquipmentAsync(EquipmentActor actor, Guid sourceId, CancellationToken token);
    Task<EquipmentUsageDto> SaveUsageAsync(EquipmentActor actor, SaveEquipmentUsageRequest request, CancellationToken token);
    Task<EquipmentDashboardDto> GetDashboardAsync(EquipmentActor actor, EquipmentFilter filter, CancellationToken token);
    Task TransferOwnershipAsync(EquipmentActor actor, TransferEquipmentOwnershipRequest request, CancellationToken token);
    Task<Guid> SaveMaintenanceAsync(EquipmentActor actor, SaveEquipmentMaintenanceRequest request, CancellationToken token);
}
