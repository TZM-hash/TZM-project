namespace EngineeringManager.Application.Users;

public interface IUserAdministrationService
{
    Task<IReadOnlyList<UserAdminDto>> GetUsersAsync(CancellationToken cancellationToken);

    Task SetEnabledAsync(
        string userId,
        bool isEnabled,
        bool callerIsSystemAdministrator,
        CancellationToken cancellationToken);

    Task SetRolesAsync(
        string userId,
        IReadOnlyCollection<string> roles,
        bool callerIsSystemAdministrator,
        CancellationToken cancellationToken);
}
