namespace EngineeringManager.Domain.Security;

public static class SystemRoles
{
    public const string SystemAdministrator = "SystemAdministrator";
    public const string ApplicationAdministrator = "ApplicationAdministrator";
    public const string Finance = "Finance";
    public const string ProjectManager = "ProjectManager";
    public const string SiteStaff = "SiteStaff";
    public const string QueryOnly = "QueryOnly";
    public const string EquipmentManager = "EquipmentManager";

    public static IReadOnlyList<string> All { get; } =
    [
        SystemAdministrator,
        ApplicationAdministrator,
        Finance,
        ProjectManager,
        SiteStaff,
        QueryOnly,
        EquipmentManager
    ];
}
