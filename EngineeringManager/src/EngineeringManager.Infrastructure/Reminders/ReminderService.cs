using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Payroll;
using EngineeringManager.Application.Reminders;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Offline;
using EngineeringManager.Domain.Reminders;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Reminders;

public sealed class ReminderService(
    ApplicationDbContext db,
    IFinanceLedgerService financeService,
    IPayrollService payrollService) : IReminderService
{
    public async Task RefreshAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var existing = await db.ReminderItems.ToDictionaryAsync(item => item.DeduplicationKey, StringComparer.Ordinal, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var milestones = await db.ProjectMilestones.AsNoTracking().Include(item => item.Project)
            .Where(item => !item.IsCompleted && item.PlannedDate.HasValue && item.PlannedDate <= today.AddDays(7))
            .ToListAsync(cancellationToken);
        foreach (var milestone in milestones)
        {
            Upsert(existing, $"milestone:{milestone.Id}", ReminderType.ProjectMilestone, milestone.PlannedDate < today ? ReminderSeverity.Critical : ReminderSeverity.Warning,
                $"项目节点：{milestone.Name}", $"{milestone.Project.ProjectNumber} · {milestone.Project.Name}，计划日期 {milestone.PlannedDate:yyyy-MM-dd}", "ProjectMilestone", milestone.Id.ToString(), milestone.PlannedDate, null, now);
        }

        var projects = await db.Projects.AsNoTracking().Where(item => item.IsActive).Select(item => new { item.Id, item.ProjectNumber, item.Name }).ToListAsync(cancellationToken);
        foreach (var project in projects)
        {
            var summary = await financeService.GetProjectSummaryAsync(project.Id, cancellationToken);
            if (summary.UncollectedAmount > 0m)
            {
                Upsert(existing, $"project-uncollected:{project.Id}", ReminderType.UncollectedReceivable, ReminderSeverity.Warning,
                    "项目存在未收款", $"{project.ProjectNumber} · {project.Name} 未收款 {summary.UncollectedAmount:N2}", "Project", project.Id.ToString(), null, summary.UncollectedAmount, now);
            }

            if (summary.UnpaidAmount > 0m)
            {
                Upsert(existing, $"project-unpaid:{project.Id}", ReminderType.UnpaidPayable, ReminderSeverity.Warning,
                    "项目存在未付款", $"{project.ProjectNumber} · {project.Name} 未付款 {summary.UnpaidAmount:N2}", "Project", project.Id.ToString(), null, summary.UnpaidAmount, now);
            }

            if (summary.UninvoicedAmount > 0m)
            {
                Upsert(existing, $"project-uninvoiced:{project.Id}", ReminderType.UninvoicedReceivable, ReminderSeverity.Warning,
                    "项目存在未开票", $"{project.ProjectNumber} · {project.Name} 未开票 {summary.UninvoicedAmount:N2}", "Project", project.Id.ToString(), null, summary.UninvoicedAmount, now);
            }
        }

        var payroll = await payrollService.GetOverviewAsync(cancellationToken);
        foreach (var batch in payroll.Batches.Where(item => item.Summary.UnpaidAmount > 0m))
        {
            Upsert(existing, $"payroll-unpaid:{batch.Batch.Id}", ReminderType.UnpaidPayroll, ReminderSeverity.Warning,
                "工资批次存在未发工资", $"{batch.Batch.BatchNumber} · {batch.Batch.Name} 未发 {batch.Summary.UnpaidAmount:N2}", "PayrollBatch", batch.Batch.Id.ToString(), batch.Batch.EndDate, batch.Summary.UnpaidAmount, now);
        }

        var expiringCertificates = await db.CompanyCertificates.AsNoTracking().Include(item => item.LegalEntity)
            .Where(item => !item.IsDeleted && item.LegalEntity.IsActive && item.ExpiresOn.HasValue && item.ExpiresOn <= today.AddDays(30))
            .ToListAsync(cancellationToken);
        foreach (var certificate in expiringCertificates)
        {
            Upsert(existing, $"company-certificate:{certificate.Id}", ReminderType.CompanyCertificateExpiring,
                certificate.ExpiresOn < today ? ReminderSeverity.Critical : ReminderSeverity.Warning,
                "公司证照即将到期", $"{certificate.LegalEntity.ShortName} · {certificate.CertificateType} · {certificate.ExpiresOn:yyyy-MM-dd}",
                nameof(CompanyCertificate), certificate.Id.ToString(), certificate.ExpiresOn, null, now);
        }

        var expiringLeases = await db.EquipmentLeaseAgreements.AsNoTracking().Include(item => item.Equipment)
            .Where(item => item.Equipment.IsActive && item.EndDate.HasValue && item.EndDate <= today.AddDays(30))
            .ToListAsync(cancellationToken);
        foreach (var lease in expiringLeases)
        {
            Upsert(existing, $"equipment-lease:{lease.Id}", ReminderType.EquipmentLeaseExpiring,
                lease.EndDate < today ? ReminderSeverity.Critical : ReminderSeverity.Warning,
                "设备租赁即将到期", $"{lease.Equipment.EquipmentNumber} · {lease.Equipment.Name} · {lease.EndDate:yyyy-MM-dd}",
                nameof(EquipmentLeaseAgreement), lease.Id.ToString(), lease.EndDate, null, now);
        }

        var maintenanceDue = await db.EquipmentMaintenanceRecords.AsNoTracking().Include(item => item.Equipment)
            .Where(item => item.Equipment.IsActive && item.NextDueDate.HasValue && item.NextDueDate <= today.AddDays(30))
            .ToListAsync(cancellationToken);
        foreach (var maintenance in maintenanceDue)
        {
            Upsert(existing, $"equipment-maintenance:{maintenance.Id}", ReminderType.EquipmentMaintenanceDue,
                maintenance.NextDueDate < today ? ReminderSeverity.Critical : ReminderSeverity.Warning,
                "设备维保即将到期", $"{maintenance.Equipment.EquipmentNumber} · {maintenance.Equipment.Name} · {maintenance.NextDueDate:yyyy-MM-dd}",
                nameof(EquipmentMaintenanceRecord), maintenance.Id.ToString(), maintenance.NextDueDate, null, now);
        }

        var failedImports = await db.ImportBatches.AsNoTracking().Where(item => item.Status == DataExchangeTaskStatus.Failed).ToListAsync(cancellationToken);
        foreach (var batch in failedImports)
        {
            Upsert(existing, $"import-failed:{batch.Id}", ReminderType.ImportFailed, ReminderSeverity.Critical,
                "数据导入失败", batch.OriginalFileName, "ImportBatch", batch.Id.ToString(), null, null, now);
        }

        var failedBackups = await db.BackupTasks.AsNoTracking().Where(item => item.Status == DataExchangeTaskStatus.Failed).ToListAsync(cancellationToken);
        foreach (var task in failedBackups)
        {
            Upsert(existing, $"backup-failed:{task.Id}", ReminderType.BackupFailed, ReminderSeverity.Critical,
                "备份任务失败", task.ErrorMessage ?? "备份任务执行失败。", "BackupTask", task.Id.ToString(), null, null, now);
        }

        var failedSyncs = await db.OfflineDraftSyncs.AsNoTracking()
            .Where(item => item.Status == OfflineSyncStatus.Failed || item.Status == OfflineSyncStatus.Conflict)
            .ToListAsync(cancellationToken);
        var activeOfflineKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sync in failedSyncs)
        {
            var key = $"offline-sync:{sync.Id}";
            activeOfflineKeys.Add(key);
            var conflict = sync.Status == OfflineSyncStatus.Conflict;
            Upsert(existing, key, ReminderType.OfflineSyncFailed, conflict ? ReminderSeverity.Warning : ReminderSeverity.Critical,
                conflict ? "离线草稿存在版本冲突" : "离线草稿同步失败",
                sync.LastError ?? (conflict ? "服务器草稿已变化，请比较后处理。" : "离线草稿同步失败。"),
                "OfflineDraftSync", sync.Id.ToString(), null, null, now);
        }

        foreach (var reminder in existing.Values.Where(item => item.Type == ReminderType.OfflineSyncFailed && !activeOfflineKeys.Contains(item.DeduplicationKey)))
        {
            reminder.Status = ReminderStatus.Resolved;
            reminder.ResolvedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReminderDto>> ListAsync(bool includeResolved, CancellationToken cancellationToken)
    {
        var query = db.ReminderItems.AsNoTracking();
        if (!includeResolved) query = query.Where(item => item.Status != ReminderStatus.Resolved);
        var items = await query.ToListAsync(cancellationToken);
        return items.OrderByDescending(item => item.Severity).ThenByDescending(item => item.LastOccurredAt).Select(ToDto).ToArray();
    }

    public async Task MarkReadAsync(Guid reminderId, CancellationToken cancellationToken)
    {
        var reminder = await FindAsync(reminderId, cancellationToken);
        if (reminder.Status == ReminderStatus.Unread)
        {
            reminder.Status = ReminderStatus.Read;
            reminder.ReadAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ResolveAsync(Guid reminderId, CancellationToken cancellationToken)
    {
        var reminder = await FindAsync(reminderId, cancellationToken);
        reminder.Status = ReminderStatus.Resolved;
        reminder.ResolvedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private void Upsert(
        Dictionary<string, ReminderItem> existing,
        string key,
        ReminderType type,
        ReminderSeverity severity,
        string title,
        string message,
        string? sourceType,
        string? sourceId,
        DateOnly? dueDate,
        decimal? amount,
        DateTimeOffset now)
    {
        if (!existing.TryGetValue(key, out var reminder))
        {
            reminder = new ReminderItem { DeduplicationKey = key, FirstOccurredAt = now };
            existing.Add(key, reminder);
            db.ReminderItems.Add(reminder);
        }

        reminder.Type = type;
        reminder.Severity = severity;
        reminder.Title = title;
        reminder.Message = message;
        reminder.SourceType = sourceType;
        reminder.SourceId = sourceId;
        reminder.DueDate = dueDate;
        reminder.Amount = amount;
        reminder.LastOccurredAt = now;
        if (reminder.Status == ReminderStatus.Resolved)
        {
            reminder.Status = ReminderStatus.Unread;
            reminder.ResolvedAt = null;
            reminder.ReadAt = null;
        }
    }

    private async Task<ReminderItem> FindAsync(Guid reminderId, CancellationToken cancellationToken) =>
        await db.ReminderItems.SingleOrDefaultAsync(item => item.Id == reminderId, cancellationToken) ?? throw new InvalidOperationException("提醒不存在。");

    private static ReminderDto ToDto(ReminderItem item) =>
        new(item.Id, item.DeduplicationKey, item.Type, item.Severity, item.Status, item.Title, item.Message, item.SourceType, item.SourceId, item.DueDate, item.Amount, item.LastOccurredAt);
}
