using EngineeringManager.Application.Projects;
using EngineeringManager.Application.Security;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Security;

namespace EngineeringManager.Application.DataExchange;

public enum ProjectWorkbookSheet
{
    ProjectMaster = 1,
    ProjectSummary = 2,
    Contracts = 3,
    QuantityLines = 4,
    Milestones = 5,
    Assignments = 6,
    Partners = 7,
    Construction = 8,
    StageResults = 9,
    Receivables = 10,
    Collections = 11,
    Payables = 12,
    Payments = 13,
    Invoices = 14,
    Deductions = 15,
    Attachments = 16
}

public static class ProjectWorkbookVersions
{
    public const string Workbook = "project-workbook/1";
    public const string Dataset = "1";
}

public sealed record ProjectWorkbookActor(
    string UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<PermissionOverrideDto>? PermissionOverrides = null)
{
    public bool IsAdministrator => Roles.Contains(SystemRoles.SystemAdministrator, StringComparer.Ordinal)
        || Roles.Contains(SystemRoles.ApplicationAdministrator, StringComparer.Ordinal);

    public bool IsBusinessAdministrator => IsAdministrator
        || Roles.Contains(SystemRoles.ProjectManager, StringComparer.Ordinal);

    public bool CanExport => IsAllowed(PermissionKeys.DataExport);

    public bool CanImport => IsAdministrator && IsAllowed(PermissionKeys.DataImport);

    public bool CanExportFullWorkbook => IsBusinessAdministrator;

    public bool CanExportAttachments => IsBusinessAdministrator;

    public bool CanExportSheet(ProjectWorkbookSheet sheet) => sheet switch
    {
        ProjectWorkbookSheet.ProjectMaster or ProjectWorkbookSheet.ProjectSummary or ProjectWorkbookSheet.Contracts
            or ProjectWorkbookSheet.QuantityLines or ProjectWorkbookSheet.Milestones or ProjectWorkbookSheet.Assignments
            => IsAllowed(PermissionKeys.ProjectsRead),
        ProjectWorkbookSheet.Partners => IsAllowed(PermissionKeys.PartnersRead),
        ProjectWorkbookSheet.Construction => IsAllowed(PermissionKeys.ProjectsRead)
            && (IsAllowed(PermissionKeys.EquipmentRead)
                || IsAllowed(PermissionKeys.ConstructionCrewsRead)
                || IsBusinessAdministrator),
        ProjectWorkbookSheet.StageResults => IsAllowed(PermissionKeys.StageResultsRead),
        ProjectWorkbookSheet.Receivables or ProjectWorkbookSheet.Collections or ProjectWorkbookSheet.Payables
            or ProjectWorkbookSheet.Payments or ProjectWorkbookSheet.Invoices or ProjectWorkbookSheet.Deductions
            => IsAllowed(PermissionKeys.FinanceRead),
        ProjectWorkbookSheet.Attachments => CanExportAttachments,
        _ => false
    };

    public bool CanManageSheet(ProjectWorkbookSheet sheet) => sheet switch
    {
        ProjectWorkbookSheet.ProjectMaster or ProjectWorkbookSheet.Milestones or ProjectWorkbookSheet.Assignments
            => IsAllowed(PermissionKeys.ProjectsManage),
        ProjectWorkbookSheet.Contracts or ProjectWorkbookSheet.QuantityLines
            => IsAllowed(PermissionKeys.ContractsManage),
        ProjectWorkbookSheet.Partners
            => IsAllowed(PermissionKeys.PartnersManage),
        ProjectWorkbookSheet.Construction
            => IsAllowed(PermissionKeys.ProjectsManage)
                || IsAllowed(PermissionKeys.ConstructionCrewsManage)
                || IsAllowed(PermissionKeys.EquipmentUsageManage),
        ProjectWorkbookSheet.StageResults
            => IsAllowed(PermissionKeys.StageResultsManage),
        ProjectWorkbookSheet.Receivables or ProjectWorkbookSheet.Collections or ProjectWorkbookSheet.Payables
            or ProjectWorkbookSheet.Payments or ProjectWorkbookSheet.Invoices or ProjectWorkbookSheet.Deductions
            => IsAllowed(PermissionKeys.FinanceManage),
        ProjectWorkbookSheet.Attachments => CanExportAttachments,
        ProjectWorkbookSheet.ProjectSummary => false,
        _ => false
    };

    public static ProjectWorkbookActor Administrator(string userId) =>
        new(userId, [SystemRoles.SystemAdministrator]);

    private bool IsAllowed(string permissionKey) =>
        PermissionEvaluator.IsAllowed(Roles, PermissionOverrides ?? [], permissionKey);
}

public sealed record ProjectWorkbookFieldDefinition(
    string Key,
    string Header,
    ExportFieldDataType DataType,
    bool IsRequired = false,
    bool CanImport = true,
    bool CanExport = true,
    bool IsHidden = false,
    bool IsCalculated = false,
    bool IsSensitive = false,
    IReadOnlyList<string>? Aliases = null);

public sealed record ProjectWorkbookSheetDefinition(
    ProjectWorkbookSheet Sheet,
    string WorksheetName,
    bool CanImport,
    bool RequiresArchive,
    IReadOnlyList<ProjectWorkbookSheet> DependsOn,
    IReadOnlyList<ProjectWorkbookFieldDefinition> Fields);

public sealed record ProjectWorkbookScope(
    ProjectListActor Actor,
    ProjectListQuery Query,
    bool SelectAllMatching = false,
    IReadOnlyCollection<Guid>? SelectedProjectIds = null);

public sealed record ProjectWorkbookExportRequest(
    ProjectWorkbookScope Scope,
    IReadOnlyCollection<ProjectWorkbookSheet> Sheets,
    DateOnly? CutoffDate = null,
    bool IncludeAttachments = false,
    ProjectWorkbookActor? Actor = null);

public sealed record ProjectWorkbookImportRequest(
    string UserId,
    string OriginalFileName,
    byte[] Content,
    ImportMode Mode = ImportMode.Mixed,
    bool IncludeAttachments = false,
    IReadOnlyDictionary<ProjectWorkbookSheet, IReadOnlyDictionary<string, string>>? Mappings = null,
    bool BlankMeansNoChange = false,
    ProjectWorkbookActor? Actor = null);

public sealed record ProjectWorkbookSheetPreviewDto(
    ProjectWorkbookSheet Sheet,
    string WorksheetName,
    int TotalRows,
    int NewRows,
    int UpdatedRows,
    int UnchangedRows,
    int SkippedRows,
    int ErrorRows,
    IReadOnlyList<ImportErrorDto> Errors);

public sealed record ProjectWorkbookImportPreviewDto(
    Guid BatchId,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    IReadOnlyList<ProjectWorkbookSheetPreviewDto> Sheets,
    IReadOnlyList<ImportErrorDto> Errors);
