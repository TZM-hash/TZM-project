namespace EngineeringManager.Application.Organization;

public interface IOrganizationSummaryService
{
    Task<OrganizationSummaryDto> GetAsync(OrganizationSummaryQuery query, CancellationToken cancellationToken);
}
