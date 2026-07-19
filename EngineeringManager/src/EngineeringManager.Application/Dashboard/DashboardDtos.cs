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

public sealed record DashboardMonthlyPointDto(string Month, decimal Collected, decimal Paid, decimal Invoiced);

public sealed record DashboardEquipmentSummaryDto(int Total, int InUse, int Rented, decimal RentedCost, decimal SettledAmount);

public sealed record DashboardPayrollSummaryDto(decimal Payable, decimal Paid, decimal Unpaid);

public sealed record DashboardProjectCashDto(
    Guid ProjectId,
    string ProjectNumber,
    string ProjectName,
    ProjectStage Stage,
    string StageLabel,
    decimal CollectedAmount,
    decimal PaidAmount,
    decimal UncollectedAmount,
    decimal UnpaidAmount,
    decimal CashGap);

public sealed record DashboardDto
{
    public DashboardDto(
        int activeProjectCount,
        decimal currentProjectAmount,
        decimal unpaidPayrollAmount,
        int openReminderCount,
        bool canViewFinance,
        bool canViewPayroll,
        IReadOnlyList<DashboardStageDto> stageDistribution,
        IReadOnlyList<DashboardMoneyComparisonDto> moneyComparisons,
        IReadOnlyList<DashboardRiskDto> risks,
        DateTimeOffset generatedAt,
        IReadOnlyList<DashboardMonthlyPointDto>? monthlyTrend = null,
        DashboardEquipmentSummaryDto? equipmentSummary = null,
        DashboardPayrollSummaryDto? payrollSummary = null,
        IReadOnlyList<DashboardProjectCashDto>? cashWatchlist = null)
    {
        ActiveProjectCount = activeProjectCount;
        CurrentProjectAmount = currentProjectAmount;
        UnpaidPayrollAmount = unpaidPayrollAmount;
        OpenReminderCount = openReminderCount;
        CanViewFinance = canViewFinance;
        CanViewPayroll = canViewPayroll;
        StageDistribution = stageDistribution;
        MoneyComparisons = moneyComparisons;
        Risks = risks;
        GeneratedAt = generatedAt;
        MonthlyTrend = monthlyTrend ?? [];
        EquipmentSummary = equipmentSummary ?? new DashboardEquipmentSummaryDto(0, 0, 0, 0m, 0m);
        PayrollSummary = payrollSummary ?? new DashboardPayrollSummaryDto(0m, 0m, 0m);
        CashWatchlist = cashWatchlist ?? [];
    }

    public int ActiveProjectCount { get; }
    public decimal CurrentProjectAmount { get; }
    public decimal UnpaidPayrollAmount { get; }
    public int OpenReminderCount { get; }
    public bool CanViewFinance { get; }
    public bool CanViewPayroll { get; }
    public IReadOnlyList<DashboardStageDto> StageDistribution { get; }
    public IReadOnlyList<DashboardMoneyComparisonDto> MoneyComparisons { get; }
    public IReadOnlyList<DashboardRiskDto> Risks { get; }
    public DateTimeOffset GeneratedAt { get; }
    public IReadOnlyList<DashboardMonthlyPointDto> MonthlyTrend { get; }
    public DashboardEquipmentSummaryDto EquipmentSummary { get; }
    public DashboardPayrollSummaryDto PayrollSummary { get; }
    public IReadOnlyList<DashboardProjectCashDto> CashWatchlist { get; }
}
