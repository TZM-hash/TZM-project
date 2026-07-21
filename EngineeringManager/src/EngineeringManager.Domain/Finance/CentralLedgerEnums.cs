namespace EngineeringManager.Domain.Finance;

public enum LedgerScope
{
    External = 1,
    Internal = 2
}

public enum LedgerDirection
{
    Receivable = 1,
    Payable = 2
}

public enum LedgerSettlementState
{
    Provisional = 1,
    Final = 2
}

public enum LedgerSourceType
{
    ProjectQuantity = 1,
    Crew = 2,
    Partner = 3,
    CentralLedger = 4,
    LegacyMigration = 5,
    ProjectCollection = 6
}

public enum LedgerAdjustmentType
{
    FinalSettlement = 1,
    Correction = 2,
    Reversal = 3
}

public enum LedgerCashType
{
    Collection = 1,
    Payment = 2,
    InternalTransfer = 3
}

public enum LedgerRecordStatus
{
    Active = 1,
    Voided = 2
}

public enum LedgerAllocationStatus
{
    Unallocated = 1,
    PartiallyAllocated = 2,
    FullyAllocated = 3
}

public enum FinanceRecordType
{
    Settlement = 1,
    Deduction = 2,
    Invoice = 3,
    Cash = 4,
    Adjustment = 5
}

public enum FinanceReconciliationScope
{
    WholeLedger = 1,
    LegalEntity = 2,
    BusinessPartner = 3
}
