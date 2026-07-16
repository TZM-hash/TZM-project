namespace EngineeringManager.Application.Users;

public sealed record UserAdminDto(
    string Id,
    string UserName,
    string DisplayName,
    bool IsEnabled,
    IReadOnlyList<string> Roles,
    string? PrimaryOrganization,
    IReadOnlyList<string> LegalEntities);
