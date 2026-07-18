namespace EngineeringManager.Domain.Employees;

public enum EmployeeType { Formal = 1, Labor = 2 }
public enum PayrollBatchType { Monthly = 1, DateRange = 2, ProjectStage = 3, Milestone = 4, Temporary = 5 }
public enum PayrollBatchStatus { Draft = 1, Confirmed = 2, Closed = 3, Voided = 4, ModifiedPendingReview = 5 }
public enum PayrollRecipientType { Employee = 1, CrewWorker = 2, TemporaryWorker = 3 }
public enum PayrollItemNature { Earning = 1, Deduction = 2 }
public enum PayrollItemType
{
    FixedSalary = 1,
    DailyWage = 2,
    HourlyWage = 3,
    Piecework = 4,
    LumpSum = 5,
    Overtime = 6,
    Bonus = 7,
    Allowance = 8,
    AdvanceDeduction = 9,
    LeaveDeduction = 10,
    Penalty = 11,
    OtherDeduction = 12,
    BackPay = 13,
    Reversal = 14
}

public enum PayrollPayeeType { Employee = 1, CrewLeader = 2, EntrustedRecipient = 3 }
public enum EmployeeLedgerEntryType { Expense = 1, AdvanceDisbursement = 2, AdvanceRepayment = 3, Dividend = 4, Interest = 5, Other = 6 }
public enum EmployeeLedgerStatus { Pending = 1, PartiallyPaid = 2, Paid = 3, Returned = 4, Reversed = 5 }
public enum EmployeeLedgerRecordKind { Payable = 1, Payment = 2, RefundOrReversal = 3 }
public enum EmployeeAdvanceAction { Disbursement = 1, Repayment = 2, PayrollDeduction = 3 }
public enum EmployeeWageCategory { SocialSecurityWage = 1, MigrantWorkerWage = 2, OtherWage = 3 }
public enum EmployeeWageCalculationMethod { Monthly = 1, Daily = 2, Hourly = 3, Piecework = 4, FixedAmount = 5, CustomUnit = 6 }
public enum EmployeeFinancialAdjustmentType { AdministratorAdjustment = 1, HistoricalOpeningBalance = 2, Reversal = 3 }
public enum EmployeeReceiptType { Wage = 1, Expense = 2, DividendOrOther = 3, Advance = 4, General = 5 }
