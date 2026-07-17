using EngineeringManager.Application.Settings;

namespace EngineeringManager.Infrastructure.Data;

public sealed class SavedDataView
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string PageKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string FilterJson { get; set; } = "{}";
    public string ColumnJson { get; set; } = "[]";
    public string? SortKey { get; set; }
    public bool SortDescending { get; set; }
    public TableDensity RowDensity { get; set; } = TableDensity.Standard;
    public int PageSize { get; set; } = 20;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
