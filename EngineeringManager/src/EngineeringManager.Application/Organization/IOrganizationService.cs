namespace EngineeringManager.Application.Organization;

public interface IOrganizationService
{
    Task<OrganizationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);

    Task<OrganizationUnitDto> CreateOrganizationUnitAsync(
        CreateOrganizationUnitRequest request,
        CancellationToken cancellationToken);

    Task<LegalEntityDto> CreateLegalEntityAsync(
        CreateLegalEntityRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(
        OrganizationOwnerKind ownerKind,
        Guid ownerId,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<DepartmentDto> SaveDepartmentAsync(
        SaveDepartmentRequest request,
        CancellationToken cancellationToken);

    Task DeactivateDepartmentAsync(
        Guid departmentId,
        CancellationToken cancellationToken);
}
