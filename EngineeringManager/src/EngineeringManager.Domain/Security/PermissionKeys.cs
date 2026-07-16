namespace EngineeringManager.Domain.Security;

public static class PermissionKeys
{
    public const string SystemSecurityManage = "system.security.manage";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string OrganizationManage = "organization.manage";
    public const string AuditRead = "audit.read";
    public const string DataImport = "data.import";
    public const string DataExport = "data.export";
    public const string BackupCreate = "backup.create";
    public const string BackupRestore = "backup.restore";
    public const string ProjectsRead = "projects.read";
    public const string ProjectsManage = "projects.manage";
    public const string ContractsManage = "contracts.manage";
    public const string PartnersRead = "partners.read";
    public const string PartnersManage = "partners.manage";
    public const string StageResultsRead = "stage-results.read";
    public const string StageResultsCreate = "stage-results.create";
    public const string StageResultsManage = "stage-results.manage";
    public const string FinanceRead = "finance.read";
    public const string FinanceManage = "finance.manage";
    public const string FinancialAccountsManage = "finance.accounts.manage";
    public const string EmployeesRead = "employees.read";
    public const string EmployeesManage = "employees.manage";
    public const string PayrollRead = "payroll.read";
    public const string PayrollManage = "payroll.manage";
    public const string EmployeeLedgerRead = "employee-ledger.read";
    public const string EmployeeLedgerManage = "employee-ledger.manage";
    public const string ExportTemplatesManage = "data-export.templates.manage";
    public const string RemindersRead = "reminders.read";
    public const string RemindersManage = "reminders.manage";

    private static readonly HashSet<string> KnownPermissions = new(StringComparer.Ordinal)
    {
        SystemSecurityManage,
        UsersManage,
        RolesManage,
        OrganizationManage,
        AuditRead,
        DataImport,
        DataExport,
        BackupCreate,
        BackupRestore,
        ProjectsRead,
        ProjectsManage,
        ContractsManage,
        PartnersRead,
        PartnersManage,
        StageResultsRead,
        StageResultsCreate,
        StageResultsManage,
        FinanceRead,
        FinanceManage,
        FinancialAccountsManage,
        EmployeesRead,
        EmployeesManage,
        PayrollRead,
        PayrollManage,
        EmployeeLedgerRead,
        EmployeeLedgerManage,
        ExportTemplatesManage,
        RemindersRead,
        RemindersManage
    };

    private static readonly Dictionary<string, IReadOnlySet<string>> RoleDefaults =
        new(StringComparer.Ordinal)
        {
            [SystemRoles.SystemAdministrator] = KnownPermissions,
            [SystemRoles.ApplicationAdministrator] = new HashSet<string>(StringComparer.Ordinal)
            {
                UsersManage,
                RolesManage,
                OrganizationManage,
                AuditRead,
                DataImport,
                DataExport,
                BackupCreate,
                ProjectsRead,
                ProjectsManage,
                ContractsManage,
                PartnersRead,
                PartnersManage,
                StageResultsRead,
                StageResultsCreate,
                StageResultsManage,
                FinanceRead,
                FinanceManage,
                FinancialAccountsManage,
                EmployeesRead,
                EmployeesManage,
                PayrollRead,
                PayrollManage,
                EmployeeLedgerRead,
                EmployeeLedgerManage,
                ExportTemplatesManage,
                RemindersRead,
                RemindersManage
            },
            [SystemRoles.Finance] = new HashSet<string>(StringComparer.Ordinal) { DataExport, ProjectsRead, PartnersRead, StageResultsRead, FinanceRead, FinanceManage, FinancialAccountsManage, EmployeesRead, PayrollRead, PayrollManage, EmployeeLedgerRead, EmployeeLedgerManage, RemindersRead },
            [SystemRoles.ProjectManager] = new HashSet<string>(StringComparer.Ordinal) { DataExport, ProjectsRead, ProjectsManage, ContractsManage, PartnersRead, PartnersManage, StageResultsRead, StageResultsCreate, StageResultsManage, EmployeesRead, RemindersRead },
            [SystemRoles.SiteStaff] = new HashSet<string>(StringComparer.Ordinal) { ProjectsRead, PartnersRead, StageResultsRead, StageResultsCreate },
            [SystemRoles.QueryOnly] = new HashSet<string>(StringComparer.Ordinal) { DataExport, ProjectsRead, PartnersRead, StageResultsRead, FinanceRead, EmployeesRead, PayrollRead, EmployeeLedgerRead, RemindersRead }
        };

    public static bool IsKnown(string permissionKey) => KnownPermissions.Contains(permissionKey);

    public static IReadOnlySet<string> DefaultsForRole(string roleName) =>
        RoleDefaults.TryGetValue(roleName, out var permissions)
            ? permissions
            : new HashSet<string>(StringComparer.Ordinal);
}
