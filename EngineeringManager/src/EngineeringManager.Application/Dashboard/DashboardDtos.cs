using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Reminders;

namespace EngineeringManager.Application.Dashboard;

public sealed record DashboardActor(string UserId, bool CanViewAllProjects, bool CanViewFinance, bool CanViewPayroll);

public sealed record DashboardStageDto(ProjectStage Stage, string Label, int Count, decimal Percentage);

public sealed record DashboardMoneyComparisonDto(
    string Key,
    string Label,
    decimal TotalAmount,
    decimal CompletedAmount,
    decimal RemainingAmount,
    decimal CompletionPercentage);

public sealed record DashboardRiskDto(Guid Id, ReminderSeverity Severity, string Title, string Message, string? SourceType, string? SourceId);

public sealed record DashboardDto(
    int ActiveProjectCount,
    decimal CurrentProjectAmount,
    decimal UnpaidPayrollAmount,
    int OpenReminderCount,
    bool CanViewFinance,
    bool CanViewPayroll,
    IReadOnlyList<DashboardStageDto> StageDistribution,
    IReadOnlyList<DashboardMoneyComparisonDto> MoneyComparisons,
    IReadOnlyList<DashboardRiskDto> Risks,
    DateTimeOffset GeneratedAt);
