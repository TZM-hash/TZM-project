using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Application.Organization;

public sealed record OrganizationUnitDto(Guid Id, string Code, string Name, OrganizationUnitType UnitType, bool IsActive);

public sealed record LegalEntityDto(Guid Id, string Code, string Name, string ShortName, bool IsActive);

public sealed record OrganizationOverviewDto(
    IReadOnlyList<OrganizationUnitDto> OrganizationUnits,
    IReadOnlyList<LegalEntityDto> LegalEntities);

public sealed record CreateOrganizationUnitRequest(
    string Code,
    string Name,
    OrganizationUnitType UnitType,
    Guid? ParentId = null);

public sealed record CreateLegalEntityRequest(
    string Code,
    string Name,
    string ShortName,
    string? UnifiedSocialCreditCode = null);

public enum OrganizationOwnerKind
{
    LegalEntity = 1,
    BusinessPartner = 2
}

public sealed record DepartmentDto(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentId,
    string? ParentName,
    OrganizationOwnerKind OwnerKind,
    Guid OwnerId,
    string OwnerName,
    bool IsAuthorizationScope,
    bool IsActive,
    int CurrentPersonnelCount);

public sealed record SaveDepartmentRequest(
    Guid? Id,
    OrganizationOwnerKind OwnerKind,
    Guid OwnerId,
    string Code,
    string Name,
    Guid? ParentId,
    bool IsAuthorizationScope,
    bool IsActive);
