using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ProjectConstructionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public ProjectConstructionRecordType RecordType { get; set; }
    public Guid? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    public Guid? CrewBusinessPartnerId { get; set; }
    public BusinessPartner? CrewBusinessPartner { get; set; }
    public Guid? TransferFromProjectId { get; set; }
    public Project? TransferFromProject { get; set; }
    public Guid? TransferToProjectId { get; set; }
    public Project? TransferToProject { get; set; }
    public Guid? PreviousRecordId { get; set; }
    public ProjectConstructionRecord? PreviousRecord { get; set; }
    public Guid? NextRecordId { get; set; }
    public ProjectConstructionRecord? NextRecord { get; set; }
    public DateOnly? EntryDate { get; set; }
    public DateOnly? ExitDate { get; set; }
    public int StopDays { get; set; }
    public string? Notes { get; set; }
    public bool IsDraft { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
