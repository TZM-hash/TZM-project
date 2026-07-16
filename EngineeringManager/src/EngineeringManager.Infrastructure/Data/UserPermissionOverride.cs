using EngineeringManager.Domain.Security;

namespace EngineeringManager.Infrastructure.Data;

public sealed class UserPermissionOverride
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public string PermissionKey { get; set; } = string.Empty;

    public PermissionEffect Effect { get; set; }

    public string? Reason { get; set; }

    public string? UpdatedByUserId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
