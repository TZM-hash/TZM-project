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
}
