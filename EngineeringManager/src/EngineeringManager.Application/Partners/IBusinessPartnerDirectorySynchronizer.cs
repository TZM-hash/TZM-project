namespace EngineeringManager.Application.Partners;

public interface IBusinessPartnerDirectorySynchronizer
{
    Task SynchronizeAsync(Guid? projectId, CancellationToken cancellationToken);
}
