namespace EngineeringManager.Infrastructure.Data;

public sealed class UserDataScope
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public PermissionScopeType ScopeType { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public Guid? LegalEntityId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
