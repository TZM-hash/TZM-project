namespace EngineeringManager.Infrastructure.Development;

public static class SampleDataCatalog
{
    public const int CompanyCount = 5;
    public const int ProjectCount = 15;
    public const int EmployeeCount = 30;
    public const int PartnerCount = 12;
    public const int EquipmentCount = 15;

    public static DateOnly AnchorDate(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
}
