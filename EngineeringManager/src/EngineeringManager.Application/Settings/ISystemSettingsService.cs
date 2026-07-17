namespace EngineeringManager.Application.Settings;

public interface ISystemSettingsService
{
    Task<SystemDisplaySettings> GetAsync(CancellationToken token);
    Task SaveAsync(SettingsActor actor, SystemDisplaySettings settings, CancellationToken token);
}
