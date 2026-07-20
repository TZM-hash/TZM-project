using EngineeringManager.Application.Dashboard;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Reminders;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext db;
    private readonly IFinanceLedgerService financeService;

    public DashboardService(ApplicationDbContext db)
        : this(db, new FinanceLedgerService(db)) { }

    public DashboardService(ApplicationDbContext db, IFinanceLedgerService financeService)
    {
        this.db = db;
        this.financeService = financeService;
    }

    public async Task<DashboardDto> GetAsync(DashboardActor actor, CancellationToken cancellationToken)
    {
        var projectQuery = db.Projects.AsNoTracking()
            .Include(item => item.Contracts).ThenInclude(contract => contract.LineItems)
            .Where(item => item.IsActive);
        if (!actor.CanViewAllProjects)
        {
            projectQuery = projectQuery.Where(item => item.ResponsibleUserId == actor.UserId || item.Assignments.Any(assignment => assignment.UserId == actor.UserId));
        }

        var projects = await projectQuery.ToListAsync(cancellationToken);
        var projectIds = projects.Select(item => item.Id).ToArray();
        var currentAmount = projects.Sum(project => ProjectAmountCalculator.Calculate(project.Stage,
            project.Contracts.Where(contract => contract.IsActive).SelectMany(contract => contract.LineItems).Select(line =>
                new LineItemAmountInput(line.Quantity, line.UnitPrice, line.RequiresInvoice))).CurrentAmount);
        var stageDistribution = projects.GroupBy(item => item.Stage).OrderBy(group => group.Key).Select(group => new DashboardStageDto(
            group.Key,
            StageLabel(group.Key),
            group.Count(),
            projects.Count == 0 ? 0m : decimal.Round((decimal)group.Count() / projects.Count * 100m, 1))).ToArray();

        var projectSummaries = actor.CanViewFinance
            ? await financeService.ListProjectSummariesAsync(projectIds, cancellationToken)
            : [];
        var comparisons = actor.CanViewFinance
            ? LoadFinanceComparisons(projectSummaries.Select(item => item.Summary))
            : EmptyComparisons();
        var monthlyTrend = actor.CanViewFinance ? await LoadMonthlyTrendAsync(projectIds, cancellationToken) : EmptyMonthlyTrend();
        var payrollSummary = actor.CanViewPayroll ? await LoadPayrollSummaryAsync(projectIds, actor.CanViewAllProjects, cancellationToken) : new DashboardPayrollSummaryDto(0m, 0m, 0m);
        var equipmentSummary = await LoadEquipmentSummaryAsync(projectIds, actor.CanViewAllProjects, cancellationToken);
        var reminderQuery = db.ReminderItems.AsNoTracking().Where(item => item.Status != ReminderStatus.Resolved);
        if (!actor.CanViewAllProjects)
        {
            var projectKeys = projectIds.Select(item => item.ToString()).ToArray();
            reminderQuery = reminderQuery.Where(item => item.SourceType == "Project" && item.SourceId != null && projectKeys.Contains(item.SourceId));
        }
        var reminderItems = await reminderQuery.ToListAsync(cancellationToken);
        var reminders = reminderItems.OrderByDescending(item => item.Severity).ThenByDescending(item => item.LastOccurredAt).Take(8).ToArray();

        return new DashboardDto(
            projects.Count,
            currentAmount,
            payrollSummary.Unpaid,
            reminders.Length,
            actor.CanViewFinance,
            actor.CanViewPayroll,
            stageDistribution,
            comparisons,
            reminders.Select(item => new DashboardRiskDto(item.Id, item.Severity, item.Title, item.Message, item.SourceType, item.SourceId)).ToArray(),
            DateTimeOffset.UtcNow,
            monthlyTrend,
            equipmentSummary,
            payrollSummary,
            BuildCashWatchlist(projects, projectSummaries));
    }

    private static IReadOnlyList<DashboardMoneyComparisonDto> LoadFinanceComparisons(IEnumerable<FinanceProjectSummaryDto> summaries)
    {
        var materialized = summaries.ToArray();
        var receivable = materialized.Sum(item => item.ReceivableAmount);
        var collected = materialized.Sum(item => item.CollectedAmount);
        var payable = materialized.Sum(item => item.PayableAmount);
        var paid = materialized.Sum(item => item.PaidAmount);
        var unpaid = materialized.Sum(item => item.UnpaidAmount);
        var invoiced = materialized.Sum(item => item.OutputInvoiceAmount);
        return
        [
            Comparison("receivable", "收款进度", receivable, collected, Math.Max(receivable - collected, 0m)),
            Comparison("payable", "付款进度", payable, paid, unpaid),
            Comparison("invoice", "开票进度", receivable, invoiced, Math.Max(receivable - invoiced, 0m))
        ];
    }

    private async Task<DashboardPayrollSummaryDto> LoadPayrollSummaryAsync(Guid[] projectIds, bool allProjects, CancellationToken cancellationToken)
    {
        var batches = db.PayrollBatches.AsNoTracking().Where(item => item.Status != PayrollBatchStatus.Voided);
        if (!allProjects) batches = batches.Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value));
        var batchIds = await batches.Select(item => item.Id).ToArrayAsync(cancellationToken);
        if (batchIds.Length == 0) return new DashboardPayrollSummaryDto(0m, 0m, 0m);
        var items = await db.PayrollItems.AsNoTracking().Where(item => batchIds.Contains(item.PayrollBatchId)).Select(item => new { item.Nature, item.Amount }).ToListAsync(cancellationToken);
        var payments = await db.PayrollPayments.AsNoTracking().Where(item => batchIds.Contains(item.PayrollBatchId)).Select(item => item.Amount).ToListAsync(cancellationToken);
        var payable = Math.Max(items.Where(item => item.Nature == PayrollItemNature.Earning).Sum(item => item.Amount) - items.Where(item => item.Nature == PayrollItemNature.Deduction).Sum(item => item.Amount), 0m);
        var paid = payments.Sum();
        return new DashboardPayrollSummaryDto(payable, paid, Math.Max(payable - paid, 0m));
    }

    private static DashboardProjectCashDto[] BuildCashWatchlist(
        IReadOnlyCollection<Project> projects,
        IReadOnlyCollection<ProjectFinanceListItemDto> summaries)
    {
        var stages = projects.ToDictionary(item => item.Id, item => item.Stage);
        return summaries
            .Select(item =>
            {
                var stage = stages[item.ProjectId];
                return new DashboardProjectCashDto(item.ProjectId, item.ProjectNumber, item.ProjectName, stage,
                    StageLabel(stage), item.Summary.CollectedAmount, item.Summary.PaidAmount,
                    item.Summary.UncollectedAmount, item.Summary.UnpaidAmount,
                    item.Summary.CollectedAmount - item.Summary.PaidAmount);
            })
            .OrderBy(item => item.CashGap)
            .ThenByDescending(item => item.UncollectedAmount)
            .Take(8)
            .ToArray();
    }

    private async Task<IReadOnlyList<DashboardMonthlyPointDto>> LoadMonthlyTrendAsync(Guid[] projectIds, CancellationToken cancellationToken)
    {
        var firstMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-11);
        var months = Enumerable.Range(0, 12).Select(index => firstMonth.AddMonths(index)).ToArray();
        if (projectIds.Length == 0) return months.Select(month => MonthPoint(month)).ToArray();

        var cash = await db.FinanceCashAllocations.AsNoTracking()
            .Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value) &&
                item.CashEntry.BusinessDate >= firstMonth && item.CashEntry.Status == LedgerRecordStatus.Active)
            .Select(item => new { Date = item.CashEntry.BusinessDate, item.CashEntry.Direction, item.CashEntry.IsReversal, item.Amount })
            .ToListAsync(cancellationToken);
        var invoices = await db.FinanceInvoiceAllocations.AsNoTracking()
            .Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value) &&
                item.Invoice.InvoiceDate >= firstMonth && item.Invoice.Direction == LedgerDirection.Receivable && item.Invoice.Status == LedgerRecordStatus.Active)
            .Select(item => new { Date = item.Invoice.InvoiceDate, item.Amount })
            .ToListAsync(cancellationToken);

        return months.Select(month =>
        {
            var end = month.AddMonths(1);
            var collected = cash.Where(item => item.Direction == LedgerDirection.Receivable && item.Date >= month && item.Date < end)
                .Sum(item => item.IsReversal ? -item.Amount : item.Amount);
            var paid = cash.Where(item => item.Direction == LedgerDirection.Payable && item.Date >= month && item.Date < end)
                .Sum(item => item.IsReversal ? -item.Amount : item.Amount);
            var invoiced = invoices.Where(item => item.Date >= month && item.Date < end).Sum(item => item.Amount);
            return MonthPoint(month, collected, paid, invoiced);
        }).ToArray();
    }

    private async Task<DashboardEquipmentSummaryDto> LoadEquipmentSummaryAsync(Guid[] projectIds, bool allProjects, CancellationToken cancellationToken)
    {
        var equipment = db.Equipment.AsNoTracking().Where(item => item.IsActive);
        var usages = db.EquipmentProjectUsages.AsNoTracking();
        if (!allProjects)
        {
            equipment = equipment.Where(item => item.ProjectUsages.Any(usage => projectIds.Contains(usage.ProjectId)));
            usages = usages.Where(item => projectIds.Contains(item.ProjectId));
        }
        var total = await equipment.CountAsync(cancellationToken);
        var inUse = await equipment.CountAsync(item => item.Status == EquipmentStatus.InUse, cancellationToken);
        var rented = await equipment.CountAsync(item => item.OwnershipType == EquipmentOwnershipType.Rented, cancellationToken);
        var settlements = await usages.Where(item => item.Settlement != null)
            .Select(item => new { item.Equipment.OwnershipType, item.Settlement!.TotalAmount })
            .ToListAsync(cancellationToken);
        return new DashboardEquipmentSummaryDto(total, inUse, rented, settlements.Where(item => item.OwnershipType == EquipmentOwnershipType.Rented).Sum(item => item.TotalAmount), settlements.Sum(item => item.TotalAmount));
    }

    private static DashboardMonthlyPointDto[] EmptyMonthlyTrend()
    {
        var firstMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-11);
        return Enumerable.Range(0, 12).Select(index => MonthPoint(firstMonth.AddMonths(index))).ToArray();
    }

    private static DashboardMonthlyPointDto MonthPoint(DateOnly month, decimal collected = 0m, decimal paid = 0m, decimal invoiced = 0m) =>
        new(month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), collected, paid, invoiced);

    private static DashboardMoneyComparisonDto Comparison(string key, string label, decimal total, decimal completed, decimal remaining) =>
        new(key, label, total, completed, remaining, total <= 0m ? 0m : decimal.Round(Math.Clamp(completed / total * 100m, 0m, 100m), 1));

    private static IReadOnlyList<DashboardMoneyComparisonDto> EmptyComparisons() =>
    [
        Comparison("receivable", "收款进度", 0m, 0m, 0m),
        Comparison("payable", "付款进度", 0m, 0m, 0m),
        Comparison("invoice", "开票进度", 0m, 0m, 0m)
    ];

    private static string StageLabel(ProjectStage stage) => stage switch
    {
        ProjectStage.AwaitingMobilization => "待进场",
        ProjectStage.UnderConstruction => "施工中",
        ProjectStage.Suspended => "停工中",
        ProjectStage.CompletedUnsettled => "已完工未结算",
        ProjectStage.SettledArchived => "已结算归档",
        _ => stage.ToString()
    };
}
