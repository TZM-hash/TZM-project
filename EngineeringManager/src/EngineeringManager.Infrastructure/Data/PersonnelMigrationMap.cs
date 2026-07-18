namespace EngineeringManager.Infrastructure.Data;

public sealed class PersonnelMigrationMap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LegacyTemporaryWorkerId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTimeOffset MigratedAt { get; set; } = DateTimeOffset.UtcNow;
    public Employee Employee { get; set; } = null!;
}
