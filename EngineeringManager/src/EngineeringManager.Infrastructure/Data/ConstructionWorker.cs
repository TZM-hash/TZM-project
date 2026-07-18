namespace EngineeringManager.Infrastructure.Data;

public sealed class ConstructionWorker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? IdentityNumber { get; set; }
    public string? Phone { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? Trade { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<ConstructionCrewMembership> Memberships { get; set; } = [];
}
