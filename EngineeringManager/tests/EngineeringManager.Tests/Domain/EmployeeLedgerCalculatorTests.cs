using EngineeringManager.Domain.Employees;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class EmployeeLedgerCalculatorTests
{
    [Fact]
    public void ExpensesAdvancesAndOtherPaymentsUseSeparateBalances()
    {
        var summary = EmployeeLedgerCalculator.Calculate(
            expensePayableAmount: 1000m,
            expensePaymentAmount: 800m,
            expenseRefundOrReversalAmount: 100m,
            advanceDisbursementAmount: 2000m,
            advanceRepaymentAmount: 500m,
            advancePayrollDeductionAmount: 300m,
            otherPayableAmount: 1000m,
            otherPaymentAmount: 1200m);

        summary.ExpensePaidAmount.Should().Be(700m);
        summary.ExpenseUnpaidAmount.Should().Be(300m);
        summary.AdvanceOutstandingAmount.Should().Be(1200m);
        summary.OtherUnpaidAmount.Should().Be(-200m);
        summary.HasOtherOverpaymentRisk.Should().BeTrue();
        summary.HasAdvanceOverSettlementRisk.Should().BeFalse();
    }

    [Fact]
    public void NegativeInputsAreRejectedBecauseReversalsUseSeparateRecords()
    {
        var action = () => EmployeeLedgerCalculator.Calculate(-1m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);

        action.Should().Throw<ArgumentException>();
    }
}
