namespace EngineeringManager.Domain.Finance;

public enum FinancialAccountType { Bank = 1, Cash = 2, Other = 3 }
public enum ReceivableSourceType { ContractMilestone = 1, StageSettlement = 2, Manual = 3 }
public enum PayableSourceType { Settlement = 1, Contract = 2, Manual = 3 }
public enum FinancialAdjustmentType { Refund = 1, Reversal = 2, NegativeAdjustment = 3 }
public enum InvoiceDirection { Output = 1, Input = 2 }
public enum InvoiceStatus { Draft = 1, IssuedOrReceived = 2, Voided = 3 }
public enum PaymentMethod { BankTransfer = 1, Cash = 2, WeChat = 3, Alipay = 4, Other = 5 }
public enum AccountTransactionDirection { Inflow = 1, Outflow = 2 }
public enum AccountTransactionSourceType
{
    Collection = 1,
    Payment = 2,
    Refund = 3,
    PaymentReversal = 4,
    TransferOut = 5,
    TransferIn = 6,
    Adjustment = 7,
    PayrollPayment = 8,
    ExpensePayment = 9,
    EmployeeAdvanceDisbursement = 10,
    EmployeeAdvanceRepayment = 11,
    EmployeeOtherPayment = 12,
    EmployeeReceipt = 13,
    PayrollPaymentReversal = 14
}
