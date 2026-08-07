using EngineeringManager.Domain.Employees;

namespace EngineeringManager.Application.DataExchange;

public sealed record PersonnelWorkbookColumnDefinition(
    string Key,
    string Label,
    bool IsNumeric = false,
    bool IsPercentage = false);

public sealed record PersonnelWorkbookExportRequest(
    IReadOnlyList<PersonnelWorkbookRow> Rows,
    IReadOnlyCollection<string>? Columns = null);

public sealed record PersonnelWorkbookRow(
    Guid Id,
    string PersonNumber,
    string Name,
    string? Phone,
    string PersonnelType,
    string? PositionTitle,
    string? OrganizationName,
    string? DepartmentName,
    string? ProjectName,
    string? CrewName,
    bool IsActive,
    EmployeeAnnualLedgerSummary? AnnualSummary,
    decimal? PenaltyAmount);
