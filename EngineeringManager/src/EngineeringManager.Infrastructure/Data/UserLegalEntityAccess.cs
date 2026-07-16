using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class UserLegalEntityAccess
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public Guid LegalEntityId { get; set; }

    public LegalEntity LegalEntity { get; set; } = null!;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
