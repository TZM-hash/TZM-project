using EngineeringManager.Web.Workbenches;

namespace EngineeringManager.Web.Pages.Personnel;

public sealed record PersonnelWorkbookExportViewModel(
    string FormId,
    string ScopeLabel,
    IReadOnlyDictionary<string, string?> PreservedQueryParameters,
    IReadOnlyList<DataWorkbenchColumn> TableColumns,
    IReadOnlyList<DataWorkbenchColumn> ExportColumns,
    IReadOnlyList<string> SelectedExportColumns,
    string ExportColumnMode)
{
    public bool UsesTableExportColumns => string.Equals(ExportColumnMode, "table", StringComparison.Ordinal);

    public bool UsesContentExportColumns => !UsesTableExportColumns;
}
