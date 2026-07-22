using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class PayrollBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BatchNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayrollBatchType BatchType { get; set; }
    public PayrollDisbursementType DisbursementType { get; set; } = PayrollDisbursementType.Wage;
    public PayrollFundingSource FundingSource { get; set; } = PayrollFundingSource.CompanyAccount;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? LegalEntityId { get; set; }
    public LegalEntity? LegalEntity { get; set; }
    public DateOnly? PaymentDate { get; set; }
    public Guid? AccountId { get; set; }
    public FinancialAccount? Account { get; set; }
    public Guid? RepaysPersonalAdvanceAccountId { get; set; }
    public FinancialAccount? RepaysPersonalAdvanceAccount { get; set; }
    public decimal ActualAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public string? VoucherNumber { get; set; }
    public Guid? AccountTransactionId { get; set; }
    public bool IsUnifiedDisbursement { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? StageOrMilestoneName { get; set; }
    public PayrollBatchStatus Status { get; set; } = PayrollBatchStatus.Draft;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<PayrollItem> Items { get; set; } = [];
    public ICollection<PayrollPayment> Payments { get; set; } = [];
    public ICollection<PayrollCrewAllocation> CrewAllocations { get; set; } = [];
}
