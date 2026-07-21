using System.Text.Json;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Finance;

public sealed class CentralLedgerQueryService(ApplicationDbContext db) : ICentralLedgerQueryService
{
    public async Task<CentralLedgerOverviewPageDto> SearchAsync(
        CentralLedgerActor actor,
        CentralLedgerQuery query,
        CancellationToken token)
    {
        ValidateQueryScope(actor, query);
        var startDate = query.StartDate;
        var endDate = query.EndDate;
        if (query.FinanceBusinessYearId.HasValue)
        {
            var year = await db.FinanceBusinessYears.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == query.FinanceBusinessYearId, token)
                ?? throw new KeyNotFoundException("财务业务年度不存在。");
            startDate ??= year.StartDate;
            endDate ??= year.EndDate;
        }
        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            throw new ArgumentException("开始日期不能晚于结束日期。", nameof(query));
        }

        var legalEntityIds = actor.LegalEntityIds.ToArray();
        var projectIds = actor.ProjectIds.ToArray();
        var records = db.FinanceSettlements.AsNoTracking()
            .Where(item => item.Scope == query.Scope && legalEntityIds.Contains(item.LegalEntityId))
            .Where(item => !item.ProjectId.HasValue || projectIds.Contains(item.ProjectId.Value));
        if (query.Scope == LedgerScope.Internal)
        {
            records = records.Where(item => item.CounterLegalEntityId.HasValue && legalEntityIds.Contains(item.CounterLegalEntityId.Value));
        }
        records = records.Where(item => item.Status == (query.RecordStatus ?? LedgerRecordStatus.Active));
        if (query.Direction.HasValue) records = records.Where(item => item.Direction == query.Direction);
        if (startDate.HasValue) records = records.Where(item => item.BusinessDate >= startDate);
        if (endDate.HasValue) records = records.Where(item => item.BusinessDate <= endDate);
        if (query.LegalEntityId.HasValue) records = records.Where(item => item.LegalEntityId == query.LegalEntityId);
        if (query.BusinessPartnerId.HasValue) records = records.Where(item => item.BusinessPartnerId == query.BusinessPartnerId);
        if (query.CounterLegalEntityId.HasValue) records = records.Where(item => item.CounterLegalEntityId == query.CounterLegalEntityId);
        if (query.ProjectId.HasValue) records = records.Where(item => item.ProjectId == query.ProjectId);
        if (query.ContractId.HasValue) records = records.Where(item => item.ContractId == query.ContractId);
        if (query.ContractLineItemId.HasValue) records = records.Where(item => item.ContractLineItemId == query.ContractLineItemId);
        if (query.SettlementState.HasValue) records = records.Where(item => item.SettlementState == query.SettlementState);

        foreach (var term in SplitSearchTerms(query.Search))
        {
            var pattern = $"%{term}%";
            records = records.Where(item =>
                EF.Functions.Like(item.LegalEntity.Name, pattern) ||
                (item.BusinessPartner != null && EF.Functions.Like(item.BusinessPartner.Name, pattern)) ||
                (item.CounterLegalEntity != null && EF.Functions.Like(item.CounterLegalEntity.Name, pattern)) ||
                (item.Project != null && (EF.Functions.Like(item.Project.Name, pattern) || EF.Functions.Like(item.Project.ProjectNumber, pattern))) ||
                (item.Contract != null && (EF.Functions.Like(item.Contract.Name, pattern) || EF.Functions.Like(item.Contract.ContractNumber, pattern))) ||
                (item.Notes != null && EF.Functions.Like(item.Notes, pattern)));
        }

        var settlements = await records
            .Include(item => item.LegalEntity)
            .Include(item => item.BusinessPartner)
            .Include(item => item.CounterLegalEntity)
            .Include(item => item.Project)
            .Include(item => item.Contract)
            .Include(item => item.Adjustments)
            .Include(item => item.Deductions)
            .Include(item => item.InvoiceAllocations).ThenInclude(item => item.Invoice)
            .Include(item => item.CashAllocations).ThenInclude(item => item.CashEntry)
            .ToListAsync(token);

        IEnumerable<CentralLedgerRowDto> rows = settlements.Select(ToRow);
        if (query.InvoiceAllocationStatus.HasValue)
        {
            rows = rows.Where(item => item.InvoiceAllocationStatus == query.InvoiceAllocationStatus);
        }
        if (query.CashAllocationStatus.HasValue)
        {
            rows = rows.Where(item => item.CashAllocationStatus == query.CashAllocationStatus);
        }
        rows = ApplyFlag(rows, query.HasAdvanceInvoiceCash, item => item.Metrics.AdvanceInvoiceCash > 0m);
        rows = ApplyFlag(rows, query.HasOverSettlementCash, item => item.Metrics.OverSettlementCash > 0m);
        rows = ApplyFlag(rows, query.HasOverInvoiced, item => item.Metrics.OverInvoiced > 0m);
        rows = Sort(rows, query.SortKey, query.SortDescending);

        var matching = rows.ToArray();
        var totals = matching.Aggregate(CentralLedgerMetrics.Zero, (current, item) => CentralLedgerCalculator.Add(current, item.Metrics));
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var totalPages = matching.Length == 0 ? 0 : (int)Math.Ceiling(matching.Length / (decimal)pageSize);
        var pageRows = matching.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var unallocatedCash = await SearchUnallocatedCashAsync(actor, query, startDate, endDate, token);
        return new CentralLedgerOverviewPageDto(
            pageRows,
            totals,
            page,
            pageSize,
            matching.Length,
            totalPages,
            matching.Select(item => item.SettlementId).ToArray(),
            unallocatedCash);
    }

    private async Task<IReadOnlyList<CentralLedgerUnallocatedCashDto>> SearchUnallocatedCashAsync(
        CentralLedgerActor actor,
        CentralLedgerQuery query,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken token)
    {
        var projectIds = actor.ProjectIds.ToArray();
        var cashQuery = db.FinanceCashEntries.AsNoTracking().AsSplitQuery()
            .Include(item => item.LegalEntity).Include(item => item.BusinessPartner).Include(item => item.Project).Include(item => item.Contract).Include(item => item.Account)
            .Include(item => item.Allocations)
            .Where(item => item.Scope == query.Scope && item.Status == (query.RecordStatus ?? LedgerRecordStatus.Active) && !item.IsReversal
                && actor.LegalEntityIds.Contains(item.LegalEntityId)
                && (item.ProjectId.HasValue ? projectIds.Contains(item.ProjectId.Value) : item.Allocations.Any(allocation => allocation.ProjectId.HasValue && projectIds.Contains(allocation.ProjectId.Value))));
        if (query.Direction.HasValue) cashQuery = cashQuery.Where(item => item.Direction == query.Direction);
        if (startDate.HasValue) cashQuery = cashQuery.Where(item => item.BusinessDate >= startDate);
        if (endDate.HasValue) cashQuery = cashQuery.Where(item => item.BusinessDate <= endDate);
        if (query.LegalEntityId.HasValue) cashQuery = cashQuery.Where(item => item.LegalEntityId == query.LegalEntityId);
        if (query.BusinessPartnerId.HasValue) cashQuery = cashQuery.Where(item => item.BusinessPartnerId == query.BusinessPartnerId);
        if (query.ProjectId.HasValue) cashQuery = cashQuery.Where(item => item.ProjectId == query.ProjectId || item.Allocations.Any(allocation => allocation.ProjectId == query.ProjectId));
        if (query.ContractId.HasValue) cashQuery = cashQuery.Where(item => item.ContractId == query.ContractId || item.Allocations.Any(allocation => allocation.ContractId == query.ContractId));
        var cashEntries = await cashQuery.OrderBy(item => item.BusinessDate).ThenBy(item => item.Id).ToListAsync(token);
        return cashEntries.Select(item => new CentralLedgerUnallocatedCashDto(
                item.Id, item.Direction, item.BusinessDate, item.LegalEntityId, item.LegalEntity.Name,
                item.BusinessPartnerId, item.BusinessPartner?.Name, item.ProjectId, item.Project?.Name,
                item.ContractId, item.Contract?.Name, item.AccountId, item.Account?.AccountName, item.Amount,
                item.Allocations.Sum(allocation => allocation.Amount),
                item.Amount - item.Allocations.Sum(allocation => allocation.Amount), item.PaymentMethod, item.ConcurrencyStamp))
            .Where(item => item.UnallocatedAmount > 0m)
            .ToArray();
    }

    public async Task<CentralLedgerDetailsDto?> GetAsync(
        CentralLedgerActor actor,
        FinanceRecordType type,
        Guid id,
        CancellationToken token)
    {
        if (type == FinanceRecordType.Settlement)
        {
            var settlement = await db.FinanceSettlements.AsNoTracking()
                .Include(item => item.LegalEntity)
                .Include(item => item.BusinessPartner)
                .Include(item => item.CounterLegalEntity)
                .Include(item => item.Project)
                .Include(item => item.Contract)
                .Include(item => item.Adjustments)
                .Include(item => item.Deductions)
                .Include(item => item.InvoiceAllocations).ThenInclude(item => item.Invoice)
                .Include(item => item.CashAllocations).ThenInclude(item => item.CashEntry)
                .SingleOrDefaultAsync(item => item.Id == id, token);
            if (settlement is null) return null;
            EnsureCanRead(actor, settlement.LegalEntityId, settlement.CounterLegalEntityId, settlement.ProjectId);
            var row = ToRow(settlement);
            var allocations = settlement.InvoiceAllocations.Select(item => new FinanceAllocationDto(
                    item.Id, item.SettlementId, item.ProjectId, item.ContractId, item.ContractLineItemId, item.Amount, item.AllocationOrder))
                .Concat(settlement.CashAllocations.Select(item => new FinanceAllocationDto(
                    item.Id, item.SettlementId, item.ProjectId, item.ContractId, item.ContractLineItemId, item.Amount, item.AllocationOrder)))
                .ToArray();
            return new CentralLedgerDetailsDto(
                type,
                id,
                settlement.Scope,
                settlement.Direction,
                JsonSerializer.Serialize(new
                {
                    settlement.Id,
                    settlement.BusinessDate,
                    settlement.OriginalAmount,
                    settlement.OriginalInvoiceAmount,
                    settlement.Notes
                }),
                row.Metrics,
                allocations,
                settlement.ConcurrencyStamp);
        }

        return await GetHeaderDetailsAsync(actor, type, id, token);
    }

    public async Task<CentralLedgerOptionsDto> GetOptionsAsync(
        CentralLedgerActor actor,
        LedgerScope scope,
        CancellationToken token)
    {
        var legalIds = actor.LegalEntityIds.ToArray();
        var projectIds = actor.ProjectIds.ToArray();
        var legalEntities = await db.LegalEntities.AsNoTracking()
            .Where(item => legalIds.Contains(item.Id) && item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new CentralLedgerOptionDto(item.Id, item.Name, null, "legal-entity"))
            .ToListAsync(token);
        var projects = await db.Projects.AsNoTracking()
            .Where(item => projectIds.Contains(item.Id) && item.IsActive)
            .OrderBy(item => item.ProjectNumber)
            .Select(item => new CentralLedgerOptionDto(item.Id, item.ProjectNumber + " " + item.Name, null, "project"))
            .ToListAsync(token);
        var contracts = await db.Contracts.AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId) && item.IsActive)
            .OrderBy(item => item.ContractNumber)
            .Select(item => new CentralLedgerOptionDto(item.Id, item.ContractNumber + " " + item.Name, item.ProjectId, "contract"))
            .ToListAsync(token);
        var lineItems = await db.ContractLineItems.AsNoTracking()
            .Where(item => projectIds.Contains(item.Contract.ProjectId))
            .OrderBy(item => item.Code)
            .Select(item => new CentralLedgerOptionDto(item.Id, item.Code + " " + item.Name, item.ContractId, "line-item"))
            .ToListAsync(token);
        var partners = scope == LedgerScope.External
            ? await db.BusinessPartners.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name)
                .Select(item => new CentralLedgerOptionDto(item.Id, item.Name, null, "partner")).ToListAsync(token)
            : [];
        var crewRole = Domain.Partners.BusinessPartnerRoleType.ConstructionCrew;
        var crews = scope == LedgerScope.External
            ? await db.BusinessPartners.AsNoTracking()
                .Where(item => item.IsActive && item.Roles.Any(role => role.RoleType == crewRole))
                .OrderBy(item => item.Name)
                .Select(item => new CentralLedgerOptionDto(item.Id, item.Name, null, "crew"))
                .ToListAsync(token)
            : [];
        var accounts = await db.FinancialAccounts.AsNoTracking()
            .Where(item => legalIds.Contains(item.LegalEntityId) && item.IsActive)
            .OrderBy(item => item.AccountName)
            .Select(item => new CentralLedgerOptionDto(item.Id, item.AccountName, item.LegalEntityId, "account"))
            .ToListAsync(token);
        var years = await db.FinanceBusinessYears.AsNoTracking()
            .OrderByDescending(item => item.StartDate)
            .Select(item => new CentralLedgerOptionDto(item.Id, item.Name, null, "finance-year"))
            .ToListAsync(token);
        return new CentralLedgerOptionsDto(
            legalEntities,
            scope == LedgerScope.Internal ? legalEntities : [],
            projects,
            contracts,
            lineItems,
            partners,
            crews,
            accounts,
            years);
    }

    public async Task<CentralLedgerMetrics> GetProjectMetricsAsync(
        CentralLedgerActor actor,
        Guid projectId,
        CancellationToken token)
    {
        if (!actor.ProjectIds.Contains(projectId)) throw new UnauthorizedAccessException("无权查看所选项目的中央账本。");
        var result = await SearchAsync(actor, new CentralLedgerQuery(LedgerScope.External, ProjectId: projectId), token);
        return result.Totals;
    }

    public async Task<CentralLedgerMetrics> GetPartnerMetricsAsync(
        CentralLedgerActor actor,
        Guid businessPartnerId,
        CancellationToken token)
    {
        var result = await SearchAsync(actor, new CentralLedgerQuery(LedgerScope.External, BusinessPartnerId: businessPartnerId), token);
        return result.Totals;
    }

    private async Task<CentralLedgerDetailsDto?> GetHeaderDetailsAsync(
        CentralLedgerActor actor,
        FinanceRecordType type,
        Guid id,
        CancellationToken token)
    {
        if (type == FinanceRecordType.Invoice)
        {
            var invoice = await db.FinanceInvoices.AsNoTracking().Include(item => item.Allocations)
                .SingleOrDefaultAsync(item => item.Id == id, token);
            if (invoice is null) return null;
            EnsureCanRead(actor, invoice.LegalEntityId, invoice.CounterLegalEntityId, null);
            return new CentralLedgerDetailsDto(
                type, id, invoice.Scope, invoice.Direction, JsonSerializer.Serialize(invoice), CentralLedgerMetrics.Zero,
                invoice.Allocations.Select(item => new FinanceAllocationDto(item.Id, item.SettlementId, item.ProjectId, item.ContractId, item.ContractLineItemId, item.Amount, item.AllocationOrder)).ToArray(),
                invoice.ConcurrencyStamp);
        }
        if (type == FinanceRecordType.Cash)
        {
            var cash = await db.FinanceCashEntries.AsNoTracking().Include(item => item.Allocations)
                .SingleOrDefaultAsync(item => item.Id == id, token);
            if (cash is null) return null;
            EnsureCanRead(actor, cash.LegalEntityId, cash.CounterLegalEntityId, null);
            return new CentralLedgerDetailsDto(
                type, id, cash.Scope, cash.Direction, JsonSerializer.Serialize(cash), CentralLedgerMetrics.Zero,
                cash.Allocations.Select(item => new FinanceAllocationDto(item.Id, item.SettlementId, item.ProjectId, item.ContractId, item.ContractLineItemId, item.Amount, item.AllocationOrder)).ToArray(),
                cash.ConcurrencyStamp);
        }
        if (type == FinanceRecordType.Deduction)
        {
            var deduction = await db.FinanceDeductions.AsNoTracking().Include(item => item.Settlement)
                .SingleOrDefaultAsync(item => item.Id == id, token);
            if (deduction is null) return null;
            EnsureCanRead(actor, deduction.Settlement.LegalEntityId, deduction.Settlement.CounterLegalEntityId, deduction.Settlement.ProjectId);
            return new CentralLedgerDetailsDto(
                type, id, deduction.Settlement.Scope, deduction.Settlement.Direction, JsonSerializer.Serialize(deduction), CentralLedgerMetrics.Zero, [], deduction.ConcurrencyStamp);
        }
        if (type == FinanceRecordType.Adjustment)
        {
            var adjustment = await db.FinanceSettlementAdjustments.AsNoTracking().Include(item => item.Settlement)
                .SingleOrDefaultAsync(item => item.Id == id, token);
            if (adjustment is null) return null;
            EnsureCanRead(actor, adjustment.Settlement.LegalEntityId, adjustment.Settlement.CounterLegalEntityId, adjustment.Settlement.ProjectId);
            return new CentralLedgerDetailsDto(
                type, id, adjustment.Settlement.Scope, adjustment.Settlement.Direction, JsonSerializer.Serialize(adjustment), CentralLedgerMetrics.Zero, [], adjustment.ConcurrencyStamp);
        }
        throw new ArgumentOutOfRangeException(nameof(type), type, "不支持的财务记录类型。");
    }

    private static CentralLedgerRowDto ToRow(FinanceSettlement settlement)
    {
        var adjustments = settlement.Adjustments.Where(item => item.Status == LedgerRecordStatus.Active).ToArray();
        var deductions = settlement.Deductions.Where(item => item.Status == LedgerRecordStatus.Active).ToArray();
        var gross = settlement.OriginalAmount + adjustments.Sum(item => item.AmountDelta);
        var baseInvoice = settlement.OriginalInvoiceAmount + adjustments.Sum(item => item.InvoiceAmountDelta);
        var invoiced = settlement.InvoiceAllocations.Where(item => item.Invoice.Status == LedgerRecordStatus.Active).Sum(item => item.Amount);
        var cash = settlement.CashAllocations.Where(item => item.CashEntry.Status == LedgerRecordStatus.Active)
            .Sum(item => item.CashEntry.IsReversal ? -item.Amount : item.Amount);
        var metrics = CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(
            gross,
            deductions.Sum(item => item.Amount),
            deductions.Where(item => item.ReduceInvoiceAmount).Sum(item => item.Amount),
            baseInvoice,
            invoiced,
            cash));
        return new CentralLedgerRowDto(
            settlement.Id,
            settlement.Scope,
            settlement.Direction,
            settlement.SettlementState,
            settlement.BusinessDate,
            settlement.LegalEntityId,
            settlement.LegalEntity.Name,
            settlement.BusinessPartnerId,
            settlement.BusinessPartner?.Name,
            settlement.CounterLegalEntityId,
            settlement.CounterLegalEntity?.Name,
            settlement.ProjectId,
            settlement.Project?.Name,
            settlement.ContractId,
            settlement.Contract?.Name,
            metrics,
            AllocationStatus(metrics.InvoicedAmount, metrics.ShouldInvoiceAmount),
            AllocationStatus(metrics.CashAmount, metrics.ActualAmount),
            settlement.ConcurrencyStamp);
    }

    private static LedgerAllocationStatus AllocationStatus(decimal allocated, decimal target)
    {
        if (target <= 0m) return LedgerAllocationStatus.FullyAllocated;
        if (allocated <= 0m) return LedgerAllocationStatus.Unallocated;
        return allocated < target ? LedgerAllocationStatus.PartiallyAllocated : LedgerAllocationStatus.FullyAllocated;
    }

    private static IEnumerable<CentralLedgerRowDto> ApplyFlag(
        IEnumerable<CentralLedgerRowDto> rows,
        bool? requested,
        Func<CentralLedgerRowDto, bool> predicate)
    {
        return requested.HasValue ? rows.Where(item => predicate(item) == requested.Value) : rows;
    }

    private static IEnumerable<CentralLedgerRowDto> Sort(
        IEnumerable<CentralLedgerRowDto> rows,
        string? sortKey,
        bool descending)
    {
        Func<CentralLedgerRowDto, object> selector = sortKey switch
        {
            "ActualAmount" => item => item.Metrics.ActualAmount,
            "ShouldInvoiceAmount" => item => item.Metrics.ShouldInvoiceAmount,
            "UncollectedOrUnpaid" => item => item.Metrics.UncollectedOrUnpaid,
            "Uninvoiced" => item => item.Metrics.Uninvoiced,
            "ProjectName" => item => item.ProjectName ?? string.Empty,
            "BusinessPartnerName" => item => item.BusinessPartnerName ?? item.CounterLegalEntityName ?? string.Empty,
            _ => item => item.BusinessDate
        };
        return descending
            ? rows.OrderByDescending(selector).ThenByDescending(item => item.SettlementId)
            : rows.OrderBy(selector).ThenBy(item => item.SettlementId);
    }

    private static string[] SplitSearchTerms(string? search)
    {
        return string.IsNullOrWhiteSpace(search)
            ? []
            : search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void ValidateQueryScope(CentralLedgerActor actor, CentralLedgerQuery query)
    {
        if (query.LegalEntityId.HasValue && !actor.LegalEntityIds.Contains(query.LegalEntityId.Value))
        {
            throw new UnauthorizedAccessException("无权查看所选自有公司的中央账本。");
        }
        if (query.CounterLegalEntityId.HasValue && !actor.LegalEntityIds.Contains(query.CounterLegalEntityId.Value))
        {
            throw new UnauthorizedAccessException("无权查看所选内部往来公司的中央账本。");
        }
        if (query.ProjectId.HasValue && !actor.ProjectIds.Contains(query.ProjectId.Value))
        {
            throw new UnauthorizedAccessException("无权查看所选项目的中央账本。");
        }
    }

    private static void EnsureCanRead(CentralLedgerActor actor, Guid legalEntityId, Guid? counterLegalEntityId, Guid? projectId)
    {
        if (!actor.LegalEntityIds.Contains(legalEntityId) ||
            (counterLegalEntityId.HasValue && !actor.LegalEntityIds.Contains(counterLegalEntityId.Value)) ||
            (projectId.HasValue && !actor.ProjectIds.Contains(projectId.Value)))
        {
            throw new UnauthorizedAccessException("无权查看该中央账本记录。");
        }
    }
}
