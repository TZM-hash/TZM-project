using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Infrastructure.Data;

public sealed class FinanceDeletionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public FinanceRecordType RecordType { get; set; }
    public Guid RecordId { get; set; }
    public string? DeletedByUserId { get; set; }
    public string? DeletedByUserName { get; set; }
    public DateTimeOffset DeletedAt { get; set; } = DateTimeOffset.UtcNow;
    public string EntryPoint { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public string BeforeMetricsJson { get; set; } = string.Empty;
    public string AfterMetricsJson { get; set; } = string.Empty;
    public Guid? LegalEntityId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public Guid? CounterLegalEntityId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ContractId { get; set; }
}
