using EngineeringManager.Domain.Equipment;

namespace EngineeringManager.Application.Equipment;

public sealed record EquipmentActor(
    string UserId,
    bool CanManage,
    bool CanSettle,
    bool CanOverrideSharedUsage,
    bool CanAccessAll,
    IReadOnlyCollection<Guid> AccessibleCompanyIds,
    IReadOnlyCollection<Guid> AccessibleProjectIds)
{
    public static EquipmentActor Administrator(string userId) => new(userId, true, true, true, true, [], []);
}

public sealed record SaveEquipmentRequest(
    Guid? Id,
    string EquipmentNumber,
    string Name,
    string? Model,
    string? Category,
    EquipmentOwnershipType OwnershipType,
    Guid? OwnerLegalEntityId,
    Guid? LessorBusinessPartnerId,
    decimal? InternalDailyRate,
    Guid? ConcurrencyStamp,
    string Reason,
    string? Notes = null);

public sealed record EquipmentDetailsDto(
    Guid Id,
    string EquipmentNumber,
    string Name,
    string? Model,
    string? Category,
    EquipmentOwnershipType OwnershipType,
    EquipmentStatus Status,
    Guid? OwnerLegalEntityId,
    Guid? LessorBusinessPartnerId,
    decimal? InternalDailyRate,
    Guid ConcurrencyStamp,
    string? Notes = null);

public sealed record EquipmentPeriodRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    EquipmentPeriodType PeriodType,
    bool IsChargeable,
    string? Notes);

public sealed record SaveEquipmentUsageRequest(
    Guid? Id,
    Guid EquipmentId,
    Guid ProjectId,
    Guid LegalEntityId,
    Guid? LeaseAgreementId,
    DateOnly EntryDate,
    DateOnly? ExitDate,
    RentMode RentMode,
    MonthlyProrationMode MonthlyProrationMode,
    decimal UnitRate,
    bool SharedUsageOverride,
    string? SharedUsageReason,
    IReadOnlyCollection<EquipmentPeriodRequest> Periods,
    Guid? ConcurrencyStamp,
    string Reason);

public sealed record EquipmentUsageDto(
    Guid Id,
    Guid EquipmentId,
    Guid ProjectId,
    Guid LegalEntityId,
    DateOnly EntryDate,
    DateOnly? ExitDate,
    int TotalDays,
    int WorkDays,
    int StopDays,
    int UnclassifiedDays,
    Guid ConcurrencyStamp);

public sealed record EquipmentFilter(Guid? CompanyId, Guid? ProjectId, EquipmentStatus? Status, string? Keyword);

public sealed record EquipmentDashboardDto(
    int TotalCount,
    int InUseCount,
    int IdleCount,
    int RentedCount,
    decimal SettledAmount,
    IReadOnlyDictionary<string, int> StatusDistribution,
    IReadOnlyList<EquipmentDetailsDto> Items);

public sealed record EquipmentSettlementAdjustmentRequest(
    EquipmentAdjustmentDirection Direction,
    string AdjustmentType,
    decimal Amount,
    string? Reason);

public sealed record FinalizeEquipmentSettlementRequest(
    Guid UsageId,
    DateOnly SettlementDate,
    IReadOnlyCollection<EquipmentSettlementAdjustmentRequest> Adjustments,
    bool GeneratePayable,
    string ModificationReason,
    Guid? ConcurrencyStamp,
    string? Notes = null);

public sealed record EquipmentSettlementDto(
    Guid Id,
    Guid UsageId,
    decimal BaseAmount,
    decimal TotalAmount,
    decimal OffsetAmount,
    decimal PayableAmount,
    Guid? PayableEntryId,
    Guid ConcurrencyStamp,
    string? Notes = null);

public sealed record TransferEquipmentOwnershipRequest(
    Guid EquipmentId,
    EquipmentTransferType TransferType,
    DateOnly TransferDate,
    Guid? ToLegalEntityId,
    string? ExternalRecipientName,
    decimal? TransferAmount,
    string Reason);

public sealed record SaveEquipmentMaintenanceRequest(
    Guid? Id,
    Guid EquipmentId,
    string? MaintenanceType,
    DateOnly? MaintenanceDate,
    DateOnly? NextDueDate,
    decimal? Amount,
    string? Provider,
    string? Notes,
    string Reason);
