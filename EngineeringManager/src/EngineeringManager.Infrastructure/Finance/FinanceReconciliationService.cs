using System.Text.Json;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Finance;

public sealed class FinanceReconciliationService(ApplicationDbContext db, ICentralLedgerQueryService ledger) : IFinanceReconciliationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<FinanceReconciliationDto>> ListAsync(CentralLedgerActor actor, CentralLedgerQuery query, CancellationToken token)
    {
        var records = db.FinanceReconciliations.AsNoTracking().Where(item => item.Scope == query.Scope);
        if (query.FinanceBusinessYearId.HasValue) records = records.Where(item => item.FinanceBusinessYearId == query.FinanceBusinessYearId);
        if (query.LegalEntityId.HasValue) records = records.Where(item => item.LegalEntityId == query.LegalEntityId);
        if (query.BusinessPartnerId.HasValue) records = records.Where(item => item.BusinessPartnerId == query.BusinessPartnerId);
        if (query.StartDate.HasValue) records = records.Where(item => item.AsOfDate >= query.StartDate);
        if (query.EndDate.HasValue) records = records.Where(item => item.AsOfDate <= query.EndDate);
        var items = await records.OrderByDescending(item => item.AsOfDate).ThenByDescending(item => item.Version).ToListAsync(token);
        foreach (var item in items) EnsureVisible(actor, item);
        return items.Select(ToDto).ToArray();
    }

    public async Task<Guid> CreateAsync(CentralLedgerActor actor, CreateFinanceReconciliationRequest request, CancellationToken token)
    {
        EnsureCanReconcile(actor);
        if (request.Query.Scope != request.Scope) throw new ArgumentException("对账范围与查询范围不一致。", nameof(request));
        if (request.StartDate.HasValue && request.StartDate > request.AsOfDate) throw new ArgumentException("对账开始日期不能晚于截止日期。", nameof(request));
        if (request.LegalEntityId.HasValue && !actor.LegalEntityIds.Contains(request.LegalEntityId.Value)) throw new UnauthorizedAccessException("没有该自有公司的对账权限。");
        var query = request.Query with
        {
            Scope = request.Scope,
            FinanceBusinessYearId = request.FinanceBusinessYearId ?? request.Query.FinanceBusinessYearId,
            StartDate = request.StartDate ?? request.Query.StartDate,
            EndDate = request.AsOfDate,
            LegalEntityId = request.LegalEntityId ?? request.Query.LegalEntityId,
            BusinessPartnerId = request.BusinessPartnerId ?? request.Query.BusinessPartnerId,
            Page = 1,
            PageSize = 200
        };
        var snapshot = await LoadAllAsync(actor, query, token);
        var version = await db.FinanceReconciliations
            .Where(item => item.Scope == request.Scope && item.ReconciliationScope == request.ReconciliationScope
                && item.FinanceBusinessYearId == request.FinanceBusinessYearId && item.LegalEntityId == request.LegalEntityId
                && item.BusinessPartnerId == request.BusinessPartnerId && item.AsOfDate == request.AsOfDate)
            .Select(item => (int?)item.Version)
            .MaxAsync(token) ?? 0;
        var reconciliation = new FinanceReconciliation
        {
            Scope = request.Scope,
            ReconciliationScope = request.ReconciliationScope,
            FinanceBusinessYearId = request.FinanceBusinessYearId,
            LegalEntityId = request.LegalEntityId,
            BusinessPartnerId = request.BusinessPartnerId,
            StartDate = request.StartDate,
            AsOfDate = request.AsOfDate,
            Version = version + 1,
            QueryJson = JsonSerializer.Serialize(query, JsonOptions),
            MetricsJson = JsonSerializer.Serialize(snapshot.Totals, JsonOptions),
            CreatedByUserId = actor.UserId,
            CreatedByUserName = actor.UserName
        };
        foreach (var row in snapshot.Rows)
        {
            reconciliation.Lines.Add(new FinanceReconciliationLine
            {
                SettlementId = row.SettlementId,
                LegalEntityId = row.LegalEntityId,
                BusinessPartnerId = row.BusinessPartnerId,
                CounterLegalEntityId = row.CounterLegalEntityId,
                ProjectId = row.ProjectId,
                ContractId = row.ContractId,
                SnapshotJson = JsonSerializer.Serialize(row, JsonOptions),
                MetricsJson = JsonSerializer.Serialize(row.Metrics, JsonOptions)
            });
        }
        db.FinanceReconciliations.Add(reconciliation);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actor.UserId,
            UserName = actor.UserName,
            Action = "Create",
            EntityType = nameof(FinanceReconciliation),
            EntityId = reconciliation.Id.ToString("D"),
            AfterJson = JsonSerializer.Serialize(new { reconciliation.Scope, reconciliation.ReconciliationScope, reconciliation.AsOfDate, reconciliation.Version }, JsonOptions)
        });
        await db.SaveChangesAsync(token);
        return reconciliation.Id;
    }

    public async Task<FinanceReconciliationDetailsDto?> GetDetailsAsync(CentralLedgerActor actor, Guid id, CancellationToken token)
    {
        var reconciliation = await db.FinanceReconciliations.AsNoTracking().Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id, token);
        if (reconciliation is null) return null;
        EnsureVisible(actor, reconciliation);
        var query = JsonSerializer.Deserialize<CentralLedgerQuery>(reconciliation.QueryJson, JsonOptions)
            ?? throw new InvalidOperationException("对账查询快照无法解析。");
        var current = await LoadAllAsync(actor, query, token);
        var currentById = current.Rows.ToDictionary(item => item.SettlementId);
        var lines = new List<FinanceReconciliationDifferenceDto>(reconciliation.Lines.Count);
        foreach (var line in reconciliation.Lines.OrderBy(item => item.SettlementId))
        {
            var snapshotMetrics = DeserializeMetrics(line.MetricsJson);
            var hasCurrent = currentById.TryGetValue(line.SettlementId, out var currentRow);
            var exists = hasCurrent || await db.FinanceSettlements.AsNoTracking().AnyAsync(item => item.Id == line.SettlementId, token);
            var currentMetrics = hasCurrent ? currentRow!.Metrics : CentralLedgerMetrics.Zero;
            lines.Add(new FinanceReconciliationDifferenceDto(
                line.SettlementId,
                snapshotMetrics,
                currentMetrics,
                Subtract(currentMetrics, snapshotMetrics),
                !exists));
        }
        var snapshotTotal = DeserializeMetrics(reconciliation.MetricsJson);
        return new FinanceReconciliationDetailsDto(
            ToDto(reconciliation),
            current.Totals,
            Subtract(current.Totals, snapshotTotal),
            lines);
    }

    public async Task DeleteAsync(CentralLedgerActor actor, Guid id, Guid concurrencyStamp, string reason, CancellationToken token)
    {
        EnsureCanReconcile(actor);
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? throw new ArgumentException("删除原因不能为空。", nameof(reason)) : reason.Trim();
        var reconciliation = await db.FinanceReconciliations.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == id, token)
            ?? throw new KeyNotFoundException("对账快照不存在。");
        EnsureVisible(actor, reconciliation);
        if (reconciliation.ConcurrencyStamp != concurrencyStamp) throw new DbUpdateConcurrencyException("对账快照已被其他用户修改，请刷新后重试。");
        db.FinanceReconciliations.Remove(reconciliation);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actor.UserId,
            UserName = actor.UserName,
            Action = "Delete",
            EntityType = nameof(FinanceReconciliation),
            EntityId = reconciliation.Id.ToString("D"),
            Reason = normalizedReason,
            BeforeJson = JsonSerializer.Serialize(new { reconciliation.Scope, reconciliation.ReconciliationScope, reconciliation.AsOfDate, reconciliation.Version }, JsonOptions)
        });
        await db.SaveChangesAsync(token);
    }

    private async Task<LedgerSnapshot> LoadAllAsync(CentralLedgerActor actor, CentralLedgerQuery query, CancellationToken token)
    {
        var first = await ledger.SearchAsync(actor, query with { Page = 1, PageSize = 200 }, token);
        var rows = new List<CentralLedgerRowDto>(first.Rows);
        for (var page = 2; page <= first.TotalPages; page++)
        {
            var next = await ledger.SearchAsync(actor, query with { Page = page, PageSize = 200 }, token);
            rows.AddRange(next.Rows);
        }
        return new LedgerSnapshot(rows, first.Totals);
    }

    private static FinanceReconciliationDto ToDto(FinanceReconciliation item) => new(
        item.Id,
        item.Scope,
        item.ReconciliationScope,
        item.AsOfDate,
        item.Version,
        DeserializeMetrics(item.MetricsJson),
        item.CreatedAt,
        item.CreatedByUserName,
        item.ConcurrencyStamp);

    private static CentralLedgerMetrics DeserializeMetrics(string json) =>
        JsonSerializer.Deserialize<CentralLedgerMetrics>(json, JsonOptions) ?? CentralLedgerMetrics.Zero;

    private static CentralLedgerMetrics Subtract(CentralLedgerMetrics left, CentralLedgerMetrics right) => new(
        left.GrossSettlementAmount - right.GrossSettlementAmount,
        left.Deductions - right.Deductions,
        left.ActualAmount - right.ActualAmount,
        left.ShouldInvoiceAmount - right.ShouldInvoiceAmount,
        left.InvoicedAmount - right.InvoicedAmount,
        left.CashAmount - right.CashAmount,
        left.UncollectedOrUnpaid - right.UncollectedOrUnpaid,
        left.Uninvoiced - right.Uninvoiced,
        left.InvoicedAndCollectedOrPaid - right.InvoicedAndCollectedOrPaid,
        left.InvoicedAndUncollectedOrUnpaid - right.InvoicedAndUncollectedOrUnpaid,
        left.AdvanceInvoiceCash - right.AdvanceInvoiceCash,
        left.UninvoicedAndUncollectedOrUnpaid - right.UninvoicedAndUncollectedOrUnpaid,
        left.InvoicedWithoutCashRequirement - right.InvoicedWithoutCashRequirement,
        left.OverSettlementCash - right.OverSettlementCash,
        left.OverInvoiced - right.OverInvoiced);

    private static void EnsureCanReconcile(CentralLedgerActor actor)
    {
        if (!actor.CanReconcile) throw new UnauthorizedAccessException("没有中央账本对账管理权限。");
    }

    private static void EnsureVisible(CentralLedgerActor actor, FinanceReconciliation reconciliation)
    {
        if (reconciliation.LegalEntityId.HasValue && !actor.LegalEntityIds.Contains(reconciliation.LegalEntityId.Value))
            throw new UnauthorizedAccessException("没有查看该对账快照的权限。");
    }

    private sealed record LedgerSnapshot(IReadOnlyList<CentralLedgerRowDto> Rows, CentralLedgerMetrics Totals);
}
