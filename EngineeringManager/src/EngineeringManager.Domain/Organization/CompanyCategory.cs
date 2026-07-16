namespace EngineeringManager.Domain.Organization;

public sealed class CompanyCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class CompanyCategoryDefaults
{
    public static readonly Guid GeneralTaxpayerCompanyId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid SmallScaleCompanyId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid SmallScaleSoleProprietorId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid OtherId = Guid.Parse("10000000-0000-0000-0000-000000000004");
}

public sealed record CompanyAccountDefault(
    bool IsDefaultCollection,
    bool IsDefaultPayment,
    bool IsDefaultInvoice);

public static class CompanyAccountRules
{
    public static void Validate(IEnumerable<CompanyAccountDefault> accounts)
    {
        var items = accounts.ToArray();
        EnsureAtMostOne(items.Count(item => item.IsDefaultCollection), "默认收款账户");
        EnsureAtMostOne(items.Count(item => item.IsDefaultPayment), "默认付款账户");
        EnsureAtMostOne(items.Count(item => item.IsDefaultInvoice), "默认开票账户");
    }

    private static void EnsureAtMostOne(int count, string label)
    {
        if (count > 1)
        {
            throw new InvalidOperationException($"每家公司最多只能设置一个{label}。");
        }
    }
}
