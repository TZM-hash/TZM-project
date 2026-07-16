using EngineeringManager.Domain.StageResults;

namespace EngineeringManager.Application.StageResults;

public sealed record StageResultLineRequest(Guid ContractLineItemId, decimal PeriodQuantity, string? Notes);

public sealed record StageAttachmentRequest(
    string StoredName,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    AttachmentCategory Category,
    string? Description);

public sealed record CreateStageResultRequest(
    Guid ProjectId,
    Guid? ContractId,
    string Title,
    StageResultType ResultType,
    StageResultStatus Status,
    DateOnly ResultDate,
    string? Description,
    QualityResult QualityResult,
    string? SubmittedByUserId,
    bool IsOfflineDraft,
    IReadOnlyCollection<StageResultLineRequest> Lines,
    IReadOnlyCollection<StageAttachmentRequest> Attachments);

public sealed record StageResultLineDto(
    Guid ContractLineItemId,
    string LineItemCode,
    string LineItemName,
    string Unit,
    decimal PeriodQuantity,
    decimal CumulativeQuantity,
    decimal RemainingQuantity,
    decimal CompletionPercentage,
    bool ExceedsTarget,
    string? Notes);

public sealed record StageAttachmentDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    AttachmentCategory Category,
    string? Description);

public sealed record StageResultDto(
    Guid Id,
    Guid ProjectId,
    Guid? ContractId,
    string Title,
    StageResultType ResultType,
    StageResultStatus Status,
    DateOnly ResultDate,
    string? Description,
    QualityResult QualityResult,
    bool IsOfflineDraft,
    IReadOnlyList<StageResultLineDto> Lines,
    IReadOnlyList<StageAttachmentDto> Attachments);
