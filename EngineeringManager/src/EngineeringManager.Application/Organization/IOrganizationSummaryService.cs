namespace EngineeringManager.Application.Organization;

public interface IOrganizationSummaryService
{
    Task<OrganizationSummaryDto> GetAsync(OrganizationSummaryQuery query, CancellationToken cancellationToken);

    async Task<IReadOnlyList<OrganizationSummaryDto>> GetManyAsync(
        IReadOnlyCollection<OrganizationSummaryQuery> queries,
        CancellationToken cancellationToken)
    {
        var summaries = new List<OrganizationSummaryDto>(queries.Count);
        foreach (var query in queries)
        {
            summaries.Add(await GetAsync(query, cancellationToken));
        }
        return summaries;
    }
}
