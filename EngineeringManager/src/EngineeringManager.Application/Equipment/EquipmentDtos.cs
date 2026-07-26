using EngineeringManager.Application.Certificates;
using EngineeringManager.Domain.Certificates;
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
    string? Notes = null,
    Guid? ManagingLegalEntityId = null,
    DateOnly? PurchaseDate = null,
    decimal? PurchaseAmount = null,
    string? QualificationCertificateNumber = null,
    DateOnly? QualificationIssuedOn = null,
    DateOnly? QualificationExpiresOn = null,
    CertificateAttachmentUpload? NewQualificationAttachment = null,
    bool RemoveQualificationAttachment = false,
    bool IsActive = true,
    EquipmentStatus Status = EquipmentStatus.Idle);

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
    string? Notes = null,
    Guid? ManagingLegalEntityId = null,
    string? ManagingLegalEntityName = null,
    string? OwnerLegalEntityName = null,
    string? LessorBusinessPartnerName = null,
    DateOnly? PurchaseDate = null,
    decimal? PurchaseAmount = null,
    string? QualificationCertificateNumber = null,
    DateOnly? QualificationIssuedOn = null,
    DateOnly? QualificationExpiresOn = null,
    Guid? QualificationAttachmentId = null,
    string? QualificationAttachmentFileName = null,
    CertificateExpiryState QualificationState = CertificateExpiryState.LongTerm,
    bool IsActive = true);

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

public sealed record EquipmentUsageFilter(Guid? EquipmentId, DateOnly StartDate, DateOnly EndDate);

public sealed record EquipmentUsageHistoryDto(
    Guid Id,
    Guid EquipmentId,
    string EquipmentNumber,
    string EquipmentName,
    Guid ProjectId,
    string ProjectNumber,
    string ProjectName,
    Guid LegalEntityId,
    string LegalEntityName,
    DateOnly EntryDate,
    DateOnly? ExitDate,
    RentMode RentMode,
    decimal UnitRate,
    Guid ConcurrencyStamp,
    IReadOnlyList<EquipmentPeriodRequest> Periods);

public sealed record EquipmentFilter(Guid? CompanyId, Guid? ProjectId, EquipmentStatus? Status, string? Keyword, bool UnassignedOnly = false);

public sealed record EquipmentDashboardDto(
    int TotalCount,
    int InUseCount,
    int IdleCount,
    int RentedCount,
    decimal SettledAmount,
    IReadOnlyDictionary<string, int> StatusDistribution,
    IReadOnlyList<EquipmentDetailsDto> Items,
    int ExpiringQualificationCount = 0,
    int ExpiredQualificationCount = 0);

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
