using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Infrastructure.Data;

public sealed class FinanceLegacyMap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LegacyEntityType { get; set; } = string.Empty;
    public string LegacyId { get; set; } = string.Empty;
    public FinanceRecordType CentralRecordType { get; set; }
    public Guid CentralRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
