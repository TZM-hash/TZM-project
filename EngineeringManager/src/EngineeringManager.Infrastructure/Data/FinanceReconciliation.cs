using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class FinanceReconciliation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LedgerScope Scope { get; set; }
    public FinanceReconciliationScope ReconciliationScope { get; set; }
    public Guid? FinanceBusinessYearId { get; set; }
    public FinanceBusinessYear? FinanceBusinessYear { get; set; }
    public Guid? LegalEntityId { get; set; }
    public LegalEntity? LegalEntity { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public BusinessPartner? BusinessPartner { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly AsOfDate { get; set; }
    public int Version { get; set; }
    public string QueryJson { get; set; } = string.Empty;
    public string MetricsJson { get; set; } = string.Empty;
    public string? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<FinanceReconciliationLine> Lines { get; set; } = [];
}
