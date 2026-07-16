namespace EngineeringManager.Domain.Employees;

public sealed record EmployeeLedgerSummary(
    decimal ExpensePayableAmount,
    decimal ExpensePaidAmount,
    decimal ExpenseUnpaidAmount,
    decimal AdvanceOutstandingAmount,
    decimal OtherPayableAmount,
    decimal OtherPaidAmount,
    decimal OtherUnpaidAmount,
    bool HasExpenseOverpaymentRisk,
    bool HasOtherOverpaymentRisk,
    bool HasAdvanceOverSettlementRisk);

public static class EmployeeLedgerCalculator
{
    public static EmployeeLedgerSummary Calculate(
        decimal expensePayableAmount,
        decimal expensePaymentAmount,
        decimal expenseRefundOrReversalAmount,
        decimal advanceDisbursementAmount,
        decimal advanceRepaymentAmount,
        decimal advancePayrollDeductionAmount,
        decimal otherPayableAmount,
        decimal otherPaymentAmount)
    {
        var values = new[]
        {
            expensePayableAmount,
            expensePaymentAmount,
            expenseRefundOrReversalAmount,
            advanceDisbursementAmount,
            advanceRepaymentAmount,
            advancePayrollDeductionAmount,
            otherPayableAmount,
            otherPaymentAmount
        };
        if (values.Any(value => value < 0m))
        {
            throw new ArgumentException("员工往来金额不能为负数，退回和冲销应使用独立记录。");
        }

        var expensePaid = expensePaymentAmount - expenseRefundOrReversalAmount;
        var expenseUnpaid = expensePayableAmount - expensePaid;
        var advanceOutstanding = advanceDisbursementAmount - advanceRepaymentAmount - advancePayrollDeductionAmount;
        var otherUnpaid = otherPayableAmount - otherPaymentAmount;
        return new EmployeeLedgerSummary(
            expensePayableAmount,
            expensePaid,
            expenseUnpaid,
            advanceOutstanding,
            otherPayableAmount,
            otherPaymentAmount,
            otherUnpaid,
            expensePaid > expensePayableAmount,
            otherPaymentAmount > otherPayableAmount,
            advanceOutstanding < 0m);
    }
}
