namespace EngineeringManager.Infrastructure.Data;

public sealed class BusinessYear
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
