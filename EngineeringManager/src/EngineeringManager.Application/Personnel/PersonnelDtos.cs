using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Personnel;

namespace EngineeringManager.Application.Personnel;

public sealed record CreatePersonRequest(
    string PersonNumber,
    string Name,
    PersonnelScope Scope,
    EmployeeType? InternalType,
    ExternalPersonnelType? ExternalType,
    string? Phone = null,
    string? IdentityNumber = null,
    string? BankAccountNumber = null,
    string? BankName = null,
    Guid? LegalEntityId = null,
    Guid? BusinessPartnerId = null,
    Guid? OrganizationUnitId = null,
    Guid? ProjectId = null,
    Guid? CrewBusinessPartnerId = null,
    string? PositionTitle = null,
    DateOnly? EffectiveDate = null,
    string? Notes = null,
    string Reason = "新增人员");

public sealed record SavePersonRequest(
    Guid PersonId,
    string Name,
    string? Phone,
    string? IdentityNumber,
    string? BankAccountNumber,
    string? BankName,
    string? Notes,
    bool IsActive,
    Guid ConcurrencyStamp,
    string Reason);

public sealed record SavePersonnelAffiliationRequest(
    Guid PersonId,
    PersonnelScope Scope,
    EmployeeType? InternalType,
    ExternalPersonnelType? ExternalType,
    Guid? LegalEntityId,
    Guid? BusinessPartnerId,
    Guid? OrganizationUnitId,
    Guid? ProjectId,
    Guid? CrewBusinessPartnerId,
    string? PositionTitle,
    DateOnly EffectiveDate,
    string Reason,
    Guid? ConcurrencyStamp = null);

public sealed record SwitchPersonnelScopeRequest(
    Guid PersonId,
    PersonnelScope Scope,
    EmployeeType? InternalType,
    ExternalPersonnelType? ExternalType,
    Guid? LegalEntityId,
    Guid? BusinessPartnerId,
    Guid? OrganizationUnitId,
    Guid? ProjectId,
    Guid? CrewBusinessPartnerId,
    string? PositionTitle,
    DateOnly EffectiveDate,
    string Reason);

public sealed record PersonnelListQuery(
    PersonnelScope Scope,
    string? Search = null,
    Guid? LegalEntityId = null,
    Guid? BusinessPartnerId = null,
    Guid? OrganizationUnitId = null,
    EmployeeType? InternalType = null,
    ExternalPersonnelType? ExternalType = null,
    bool? IsActive = null,
    DateOnly? AsOf = null);

public sealed record PersonnelAffiliationDto(
    Guid Id,
    PersonnelScope Scope,
    EmployeeType? InternalType,
    ExternalPersonnelType? ExternalType,
    Guid? LegalEntityId,
    string? LegalEntityName,
    Guid? BusinessPartnerId,
    string? BusinessPartnerName,
    Guid? OrganizationUnitId,
    string? OrganizationUnitName,
    Guid? ProjectId,
    string? ProjectName,
    Guid? CrewBusinessPartnerId,
    string? CrewBusinessPartnerName,
    string? PositionTitle,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsPrimary,
    string? Notes,
    Guid ConcurrencyStamp);

public sealed record PersonnelListItemDto(
    Guid Id,
    string PersonNumber,
    string Name,
    string? Phone,
    bool IsActive,
    PersonnelScope Scope,
    EmployeeType? InternalType,
    ExternalPersonnelType? ExternalType,
    Guid? EmployeeId,
    Guid? ConstructionWorkerId,
    PersonnelAffiliationDto? CurrentAffiliation);

public sealed record PersonnelDetailsDto(
    Guid Id,
    string PersonNumber,
    string Name,
    string? Phone,
    string? IdentityNumber,
    string? BankAccountNumber,
    string? BankName,
    string? Notes,
    bool IsActive,
    Guid ConcurrencyStamp,
    Guid? EmployeeId,
    Guid? ConstructionWorkerId,
    PersonnelAffiliationDto? CurrentAffiliation,
    IReadOnlyList<PersonnelAffiliationDto> EngagementHistory);

public sealed record PersonnelOrganizationOptionDto(Guid Id, string Name, bool IsCrew);

public sealed record PersonnelDepartmentOptionDto(
    Guid Id,
    string Code,
    string Name,
    Guid? LegalEntityId,
    Guid? BusinessPartnerId);

public sealed record PersonnelProjectOptionDto(
    Guid Id,
    string Name,
    IReadOnlyList<Guid> LegalEntityIds,
    IReadOnlyList<Guid> BusinessPartnerIds);

public sealed record PersonnelOptionSetDto(
    IReadOnlyList<PersonnelOrganizationOptionDto> LegalEntities,
    IReadOnlyList<PersonnelOrganizationOptionDto> BusinessPartners,
    IReadOnlyList<PersonnelDepartmentOptionDto> Departments,
    IReadOnlyList<PersonnelProjectOptionDto> Projects,
    IReadOnlyList<PersonnelOrganizationOptionDto> Crews);
