namespace EngineeringManager.Infrastructure.Data;

public sealed class FinanceReconciliationLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReconciliationId { get; set; }
    public FinanceReconciliation Reconciliation { get; set; } = null!;
    public Guid SettlementId { get; set; }
    public Guid LegalEntityId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public Guid? CounterLegalEntityId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? ContractLineItemId { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string MetricsJson { get; set; } = string.Empty;
}
