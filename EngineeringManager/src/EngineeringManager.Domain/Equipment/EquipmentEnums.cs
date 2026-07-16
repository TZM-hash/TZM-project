namespace EngineeringManager.Domain.Equipment;

public enum EquipmentOwnershipType { SelfOwned = 1, Rented = 2 }
public enum EquipmentStatus { Idle = 1, InUse = 2, Maintenance = 3, Disabled = 4, Scrapped = 5, TransferredOut = 6 }
public enum EquipmentPeriodType { Work = 1, Stop = 2 }
public enum RentMode { Daily = 1, Monthly = 2, StagePackage = 3 }
public enum MonthlyProrationMode { CalendarMonth = 1, ThirtyDay = 2 }
public enum EquipmentAdjustmentDirection { Addition = 1, Deduction = 2 }
public enum EquipmentTransferType { InternalCompany = 1, ExternalSale = 2 }
public enum EquipmentAdvancePaymentType { Deposit = 1, Prepayment = 2, TemporaryPayment = 3, DepositReturn = 4 }

public sealed record EquipmentUsagePeriodInput(
    DateOnly StartDate,
    DateOnly EndDate,
    EquipmentPeriodType PeriodType,
    bool IsChargeable);

public sealed record EquipmentUsageCalculation(
    int TotalDays,
    int WorkDays,
    int StopDays,
    int ChargeableStopDays,
    int UnclassifiedDays,
    int ChargeableDays);

public sealed record EquipmentRentAdjustmentInput(EquipmentAdjustmentDirection Direction, decimal Amount);

public sealed record EquipmentRentInput(
    RentMode RentMode,
    decimal UnitRate,
    MonthlyProrationMode MonthlyProrationMode,
    DateOnly EntryDate,
    DateOnly ExitDate,
    IReadOnlyCollection<EquipmentUsagePeriodInput> Periods,
    IReadOnlyCollection<EquipmentRentAdjustmentInput> Adjustments);

public sealed record EquipmentRentCalculation(decimal BaseAmount, decimal AdditionAmount, decimal DeductionAmount, decimal TotalAmount);
