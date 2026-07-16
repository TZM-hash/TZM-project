namespace EngineeringManager.Application.Companies;

public interface ICompanyActorService
{
    Task<CompanyActor> ResolveAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
}
