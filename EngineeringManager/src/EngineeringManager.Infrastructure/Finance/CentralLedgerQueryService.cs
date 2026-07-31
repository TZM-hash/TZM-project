using System.Text.Json;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Employees;
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
        var payrollPayments = await SearchPayrollPaymentsAsync(actor, query, startDate, endDate, token);
        var invoices = await SearchInvoicesAsync(actor, query, startDate, endDate, token);
        var cashEntries = await SearchCashEntriesAsync(actor, query, startDate, endDate, token);
        var deductions = await SearchDeductionsAsync(actor, query, startDate, endDate, token);
        var auditEntries = await SearchAuditEntriesAsync(
            actor,
            query,
            matching.Select(item => item.SettlementId).ToHashSet(),
            invoices.Select(item => item.Id).Concat(cashEntries.Select(item => item.Id)).Concat(deductions.Select(item => item.Id)).ToHashSet(),
            token);
        var receivableTotals = matching.Where(item => item.Direction == LedgerDirection.Receivable)
            .Aggregate(CentralLedgerMetrics.Zero, (current, item) => CentralLedgerCalculator.Add(current, item.Metrics));
        var payableTotals = matching.Where(item => item.Direction == LedgerDirection.Payable)
            .Aggregate(CentralLedgerMetrics.Zero, (current, item) => CentralLedgerCalculator.Add(current, item.Metrics));
        return new CentralLedgerOverviewPageDto(
            pageRows,
            totals,
            page,
            pageSize,
            matching.Length,
            totalPages,
            matching.Select(item => item.SettlementId).ToArray(),
            unallocatedCash,
            payrollPayments,
            payrollPayments.Sum(item => item.ActualAmount),
            invoices,
            cashEntries,
            deductions,
            auditEntries,
            receivableTotals,
            payableTotals);
    }

    private async Task<IReadOnlyList<CentralLedgerPayrollPaymentDto>> SearchPayrollPaymentsAsync(
        CentralLedgerActor actor,
        CentralLedgerQuery query,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken token)
    {
        if (query.Scope != LedgerScope.External || query.Direction == LedgerDirection.Receivable || query.ContractLineItemId.HasValue)
        {
            return [];
        }

        var legalEntityIds = actor.LegalEntityIds.ToArray();
        var projectIds = actor.ProjectIds.ToArray();
        var payroll = db.PayrollBatches.AsNoTracking()
            .Where(item => item.IsUnifiedDisbursement
                && item.PaymentDate.HasValue
                && item.LegalEntityId.HasValue
                && item.AccountId.HasValue
                && item.AccountTransactionId.HasValue
                && (item.Status == PayrollBatchStatus.Confirmed
                    || item.Status == PayrollBatchStatus.Closed
                    || item.Status == PayrollBatchStatus.ModifiedPendingReview))
            .Where(item => item.LegalEntityId.HasValue && legalEntityIds.Contains(item.LegalEntityId.Value))
            .Where(item => !item.ProjectId.HasValue || projectIds.Contains(item.ProjectId.Value))
            .Where(item => db.AccountTransactions.Any(transaction =>
                item.AccountTransactionId.HasValue
                && transaction.Id == item.AccountTransactionId.Value
                && transaction.SourceType == AccountTransactionSourceType.PayrollPayment
                && transaction.SourceId == item.Id
                && transaction.Direction == AccountTransactionDirection.Outflow));

        if (startDate.HasValue) payroll = payroll.Where(item => item.PaymentDate >= startDate);
        if (endDate.HasValue) payroll = payroll.Where(item => item.PaymentDate <= endDate);
        if (query.LegalEntityId.HasValue) payroll = payroll.Where(item => item.LegalEntityId == query.LegalEntityId);
        if (query.ProjectId.HasValue) payroll = payroll.Where(item => item.ProjectId == query.ProjectId);
        if (query.BusinessPartnerId.HasValue)
        {
            payroll = payroll.Where(item => item.CrewAllocations.Any(allocation => allocation.CrewBusinessPartnerId == query.BusinessPartnerId)
                || item.Payments.Any(payment => payment.CrewBusinessPartnerId == query.BusinessPartnerId || payment.LaborBusinessPartnerId == query.BusinessPartnerId));
        }
        if (query.ContractId.HasValue)
        {
            payroll = payroll.Where(item => item.CrewAllocations.Any(allocation => allocation.ContractId == query.ContractId));
        }

        foreach (var term in SplitSearchTerms(query.Search))
        {
            var pattern = $"%{term}%";
            payroll = payroll.Where(item =>
                EF.Functions.Like(item.BatchNumber, pattern)
                || EF.Functions.Like(item.Name, pattern)
                || (item.VoucherNumber != null && EF.Functions.Like(item.VoucherNumber, pattern))
                || (item.Notes != null && EF.Functions.Like(item.Notes, pattern))
                || (item.Project != null && (EF.Functions.Like(item.Project.Name, pattern) || EF.Functions.Like(item.Project.ProjectNumber, pattern)))
                || (item.LegalEntity != null && (EF.Functions.Like(item.LegalEntity.Name, pattern) || EF.Functions.Like(item.LegalEntity.ShortName, pattern)))
                || (item.Account != null && EF.Functions.Like(item.Account.AccountName, pattern))
                || item.Payments.Any(payment =>
                    (payment.RecipientNameSnapshot != null && EF.Functions.Like(payment.RecipientNameSnapshot, pattern))
                    || (payment.CrewNameSnapshot != null && EF.Functions.Like(payment.CrewNameSnapshot, pattern))
                    || (payment.Notes != null && EF.Functions.Like(payment.Notes, pattern))));
        }

        var rows = await payroll
            .OrderByDescending(item => item.PaymentDate)
            .ThenBy(item => item.BatchNumber)
            .Select(item => new
            {
                item.Id,
                item.BatchNumber,
                item.Name,
                PaymentDate = item.PaymentDate!.Value,
                LegalEntityId = item.LegalEntityId!.Value,
                LegalEntityName = item.LegalEntity!.Name,
                item.ProjectId,
                ProjectName = item.Project != null ? item.Project.Name : null,
                AccountId = item.AccountId!.Value,
                AccountName = item.Account!.AccountName,
                EmployeeAmount = item.Payments.Where(payment => payment.RecipientType == PayrollRecipientType.Employee).Sum(payment => (decimal?)payment.Amount) ?? 0m,
                CrewAmount = item.Payments.Where(payment => payment.RecipientType == PayrollRecipientType.CrewWorker).Sum(payment => (decimal?)payment.Amount) ?? 0m,
                item.ActualAmount,
                item.Status
            })
            .ToListAsync(token);

        return rows.Select(item => new CentralLedgerPayrollPaymentDto(
            item.Id,
            item.BatchNumber,
            item.Name,
            item.PaymentDate,
            item.LegalEntityId,
            item.LegalEntityName,
            item.ProjectId,
            item.ProjectName,
            item.AccountId,
            item.AccountName,
            item.EmployeeAmount,
            item.CrewAmount,
            item.ActualAmount,
            item.Status)).ToArray();
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
            .Include(item => item.LegalEntity).Include(item => item.BusinessPartner).Include(item => item.CounterLegalEntity).Include(item => item.Project).Include(item => item.Contract).Include(item => item.Account)
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
                item.Amount - item.Allocations.Sum(allocation => allocation.Amount), item.PaymentMethod, item.ConcurrencyStamp,
                item.CounterLegalEntityId, item.CounterLegalEntity?.Name))
            .Where(item => item.UnallocatedAmount > 0m)
            .ToArray();
    }

    private async Task<IReadOnlyList<CentralLedgerInvoiceDto>> SearchInvoicesAsync(
        CentralLedgerActor actor,
        CentralLedgerQuery query,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken token)
    {
        var projectIds = actor.ProjectIds.ToArray();
        var invoiceQuery = db.FinanceInvoices.AsNoTracking().AsSplitQuery()
            .Include(item => item.LegalEntity)
            .Include(item => item.BusinessPartner)
            .Include(item => item.CounterLegalEntity)
            .Include(item => item.Project)
            .Include(item => item.Contract)
            .Include(item => item.Allocations)
            .Where(item => item.Scope == query.Scope && item.Status == (query.RecordStatus ?? LedgerRecordStatus.Active))
            .Where(item => actor.LegalEntityIds.Contains(item.LegalEntityId))
            .Where(item => !item.ProjectId.HasValue || projectIds.Contains(item.ProjectId.Value));
        if (query.Scope == LedgerScope.Internal)
        {
            invoiceQuery = invoiceQuery.Where(item => item.CounterLegalEntityId.HasValue && actor.LegalEntityIds.Contains(item.CounterLegalEntityId.Value));
        }
        if (query.Direction.HasValue) invoiceQuery = invoiceQuery.Where(item => item.Direction == query.Direction);
        if (startDate.HasValue) invoiceQuery = invoiceQuery.Where(item => item.InvoiceDate >= startDate);
        if (endDate.HasValue) invoiceQuery = invoiceQuery.Where(item => item.InvoiceDate <= endDate);
        if (query.LegalEntityId.HasValue) invoiceQuery = invoiceQuery.Where(item => item.LegalEntityId == query.LegalEntityId);
        if (query.BusinessPartnerId.HasValue) invoiceQuery = invoiceQuery.Where(item => item.BusinessPartnerId == query.BusinessPartnerId);
        if (query.CounterLegalEntityId.HasValue) invoiceQuery = invoiceQuery.Where(item => item.CounterLegalEntityId == query.CounterLegalEntityId);
        if (query.ProjectId.HasValue) invoiceQuery = invoiceQuery.Where(item => item.ProjectId == query.ProjectId || item.Allocations.Any(allocation => allocation.ProjectId == query.ProjectId));
        if (query.ContractId.HasValue) invoiceQuery = invoiceQuery.Where(item => item.ContractId == query.ContractId || item.Allocations.Any(allocation => allocation.ContractId == query.ContractId));
        foreach (var term in SplitSearchTerms(query.Search))
        {
            var pattern = $"%{term}%";
            invoiceQuery = invoiceQuery.Where(item =>
                EF.Functions.Like(item.InvoiceNumber, pattern)
                || (item.InvoiceType != null && EF.Functions.Like(item.InvoiceType, pattern))
                || (item.Notes != null && EF.Functions.Like(item.Notes, pattern))
                || EF.Functions.Like(item.LegalEntity.Name, pattern)
                || (item.BusinessPartner != null && EF.Functions.Like(item.BusinessPartner.Name, pattern))
                || (item.CounterLegalEntity != null && EF.Functions.Like(item.CounterLegalEntity.Name, pattern))
                || (item.Project != null && EF.Functions.Like(item.Project.Name, pattern)));
        }

        var invoices = await invoiceQuery.OrderByDescending(item => item.InvoiceDate).ThenBy(item => item.InvoiceNumber).ToListAsync(token);
        return invoices.Select(item =>
        {
            var allocated = item.Allocations.Sum(allocation => allocation.Amount);
            return new CentralLedgerInvoiceDto(
                item.Id,
                item.Scope,
                item.Direction,
                item.InvoiceDate,
                item.LegalEntityId,
                item.LegalEntity.Name,
                item.BusinessPartnerId,
                item.BusinessPartner?.Name,
                item.CounterLegalEntityId,
                item.CounterLegalEntity?.Name,
                item.ProjectId,
                item.Project?.Name,
                item.ContractId,
                item.Contract?.Name,
                item.InvoiceNumber,
                item.InvoiceType,
                item.Amount,
                item.NetAmount,
                item.TaxAmount,
                item.TaxRate,
                allocated,
                Math.Max(item.Amount - allocated, 0m),
                item.Status,
                item.SourceType,
                item.SourceId,
                item.SourceUrl,
                item.Notes,
                item.ConcurrencyStamp);
        }).ToArray();
    }

    private async Task<IReadOnlyList<CentralLedgerCashEntryDto>> SearchCashEntriesAsync(
        CentralLedgerActor actor,
        CentralLedgerQuery query,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken token)
    {
        var projectIds = actor.ProjectIds.ToArray();
        var cashQuery = db.FinanceCashEntries.AsNoTracking().AsSplitQuery()
            .Include(item => item.LegalEntity)
            .Include(item => item.BusinessPartner)
            .Include(item => item.CounterLegalEntity)
            .Include(item => item.Project)
            .Include(item => item.Contract)
            .Include(item => item.Account)
            .Include(item => item.CounterAccount)
            .Include(item => item.Allocations)
            .Where(item => item.Scope == query.Scope && item.Status == (query.RecordStatus ?? LedgerRecordStatus.Active))
            .Where(item => actor.LegalEntityIds.Contains(item.LegalEntityId))
            .Where(item => !item.ProjectId.HasValue || projectIds.Contains(item.ProjectId.Value));
        if (query.Scope == LedgerScope.Internal)
        {
            cashQuery = cashQuery.Where(item => item.CounterLegalEntityId.HasValue && actor.LegalEntityIds.Contains(item.CounterLegalEntityId.Value));
        }
        if (query.Direction.HasValue) cashQuery = cashQuery.Where(item => item.Direction == query.Direction);
        if (startDate.HasValue) cashQuery = cashQuery.Where(item => item.BusinessDate >= startDate);
        if (endDate.HasValue) cashQuery = cashQuery.Where(item => item.BusinessDate <= endDate);
        if (query.LegalEntityId.HasValue) cashQuery = cashQuery.Where(item => item.LegalEntityId == query.LegalEntityId);
        if (query.BusinessPartnerId.HasValue) cashQuery = cashQuery.Where(item => item.BusinessPartnerId == query.BusinessPartnerId);
        if (query.CounterLegalEntityId.HasValue) cashQuery = cashQuery.Where(item => item.CounterLegalEntityId == query.CounterLegalEntityId);
        if (query.ProjectId.HasValue) cashQuery = cashQuery.Where(item => item.ProjectId == query.ProjectId || item.Allocations.Any(allocation => allocation.ProjectId == query.ProjectId));
        if (query.ContractId.HasValue) cashQuery = cashQuery.Where(item => item.ContractId == query.ContractId || item.Allocations.Any(allocation => allocation.ContractId == query.ContractId));
        foreach (var term in SplitSearchTerms(query.Search))
        {
            var pattern = $"%{term}%";
            cashQuery = cashQuery.Where(item =>
                (item.PaymentMethod != null && EF.Functions.Like(item.PaymentMethod, pattern))
                || (item.Notes != null && EF.Functions.Like(item.Notes, pattern))
                || EF.Functions.Like(item.LegalEntity.Name, pattern)
                || (item.BusinessPartner != null && EF.Functions.Like(item.BusinessPartner.Name, pattern))
                || (item.CounterLegalEntity != null && EF.Functions.Like(item.CounterLegalEntity.Name, pattern))
                || (item.Project != null && EF.Functions.Like(item.Project.Name, pattern))
                || (item.Account != null && EF.Functions.Like(item.Account.AccountName, pattern)));
        }

        var cashEntries = await cashQuery.OrderByDescending(item => item.BusinessDate).ThenBy(item => item.Id).ToListAsync(token);
        return cashEntries.Select(item =>
        {
            var allocated = item.Allocations.Sum(allocation => allocation.Amount);
            return new CentralLedgerCashEntryDto(
                item.Id,
                item.Scope,
                item.Direction,
                item.CashType,
                item.IsReversal,
                item.BusinessDate,
                item.LegalEntityId,
                item.LegalEntity.Name,
                item.BusinessPartnerId,
                item.BusinessPartner?.Name,
                item.CounterLegalEntityId,
                item.CounterLegalEntity?.Name,
                item.ProjectId,
                item.Project?.Name,
                item.ContractId,
                item.Contract?.Name,
                item.AccountId,
                item.Account?.AccountName,
                item.CounterAccountId,
                item.CounterAccount?.AccountName,
                item.Amount,
                allocated,
                Math.Max(item.Amount - allocated, 0m),
                item.PaymentMethod,
                item.Status,
                item.SourceType,
                item.SourceId,
                item.SourceUrl,
                item.Notes,
                item.ConcurrencyStamp);
        }).ToArray();
    }

    private async Task<IReadOnlyList<CentralLedgerDeductionDto>> SearchDeductionsAsync(
        CentralLedgerActor actor,
        CentralLedgerQuery query,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken token)
    {
        var projectIds = actor.ProjectIds.ToArray();
        var deductions = await db.FinanceDeductions.AsNoTracking()
            .Where(item => item.Status == (query.RecordStatus ?? LedgerRecordStatus.Active)
                && item.Settlement.Scope == query.Scope
                && actor.LegalEntityIds.Contains(item.Settlement.LegalEntityId)
                && (!item.Settlement.ProjectId.HasValue || projectIds.Contains(item.Settlement.ProjectId.Value)))
            .Include(item => item.Settlement).ThenInclude(item => item.LegalEntity)
            .Include(item => item.Settlement).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.Settlement).ThenInclude(item => item.CounterLegalEntity)
            .Include(item => item.Settlement).ThenInclude(item => item.Project)
            .ToListAsync(token);
        if (query.Scope == LedgerScope.Internal)
        {
            deductions = deductions.Where(item => item.Settlement.CounterLegalEntityId.HasValue && actor.LegalEntityIds.Contains(item.Settlement.CounterLegalEntityId.Value)).ToList();
        }
        if (query.Direction.HasValue) deductions = deductions.Where(item => item.Settlement.Direction == query.Direction).ToList();
        if (startDate.HasValue) deductions = deductions.Where(item => item.BusinessDate >= startDate).ToList();
        if (endDate.HasValue) deductions = deductions.Where(item => item.BusinessDate <= endDate).ToList();
        if (query.LegalEntityId.HasValue) deductions = deductions.Where(item => item.Settlement.LegalEntityId == query.LegalEntityId).ToList();
        if (query.BusinessPartnerId.HasValue) deductions = deductions.Where(item => item.Settlement.BusinessPartnerId == query.BusinessPartnerId).ToList();
        if (query.CounterLegalEntityId.HasValue) deductions = deductions.Where(item => item.Settlement.CounterLegalEntityId == query.CounterLegalEntityId).ToList();
        if (query.ProjectId.HasValue) deductions = deductions.Where(item => item.Settlement.ProjectId == query.ProjectId).ToList();
        foreach (var term in SplitSearchTerms(query.Search))
        {
            deductions = deductions.Where(item => item.Reason.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Settlement.LegalEntity.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (item.Settlement.BusinessPartner?.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.Settlement.Project?.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }
        return deductions.OrderByDescending(item => item.BusinessDate).ThenBy(item => item.Id).Select(item => new CentralLedgerDeductionDto(
            item.Id,
            item.SettlementId,
            item.Settlement.Scope,
            item.Settlement.Direction,
            item.BusinessDate,
            item.Settlement.LegalEntityId,
            item.Settlement.LegalEntity.Name,
            item.Settlement.BusinessPartnerId,
            item.Settlement.BusinessPartner?.Name,
            item.Settlement.CounterLegalEntityId,
            item.Settlement.CounterLegalEntity?.Name,
            item.Settlement.ProjectId,
            item.Settlement.Project?.Name,
            item.Amount,
            item.ReduceInvoiceAmount,
            item.Reason,
            item.Status,
            item.SourceType,
            item.SourceId,
            item.ConcurrencyStamp)).ToArray();
    }

    private async Task<IReadOnlyList<CentralLedgerAuditDto>> SearchAuditEntriesAsync(
        CentralLedgerActor actor,
        CentralLedgerQuery query,
        IReadOnlySet<Guid> settlementIds,
        IReadOnlySet<Guid> headerIds,
        CancellationToken token)
    {
        var allowedIds = settlementIds.Select(item => item.ToString()).Concat(headerIds.Select(item => item.ToString())).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var audits = await db.AuditLogs.AsNoTracking()
            .Where(item => item.EntityType.Contains("Finance") || item.EntityType.Contains("Ledger"))
            .ToListAsync(token);
        var deletionLogs = await db.FinanceDeletionLogs.AsNoTracking()
            .Where(item => item.LegalEntityId.HasValue && actor.LegalEntityIds.Contains(item.LegalEntityId.Value))
            .ToListAsync(token);
        var result = audits
            .Where(item => allowedIds.Contains(item.EntityId))
            .OrderByDescending(item => item.OccurredAt)
            .Take(500)
            .Select(item => new CentralLedgerAuditDto(item.EntityId, item.EntityType, item.Action, item.OccurredAt, item.UserName, item.Reason, false))
            .Concat(deletionLogs
                .OrderByDescending(item => item.DeletedAt)
                .Take(200)
                .Select(item => new CentralLedgerAuditDto(item.RecordId.ToString(), item.RecordType.ToString(), "Delete", item.DeletedAt, item.DeletedByUserName, item.Reason, true)))
            .OrderByDescending(item => item.OccurredAt)
            .ToArray();
        return result;
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
                    settlement.DueDate,
                    settlement.SettlementDate,
                    settlement.OriginalAmount,
                    settlement.OriginalInvoiceAmount,
                    settlement.SettlementState,
                    settlement.Status,
                    settlement.SourceType,
                    settlement.SourceId,
                    settlement.SourceUrl,
                    LegalEntity = settlement.LegalEntity.Name,
                    BusinessPartner = settlement.BusinessPartner?.Name,
                    CounterLegalEntity = settlement.CounterLegalEntity?.Name,
                    Project = settlement.Project?.Name,
                    Contract = settlement.Contract?.Name,
                    settlement.Notes
                }),
                row.Metrics,
                allocations,
                settlement.ConcurrencyStamp)
            {
                SourceType = settlement.SourceType,
                SourceId = settlement.SourceId,
                SourceUrl = settlement.SourceUrl,
                SourceLabel = settlement.SourceType == LedgerSourceType.CentralLedger ? "中央账本直接录入" : $"{settlement.SourceType} 来源记录"
            };
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

    public async Task<IReadOnlyDictionary<Guid, PartnerLedgerSummaryDto>> GetPartnerSummariesAsync(
        CentralLedgerActor actor,
        IReadOnlyCollection<Guid> businessPartnerIds,
        CancellationToken token)
    {
        var partnerIds = businessPartnerIds.Distinct().ToArray();
        if (partnerIds.Length == 0) return new Dictionary<Guid, PartnerLedgerSummaryDto>();

        var legalEntityIds = actor.LegalEntityIds.ToArray();
        var projectIds = actor.ProjectIds.ToArray();
        var settlements = await db.FinanceSettlements.AsNoTracking().AsSplitQuery()
            .Where(item => item.Scope == LedgerScope.External && item.Status == LedgerRecordStatus.Active)
            .Where(item => item.BusinessPartnerId.HasValue && partnerIds.Contains(item.BusinessPartnerId.Value))
            .Where(item => legalEntityIds.Contains(item.LegalEntityId))
            .Where(item => !item.ProjectId.HasValue || projectIds.Contains(item.ProjectId.Value))
            .Include(item => item.LegalEntity)
            .Include(item => item.BusinessPartner)
            .Include(item => item.Project)
            .Include(item => item.Contract)
            .Include(item => item.Adjustments)
            .Include(item => item.Deductions)
            .Include(item => item.InvoiceAllocations).ThenInclude(item => item.Invoice)
            .Include(item => item.CashAllocations).ThenInclude(item => item.CashEntry)
            .ToListAsync(token);

        var summaries = partnerIds.ToDictionary(id => id, PartnerLedgerSummaryDto.Empty);
        foreach (var row in settlements.Select(ToRow))
        {
            var partnerId = row.BusinessPartnerId!.Value;
            var summary = summaries[partnerId];
            summaries[partnerId] = row.Direction == LedgerDirection.Receivable
                ? summary with { Receivable = CentralLedgerCalculator.Add(summary.Receivable, row.Metrics) }
                : summary with { Payable = CentralLedgerCalculator.Add(summary.Payable, row.Metrics) };
        }

        return summaries;
    }

    private async Task<CentralLedgerDetailsDto?> GetHeaderDetailsAsync(
        CentralLedgerActor actor,
        FinanceRecordType type,
        Guid id,
        CancellationToken token)
    {
        if (type == FinanceRecordType.Invoice)
        {
            var invoice = await db.FinanceInvoices.AsNoTracking()
                .Include(item => item.Allocations)
                .Include(item => item.LegalEntity)
                .Include(item => item.BusinessPartner)
                .Include(item => item.CounterLegalEntity)
                .Include(item => item.Project)
                .Include(item => item.Contract)
                .SingleOrDefaultAsync(item => item.Id == id, token);
            if (invoice is null) return null;
            EnsureCanRead(actor, invoice.LegalEntityId, invoice.CounterLegalEntityId, null);
            var allocated = invoice.Allocations.Sum(item => item.Amount);
            return new CentralLedgerDetailsDto(
                type,
                id,
                invoice.Scope,
                invoice.Direction,
                JsonSerializer.Serialize(new
                {
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.InvoiceDate,
                    invoice.InvoiceType,
                    invoice.Amount,
                    invoice.NetAmount,
                    invoice.TaxAmount,
                    invoice.TaxRate,
                    AllocatedAmount = allocated,
                    UnallocatedAmount = Math.Max(invoice.Amount - allocated, 0m),
                    invoice.Status,
                    invoice.SourceType,
                    invoice.SourceId,
                    invoice.SourceUrl,
                    LegalEntity = invoice.LegalEntity.Name,
                    BusinessPartner = invoice.BusinessPartner?.Name,
                    CounterLegalEntity = invoice.CounterLegalEntity?.Name,
                    Project = invoice.Project?.Name,
                    Contract = invoice.Contract?.Name,
                    invoice.Notes
                }),
                CentralLedgerMetrics.Zero,
                invoice.Allocations.Select(item => new FinanceAllocationDto(item.Id, item.SettlementId, item.ProjectId, item.ContractId, item.ContractLineItemId, item.Amount, item.AllocationOrder)).ToArray(),
                invoice.ConcurrencyStamp)
            {
                SourceType = invoice.SourceType,
                SourceId = invoice.SourceId,
                SourceUrl = invoice.SourceUrl,
                SourceLabel = invoice.SourceType == LedgerSourceType.CentralLedger ? "中央账本直接录入" : $"{invoice.SourceType} 来源记录"
            };
        }
        if (type == FinanceRecordType.Cash)
        {
            var cash = await db.FinanceCashEntries.AsNoTracking()
                .Include(item => item.Allocations)
                .Include(item => item.LegalEntity)
                .Include(item => item.BusinessPartner)
                .Include(item => item.CounterLegalEntity)
                .Include(item => item.Project)
                .Include(item => item.Contract)
                .Include(item => item.Account)
                .Include(item => item.CounterAccount)
                .SingleOrDefaultAsync(item => item.Id == id, token);
            if (cash is null) return null;
            EnsureCanRead(actor, cash.LegalEntityId, cash.CounterLegalEntityId, null);
            var allocated = cash.Allocations.Sum(item => item.Amount);
            return new CentralLedgerDetailsDto(
                type,
                id,
                cash.Scope,
                cash.Direction,
                JsonSerializer.Serialize(new
                {
                    cash.Id,
                    cash.BusinessDate,
                    cash.CashType,
                    cash.IsReversal,
                    cash.Amount,
                    AllocatedAmount = allocated,
                    UnallocatedAmount = Math.Max(cash.Amount - allocated, 0m),
                    cash.PaymentMethod,
                    cash.Status,
                    cash.SourceType,
                    cash.SourceId,
                    cash.SourceUrl,
                    LegalEntity = cash.LegalEntity.Name,
                    BusinessPartner = cash.BusinessPartner?.Name,
                    CounterLegalEntity = cash.CounterLegalEntity?.Name,
                    Project = cash.Project?.Name,
                    Contract = cash.Contract?.Name,
                    Account = cash.Account?.AccountName,
                    CounterAccount = cash.CounterAccount?.AccountName,
                    cash.Notes
                }),
                CentralLedgerMetrics.Zero,
                cash.Allocations.Select(item => new FinanceAllocationDto(item.Id, item.SettlementId, item.ProjectId, item.ContractId, item.ContractLineItemId, item.Amount, item.AllocationOrder)).ToArray(),
                cash.ConcurrencyStamp)
            {
                SourceType = cash.SourceType,
                SourceId = cash.SourceId,
                SourceUrl = cash.SourceUrl,
                SourceLabel = cash.SourceType == LedgerSourceType.CentralLedger ? "中央账本直接录入" : $"{cash.SourceType} 来源记录"
            };
        }
        if (type == FinanceRecordType.Deduction)
        {
            var deduction = await db.FinanceDeductions.AsNoTracking()
                .Include(item => item.Settlement).ThenInclude(item => item.LegalEntity)
                .Include(item => item.Settlement).ThenInclude(item => item.BusinessPartner)
                .Include(item => item.Settlement).ThenInclude(item => item.CounterLegalEntity)
                .Include(item => item.Settlement).ThenInclude(item => item.Project)
                .SingleOrDefaultAsync(item => item.Id == id, token);
            if (deduction is null) return null;
            EnsureCanRead(actor, deduction.Settlement.LegalEntityId, deduction.Settlement.CounterLegalEntityId, deduction.Settlement.ProjectId);
            return new CentralLedgerDetailsDto(
                type,
                id,
                deduction.Settlement.Scope,
                deduction.Settlement.Direction,
                JsonSerializer.Serialize(new
                {
                    deduction.Id,
                    deduction.SettlementId,
                    deduction.BusinessDate,
                    deduction.Amount,
                    deduction.ReduceInvoiceAmount,
                    deduction.Reason,
                    deduction.Status,
                    deduction.SourceType,
                    deduction.SourceId,
                    LegalEntity = deduction.Settlement.LegalEntity.Name,
                    BusinessPartner = deduction.Settlement.BusinessPartner?.Name,
                    CounterLegalEntity = deduction.Settlement.CounterLegalEntity?.Name,
                    Project = deduction.Settlement.Project?.Name
                }),
                CentralLedgerMetrics.Zero,
                [],
                deduction.ConcurrencyStamp)
            {
                SourceType = deduction.SourceType,
                SourceId = deduction.SourceId,
                SourceLabel = deduction.SourceType == LedgerSourceType.CentralLedger ? "中央账本直接录入" : $"{deduction.SourceType} 来源记录"
            };
        }
        if (type == FinanceRecordType.Adjustment)
        {
            var adjustment = await db.FinanceSettlementAdjustments.AsNoTracking().Include(item => item.Settlement)
                .SingleOrDefaultAsync(item => item.Id == id, token);
            if (adjustment is null) return null;
            EnsureCanRead(actor, adjustment.Settlement.LegalEntityId, adjustment.Settlement.CounterLegalEntityId, adjustment.Settlement.ProjectId);
            return new CentralLedgerDetailsDto(
                type,
                id,
                adjustment.Settlement.Scope,
                adjustment.Settlement.Direction,
                JsonSerializer.Serialize(new
                {
                    adjustment.Id,
                    adjustment.SettlementId,
                    adjustment.AdjustmentType,
                    adjustment.AmountDelta,
                    adjustment.InvoiceAmountDelta,
                    adjustment.BusinessDate,
                    adjustment.Reason,
                    adjustment.Status,
                    adjustment.SourceType,
                    adjustment.SourceId
                }),
                CentralLedgerMetrics.Zero,
                [],
                adjustment.ConcurrencyStamp)
            {
                SourceType = adjustment.SourceType,
                SourceId = adjustment.SourceId,
                SourceLabel = adjustment.SourceType == LedgerSourceType.CentralLedger ? "中央账本直接录入" : $"{adjustment.SourceType} 来源记录"
            };
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
            settlement.ConcurrencyStamp)
        {
            SourceType = settlement.SourceType,
            SourceId = settlement.SourceId,
            SourceUrl = settlement.SourceUrl
        };
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
