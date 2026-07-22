namespace EngineeringManager.Domain.Finance;

public static class InvoiceAmountValidator
{
    public static void Validate(decimal netAmount, decimal taxAmount, decimal grossAmount, decimal taxRate)
    {
        if (netAmount < 0m || taxAmount < 0m || grossAmount < 0m || taxRate < 0m)
        {
            throw new ArgumentException("发票金额和税率不能为负数。");
        }

        if (Math.Abs(netAmount + taxAmount - grossAmount) > 0.01m)
        {
            throw new ArgumentException("不含税金额加税额必须等于含税金额。", nameof(grossAmount));
        }
    }

    public static (decimal NetAmount, decimal TaxAmount) SplitGross(decimal grossAmount, decimal taxRate)
    {
        if (grossAmount < 0m || taxRate < 0m)
        {
            throw new ArgumentException("发票金额和税率不能为负数。");
        }

        var net = Math.Round(grossAmount / (1m + taxRate), 2, MidpointRounding.AwayFromZero);
        var tax = Math.Round(grossAmount - net, 2, MidpointRounding.AwayFromZero);
        Validate(net, tax, grossAmount, taxRate);
        return (net, tax);
    }
}
