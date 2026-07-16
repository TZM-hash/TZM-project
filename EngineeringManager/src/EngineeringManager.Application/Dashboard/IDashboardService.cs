namespace EngineeringManager.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(DashboardActor actor, CancellationToken cancellationToken);
}
