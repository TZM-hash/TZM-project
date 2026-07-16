namespace EngineeringManager.Application.Equipment;

public interface IEquipmentSettlementService
{
    Task<EquipmentSettlementDto> FinalizeAsync(EquipmentActor actor, FinalizeEquipmentSettlementRequest request, CancellationToken token);
    Task<Guid> GeneratePayableAsync(EquipmentActor actor, Guid settlementId, CancellationToken token);
}
