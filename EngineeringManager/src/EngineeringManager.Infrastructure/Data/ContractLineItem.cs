using System.ComponentModel.DataAnnotations.Schema;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ContractLineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? AccountingLabel { get; set; }
    public bool RequiresInvoice { get; set; } = true;
    [NotMapped] public decimal? EstimatedQuantity { get => Quantity; set => Quantity = value; }
    [NotMapped] public decimal? EstimatedUnitPrice { get => UnitPrice; set => UnitPrice = value; }
    [NotMapped] public decimal? SettledQuantity { get => Quantity; set { if (value.HasValue) Quantity = value; } }
    [NotMapped] public decimal? SettledUnitPrice { get => UnitPrice; set { if (value.HasValue) UnitPrice = value; } }
    [NotMapped] public bool IsSettlementConfirmed { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<ContractLineItemLegalEntityAllocation> LegalEntityAllocations { get; set; } = [];
}
