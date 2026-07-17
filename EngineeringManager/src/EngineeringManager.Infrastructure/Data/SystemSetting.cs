namespace EngineeringManager.Infrastructure.Data;

public sealed class SystemSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedByUserId { get; set; }
    public ApplicationUser? UpdatedByUser { get; set; }
}
