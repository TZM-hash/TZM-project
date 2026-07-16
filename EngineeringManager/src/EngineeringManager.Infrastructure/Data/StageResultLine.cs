namespace EngineeringManager.Infrastructure.Data;

public sealed class StageResultLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StageResultId { get; set; }
    public StageResult StageResult { get; set; } = null!;
    public Guid ContractLineItemId { get; set; }
    public ContractLineItem ContractLineItem { get; set; } = null!;
    public decimal PeriodQuantity { get; set; }
    public decimal CumulativeQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal CompletionPercentage { get; set; }
    public bool ExceedsTarget { get; set; }
    public string? Notes { get; set; }
}
