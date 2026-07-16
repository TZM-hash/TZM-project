namespace EngineeringManager.Infrastructure.Data;

public sealed class InvoiceLineItemLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceEntryId { get; set; }
    public InvoiceEntry Invoice { get; set; } = null!;
    public Guid ContractLineItemId { get; set; }
    public ContractLineItem ContractLineItem { get; set; } = null!;
    public decimal AllocatedAmount { get; set; }
}
