using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class UserOrganizationMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public Guid OrganizationUnitId { get; set; }

    public OrganizationUnit OrganizationUnit { get; set; } = null!;

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
