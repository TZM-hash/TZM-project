using EngineeringManager.Application.Dashboard;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Reminders;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Dashboard;

public sealed class DashboardService(ApplicationDbContext db) : IDashboardService
{
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
        var currentAmount = projects.Sum(project => ProjectAmountCalculator.Calculate(
            project.Contracts.Where(contract => contract.IsActive).SelectMany(contract => contract.LineItems).Select(line =>
                new LineItemAmountInput(line.EstimatedQuantity, line.EstimatedUnitPrice, line.SettledQuantity, line.SettledUnitPrice))).CurrentAmount);
        var stageDistribution = projects.GroupBy(item => item.Stage).OrderBy(group => group.Key).Select(group => new DashboardStageDto(
            group.Key,
            StageLabel(group.Key),
            group.Count(),
            projects.Count == 0 ? 0m : decimal.Round((decimal)group.Count() / projects.Count * 100m, 1))).ToArray();

        var comparisons = actor.CanViewFinance
            ? await LoadFinanceAsync(projectIds, cancellationToken)
            : EmptyComparisons();
        var unpaidPayroll = actor.CanViewPayroll ? await LoadUnpaidPayrollAsync(projectIds, actor.CanViewAllProjects, cancellationToken) : 0m;
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
            unpaidPayroll,
            reminders.Length,
            actor.CanViewFinance,
            actor.CanViewPayroll,
            stageDistribution,
            comparisons,
            reminders.Select(item => new DashboardRiskDto(item.Id, item.Severity, item.Title, item.Message, item.SourceType, item.SourceId)).ToArray(),
            DateTimeOffset.UtcNow);
    }

    private async Task<IReadOnlyList<DashboardMoneyComparisonDto>> LoadFinanceAsync(Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0) return EmptyComparisons();
        var receivables = await db.ReceivableEntries.AsNoTracking().Where(item => projectIds.Contains(item.ProjectId) && !item.IsVoided).Select(item => item.Amount).ToListAsync(cancellationToken);
        var collections = await db.CollectionEntries.AsNoTracking().Where(item => projectIds.Contains(item.ProjectId)).Select(item => item.Amount).ToListAsync(cancellationToken);
        var refunds = await db.RefundOrReversalEntries.AsNoTracking().Where(item =>
            item.Collection != null && projectIds.Contains(item.Collection.ProjectId) || item.Receivable != null && projectIds.Contains(item.Receivable.ProjectId)).Select(item => item.Amount).ToListAsync(cancellationToken);
        var payables = await db.PayableEntries.AsNoTracking().Where(item => projectIds.Contains(item.ProjectId) && !item.IsVoided).Select(item => item.Amount).ToListAsync(cancellationToken);
        var payments = await db.PaymentEntries.AsNoTracking().Where(item => projectIds.Contains(item.ProjectId)).Select(item => item.Amount).ToListAsync(cancellationToken);
        var reversals = await db.PaymentReversalEntries.AsNoTracking().Where(item => projectIds.Contains(item.Payment.ProjectId)).Select(item => item.Amount).ToListAsync(cancellationToken);
        var deductions = await db.DeductionEntries.AsNoTracking().Where(item => projectIds.Contains(item.ProjectId)).Select(item => item.Amount).ToListAsync(cancellationToken);
        var invoices = await db.InvoiceEntries.AsNoTracking().Where(item => projectIds.Contains(item.ProjectId) && item.Direction == InvoiceDirection.Output && item.Status != InvoiceStatus.Voided).Select(item => item.GrossAmount).ToListAsync(cancellationToken);

        var receivable = receivables.Sum();
        var collected = collections.Sum() - refunds.Sum();
        var payable = payables.Sum();
        var paid = payments.Sum() - reversals.Sum();
        var unpaid = Math.Max(payable - paid - deductions.Sum(), 0m);
        var invoiced = invoices.Sum();
        return
        [
            Comparison("receivable", "收款进度", receivable, collected, Math.Max(receivable - collected, 0m)),
            Comparison("payable", "付款进度", payable, paid, unpaid),
            Comparison("invoice", "开票进度", receivable, invoiced, Math.Max(receivable - invoiced, 0m))
        ];
    }

    private async Task<decimal> LoadUnpaidPayrollAsync(Guid[] projectIds, bool allProjects, CancellationToken cancellationToken)
    {
        var batches = db.PayrollBatches.AsNoTracking().Where(item => item.Status != PayrollBatchStatus.Voided);
        if (!allProjects) batches = batches.Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value));
        var batchIds = await batches.Select(item => item.Id).ToArrayAsync(cancellationToken);
        if (batchIds.Length == 0) return 0m;
        var items = await db.PayrollItems.AsNoTracking().Where(item => batchIds.Contains(item.PayrollBatchId)).Select(item => new { item.Nature, item.Amount }).ToListAsync(cancellationToken);
        var payments = await db.PayrollPayments.AsNoTracking().Where(item => batchIds.Contains(item.PayrollBatchId)).Select(item => item.Amount).ToListAsync(cancellationToken);
        var payable = Math.Max(items.Where(item => item.Nature == PayrollItemNature.Earning).Sum(item => item.Amount) - items.Where(item => item.Nature == PayrollItemNature.Deduction).Sum(item => item.Amount), 0m);
        return Math.Max(payable - payments.Sum(), 0m);
    }

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
        ProjectStage.Preliminary => "前期",
        ProjectStage.AwaitingContract => "待签合同",
        ProjectStage.AwaitingMobilization => "待进场",
        ProjectStage.UnderConstruction => "施工中",
        ProjectStage.Suspended => "暂停施工",
        ProjectStage.CompletedAwaitingAcceptance => "完工待验收",
        ProjectStage.Settlement => "结算中",
        ProjectStage.Warranty => "质保期",
        ProjectStage.Closed => "已结束",
        _ => stage.ToString()
    };
}
