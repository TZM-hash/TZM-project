using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class EmployeeWageEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public Guid BusinessYearId { get; set; }
    public BusinessYear BusinessYear { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public EmployeeWageEntryType EntryType { get; set; } = EmployeeWageEntryType.Attendance;
    public EmployeeWageCategory WageCategory { get; set; }
    public EmployeeWageCalculationMethod CalculationMethod { get; set; }
    public PayrollItemNature Nature { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal AutomaticAmount { get; set; }
    public Guid? LegalEntityId { get; set; }
    public LegalEntity? LegalEntity { get; set; }
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? LaborBusinessPartnerId { get; set; }
    public BusinessPartner? LaborBusinessPartner { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public Guid? AttachmentId { get; set; }
    public Attachment? Attachment { get; set; }
    public string? Notes { get; set; }
    public Guid? SourcePayrollItemId { get; set; }
    public PayrollItem? SourcePayrollItem { get; set; }
    public Guid? SourcePersonalAdvanceBatchId { get; set; }
    public PayrollBatch? SourcePersonalAdvanceBatch { get; set; }
    public bool IsSystemGenerated { get; set; }
    public bool ExcludeFromWageCost { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
