using EngineeringManager.Domain.Employees;

namespace EngineeringManager.Application.Employees;

public sealed record CreateEmployeeRequest(
    string EmployeeNumber,
    string Name,
    EmployeeType EmployeeType,
    string? Phone = null,
    string? IdentityNumber = null,
    string? BankAccountNumber = null,
    string? BankName = null,
    DateOnly? HireDate = null,
    DateOnly? LeaveDate = null,
    string? PositionTitle = null,
    Guid? DefaultLegalEntityId = null,
    decimal? DefaultDailyRate = null,
    decimal? DefaultPieceworkRate = null,
    bool IsActive = true,
    decimal? DefaultMonthlySalary = null,
    decimal? DefaultHourlyRate = null,
    string? Notes = null);

public sealed record CopyEmployeeRequest(Guid SourceEmployeeId, string NewEmployeeNumber, string NewName);

public sealed record UpdateEmployeeRequest(
    Guid Id,
    string EmployeeNumber,
    string Name,
    EmployeeType EmployeeType,
    string? Phone,
    string? IdentityNumber,
    string? BankAccountNumber,
    string? BankName,
    DateOnly? HireDate,
    DateOnly? LeaveDate,
    string? PositionTitle,
    Guid? DefaultLegalEntityId,
    decimal? DefaultMonthlySalary,
    decimal? DefaultDailyRate,
    decimal? DefaultHourlyRate,
    decimal? DefaultPieceworkRate,
    bool IsActive,
    Guid ConcurrencyStamp,
    string Reason,
    string? Notes = null);

public sealed record CreateEmployeeAffiliationRequest(
    Guid EmployeeId,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid? DepartmentId,
    Guid? ProjectId,
    Guid? CrewBusinessPartnerId,
    Guid? LegalEntityId,
    string? PositionTitle,
    bool IsPrimary,
    string? Notes);

public sealed record EmployeeAffiliationDto(
    Guid Id,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid? DepartmentId,
    Guid? ProjectId,
    Guid? CrewBusinessPartnerId,
    Guid? LegalEntityId,
    string? PositionTitle,
    bool IsPrimary,
    string? Notes,
    string? DepartmentName = null,
    string? ProjectName = null,
    string? CrewBusinessPartnerName = null,
    string? LegalEntityName = null);

public sealed record EmployeeDto(
    Guid Id,
    string EmployeeNumber,
    string Name,
    EmployeeType EmployeeType,
    string? Phone,
    string? PositionTitle,
    Guid? DefaultLegalEntityId,
    decimal? DefaultMonthlySalary,
    decimal? DefaultDailyRate,
    decimal? DefaultHourlyRate,
    decimal? DefaultPieceworkRate,
    bool IsActive,
    IReadOnlyList<EmployeeAffiliationDto> Affiliations,
    string? IdentityNumber = null,
    string? BankAccountNumber = null,
    string? BankName = null,
    DateOnly? HireDate = null,
    DateOnly? LeaveDate = null,
    Guid ConcurrencyStamp = default,
    string? Notes = null);
