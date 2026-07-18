namespace EngineeringManager.Infrastructure.Data;

public sealed class ConstructionCrewMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConstructionWorkerId { get; set; }
    public ConstructionWorker Worker { get; set; } = null!;
    public Guid CrewBusinessPartnerId { get; set; }
    public BusinessPartner CrewBusinessPartner { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsPrimary { get; set; }
    public string? Notes { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
