namespace EngineeringManager.Domain.Employees;

public sealed record PayrollComponentInput(PayrollItemNature Nature, decimal Amount);

public sealed record PayrollSummary(
    decimal GrossEarnings,
    decimal DeductionAmount,
    decimal PayableAmount,
    decimal PaidAmount,
    decimal UnpaidAmount,
    bool HasOverpaymentRisk,
    bool HasDeductionRisk);

public static class PayrollCalculator
{
    public static PayrollSummary Calculate(IEnumerable<PayrollComponentInput> components, decimal paidAmount)
    {
        ArgumentNullException.ThrowIfNull(components);
        var items = components.ToArray();
        if (paidAmount < 0m || items.Any(item => item.Amount < 0m))
        {
            throw new ArgumentException("工资项目和实际支付金额不能为负数，冲减应使用独立工资项目。", nameof(components));
        }

        var grossEarnings = items.Where(item => item.Nature == PayrollItemNature.Earning).Sum(item => item.Amount);
        var deductions = items.Where(item => item.Nature == PayrollItemNature.Deduction).Sum(item => item.Amount);
        var payable = grossEarnings - deductions;
        var unpaid = payable - paidAmount;
        return new PayrollSummary(
            grossEarnings,
            deductions,
            payable,
            paidAmount,
            unpaid,
            paidAmount > payable,
            deductions > grossEarnings);
    }
}
