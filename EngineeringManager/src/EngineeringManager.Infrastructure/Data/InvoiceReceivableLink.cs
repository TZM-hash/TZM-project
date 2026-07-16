namespace EngineeringManager.Infrastructure.Data;

public sealed class InvoiceReceivableLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceEntryId { get; set; }
    public InvoiceEntry Invoice { get; set; } = null!;
    public Guid ReceivableEntryId { get; set; }
    public ReceivableEntry Receivable { get; set; } = null!;
    public decimal AllocatedAmount { get; set; }
}
