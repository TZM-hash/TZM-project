namespace EngineeringManager.Application.TemporaryWorkers;

public sealed record CreateTemporaryWorkerRequest(
    string Name,
    string? IdentityNumber,
    string? Phone,
    string? BankAccountNumber,
    string? BankName,
    string? Trade,
    Guid? DefaultProjectId,
    string? Notes,
    string Reason);

public sealed record UpdateTemporaryWorkerRequest(
    Guid Id,
    string Name,
    string? IdentityNumber,
    string? Phone,
    string? BankAccountNumber,
    string? BankName,
    string? Trade,
    Guid? DefaultProjectId,
    string? Notes,
    bool IsActive,
    Guid ConcurrencyStamp,
    string Reason);

public sealed record TemporaryWorkerPaymentDto(
    Guid PayrollBatchId,
    Guid PayrollPaymentId,
    string BatchNumber,
    DateOnly PaymentDate,
    Guid? ProjectId,
    string? ProjectName,
    decimal Amount,
    string? Notes);

public sealed record TemporaryWorkerDto(
    Guid Id,
    string Name,
    string? IdentityNumber,
    string? Phone,
    string? BankAccountNumber,
    string? BankName,
    string? Trade,
    Guid? DefaultProjectId,
    Guid? ConvertedEmployeeId,
    string? Notes,
    bool IsActive,
    bool HasPotentialDuplicate,
    int PaymentCount,
    decimal TotalPaidAmount,
    DateOnly? LastPaymentDate,
    Guid ConcurrencyStamp,
    IReadOnlyList<TemporaryWorkerPaymentDto> Payments);
