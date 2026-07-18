namespace EngineeringManager.Application.ConstructionCrews;

public sealed record CreateConstructionWorkerRequest(
    Guid CrewBusinessPartnerId,
    string Name,
    string? IdentityNumber,
    string? Phone,
    string? BankAccountNumber,
    string? BankName,
    string? Trade,
    DateOnly StartDate,
    string? Notes,
    string Reason);

public sealed record TransferConstructionWorkerRequest(
    Guid ConstructionWorkerId,
    Guid NewCrewBusinessPartnerId,
    DateOnly TransferDate,
    string Reason);

public sealed record ConstructionWorkerDto(
    Guid Id,
    string Name,
    string? IdentityNumber,
    string? Phone,
    string? BankAccountNumber,
    string? BankName,
    string? Trade,
    Guid CrewBusinessPartnerId,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    string? Notes,
    Guid ConcurrencyStamp);

public sealed record ConstructionCrewPaymentBatchDto(
    Guid PayrollBatchId,
    string BatchNumber,
    DateOnly PaymentDate,
    Guid? ProjectId,
    string? ProjectName,
    decimal Amount,
    int WorkerCount,
    IReadOnlyList<ConstructionCrewPaymentLineDto> Lines);

public sealed record ConstructionCrewPaymentLineDto(
    Guid PayrollPaymentId,
    Guid ConstructionWorkerId,
    string RecipientName,
    string? IdentityNumber,
    string? Trade,
    decimal Amount);

public sealed record ConstructionCrewListItemDto(
    Guid Id,
    string PartnerNumber,
    string Name,
    string ShortName,
    string? TradeCategory,
    string? PrimaryContact,
    string? PrimaryPhone,
    int CurrentWorkerCount,
    int ProjectCount,
    decimal TotalPaidAmount,
    DateOnly? LastPaymentDate,
    bool IsActive);

public sealed record ConstructionCrewDetailsDto(
    ConstructionCrewListItemDto Crew,
    IReadOnlyList<ConstructionWorkerDto> Workers,
    IReadOnlyList<ConstructionCrewPaymentBatchDto> PaymentBatches);
