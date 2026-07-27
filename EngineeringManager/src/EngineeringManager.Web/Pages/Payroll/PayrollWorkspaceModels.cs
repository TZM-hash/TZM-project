using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Web.Pages.Payroll;

public sealed record PayrollSelectOption(Guid Id, string Label);

public sealed record PayrollAccountOption(
    Guid Id,
    Guid LegalEntityId,
    string Label,
    FinancialAccountType Type,
    string? OwnerName,
    Guid? OwnerEmployeeId);

public sealed record PayrollRecipientItemViewModel(string Name, string? GroupName, decimal Amount);

public sealed record PayrollRecipientBreakdownViewModel(
    IReadOnlyList<PayrollRecipientItemViewModel> Employees,
    IReadOnlyList<PayrollRecipientItemViewModel> CrewWorkers)
{
    public static PayrollRecipientBreakdownViewModel Empty { get; } = new([], []);
    public int EmployeeCount => Employees.Count;
    public int CrewCount => CrewWorkers.Count;
    public int TotalCount => EmployeeCount + CrewCount;
}

public sealed class PayrollEditorInput
{
    public string BatchNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly? PaymentDate { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? LegalEntityId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? PersonalAdvanceAccountId { get; set; }
    public decimal ActualAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public string? VoucherNumber { get; set; }
    public PayrollBatchStatus Status { get; set; } = PayrollBatchStatus.Draft;
    public string? Notes { get; set; }
    public PayrollDisbursementType DisbursementType { get; set; } = PayrollDisbursementType.Wage;
    public PayrollFundingSource FundingSource { get; set; } = PayrollFundingSource.CompanyAccount;
    public Guid? RepaysPersonalAdvanceAccountId { get; set; }
    public Guid? ConcurrencyStamp { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<PayrollPersonLineInput> EmployeeLines { get; set; } = [];
    public List<PayrollPersonLineInput> CrewLines { get; set; } = [];
}

public sealed class PayrollPersonLineInput
{
    public Guid? PaymentId { get; set; }
    public Guid PersonId { get; set; }
    public Guid? CrewBusinessPartnerId { get; set; }
    public string? CrewName { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool Selected { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public PayrollPaymentCategory PaymentCategory { get; set; } = PayrollPaymentCategory.Wage;
    public EmployeeWageCategory? WageCategory { get; set; } = EmployeeWageCategory.SocialSecurityWage;
    public Guid? LaborBusinessPartnerId { get; set; }
    public Guid? ProjectId { get; set; }
}

public sealed record PayrollEditorViewModel(
    Guid? EditorId,
    Guid? LineId,
    string? ReturnUrl,
    PayrollEditorInput Input,
    IReadOnlyList<PayrollSelectOption> Projects,
    IReadOnlyList<PayrollSelectOption> Companies,
    IReadOnlyList<PayrollAccountOption> Accounts,
    IReadOnlyList<PayrollSelectOption> LaborPartners)
{
    public IEnumerable<PayrollAccountOption> CompanyAccounts => Accounts.Where(item => item.Type != FinancialAccountType.PersonalAdvance);
    public IEnumerable<PayrollAccountOption> PersonalAdvanceAccounts => Accounts.Where(item => item.Type == FinancialAccountType.PersonalAdvance && item.OwnerEmployeeId.HasValue);
}
