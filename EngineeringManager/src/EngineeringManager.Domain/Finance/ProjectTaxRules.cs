namespace EngineeringManager.Domain.Finance;

public static class ProjectTaxRules
{
    public static IReadOnlyList<decimal> AllowedRates { get; } = [0.01m, 0.03m, 0.06m, 0.09m, 0.13m];

    public static bool IsAllowedRate(decimal rate) => AllowedRates.Contains(rate);
}
