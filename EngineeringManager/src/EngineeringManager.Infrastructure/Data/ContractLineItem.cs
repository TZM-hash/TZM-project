namespace EngineeringManager.Infrastructure.Data;

public sealed class ContractLineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? EstimatedQuantity { get; set; }
    public decimal? EstimatedUnitPrice { get; set; }
    public decimal? SettledQuantity { get; set; }
    public decimal? SettledUnitPrice { get; set; }
    public bool IsSettlementConfirmed { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<ContractLineItemLegalEntityAllocation> LegalEntityAllocations { get; set; } = [];
}
