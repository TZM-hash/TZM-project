using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace EngineeringManager.Infrastructure.Finance;

public sealed class FinanceLedgerService(ApplicationDbContext db) : IFinanceLedgerService
{
    public async Task<Guid> CreateAccountAsync(CreateFinancialAccountRequest request, CancellationToken cancellationToken)
    {
        var accountName = NormalizeRequired(request.AccountName, nameof(request.AccountName));
        if (!await db.LegalEntities.AnyAsync(item => item.Id == request.LegalEntityId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("签约公司不存在或已停用。");
        }

        if (await db.FinancialAccounts.AnyAsync(item => item.LegalEntityId == request.LegalEntityId && item.AccountName == accountName, cancellationToken))
        {
            throw new InvalidOperationException($"当前签约公司下的账户名称已存在：{accountName}");
        }

        var account = new FinancialAccount
        {
            LegalEntityId = request.LegalEntityId,
            AccountName = accountName,
            AccountNumber = NormalizeOptional(request.AccountNumber),
            BankName = NormalizeOptional(request.BankName),
            AccountType = request.AccountType,
            OpeningBalance = request.OpeningBalance,
            Notes = NormalizeOptional(request.Notes)
        };
        db.FinancialAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    public async Task<IReadOnlyList<FinancialAccountDto>> ListAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await db.FinancialAccounts
            .AsNoTracking()
            .Include(item => item.LegalEntity)
            .OrderBy(item => item.LegalEntity.Name)
            .ThenBy(item => item.AccountName)
            .ToListAsync(cancellationToken);
        var accountIds = accounts.Select(item => item.Id).ToArray();
        var movements = await db.AccountTransactions
            .AsNoTracking()
            .Where(item => accountIds.Contains(item.AccountId))
            .GroupBy(item => new { item.AccountId, item.Direction })
            .Select(group => new { group.Key.AccountId, group.Key.Direction, Amount = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);
        return accounts.Select(account =>
        {
            var inflow = movements.Where(item => item.AccountId == account.Id && item.Direction == AccountTransactionDirection.Inflow).Sum(item => item.Amount);
            var outflow = movements.Where(item => item.AccountId == account.Id && item.Direction == AccountTransactionDirection.Outflow).Sum(item => item.Amount);
            return new FinancialAccountDto(
                account.Id,
                account.LegalEntityId,
                account.LegalEntity.Name,
                account.AccountName,
                account.AccountNumber,
                account.BankName,
                account.AccountType,
                account.OpeningBalance,
                account.OpeningBalance + inflow - outflow,
                account.IsActive,
                account.Notes);
        }).ToArray();
    }

    public async Task<IReadOnlyList<ProjectFinanceListItemDto>> ListProjectSummariesAsync(CancellationToken cancellationToken)
    {
        var projects = await db.Projects
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.ProjectNumber)
            .Select(item => new ProjectSummarySeed(item.Id, item.ProjectNumber, item.Name))
            .ToListAsync(cancellationToken);
        return await BuildProjectSummariesAsync(projects, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectFinanceListItemDto>> ListProjectSummariesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        if (projectIds.Count == 0) return [];
        var projects = await db.Projects
            .AsNoTracking()
            .Where(item => item.IsActive && projectIds.Contains(item.Id))
            .OrderBy(item => item.ProjectNumber)
            .Select(item => new ProjectSummarySeed(item.Id, item.ProjectNumber, item.Name))
            .ToListAsync(cancellationToken);
        return await BuildProjectSummariesAsync(projects, cancellationToken);
    }

    private async Task<IReadOnlyList<ProjectFinanceListItemDto>> BuildProjectSummariesAsync(
        IReadOnlyList<ProjectSummarySeed> projects,
        CancellationToken cancellationToken)
    {
        var results = new List<ProjectFinanceListItemDto>(projects.Count);
        foreach (var project in projects)
        {
            results.Add(new ProjectFinanceListItemDto(
                project.Id,
                project.ProjectNumber,
                project.Name,
                await GetProjectSummaryAsync(project.Id, cancellationToken)));
        }

        return results;
    }

    private sealed record ProjectSummarySeed(Guid Id, string ProjectNumber, string Name);

    public async Task<FinanceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var projects = await ListProjectSummariesAsync(cancellationToken);
        var summaries = projects.Select(item => item.Summary).ToArray();
        return new FinanceOverviewDto(
            projects,
            new FinanceProjectSummaryDto(
                Guid.Empty,
                summaries.Sum(item => item.ReceivableAmount),
                summaries.Sum(item => item.CollectedAmount),
                summaries.Sum(item => item.UncollectedAmount),
                summaries.Sum(item => item.PayableAmount),
                summaries.Sum(item => item.PaidAmount),
                summaries.Sum(item => item.DeductionAmount),
                summaries.Sum(item => item.UnpaidAmount),
                summaries.Sum(item => item.OutputInvoiceAmount),
                summaries.Sum(item => item.UninvoicedAmount),
                summaries.Sum(item => item.InputInvoiceAmount),
                summaries.Any(item => item.HasCollectionRisk),
                summaries.Any(item => item.HasPaymentRisk)));
    }

    public async Task<FinanceOverviewPageDto> SearchOverviewAsync(FinanceOverviewQuery query, CancellationToken cancellationToken)
    {
        IEnumerable<ProjectFinanceListItemDto> items = await ListProjectSummariesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            items = items.Where(item => item.ProjectNumber.Contains(term, StringComparison.OrdinalIgnoreCase) || item.ProjectName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (query.MinimumReceivable.HasValue) items = items.Where(item => item.Summary.ReceivableAmount >= query.MinimumReceivable.Value);
        if (query.MinimumUncollected.HasValue) items = items.Where(item => item.Summary.UncollectedAmount >= query.MinimumUncollected.Value);
        if (query.RiskOnly) items = items.Where(item => item.Summary.HasCollectionRisk || item.Summary.HasPaymentRisk);
        items = SortOverview(items, query.SortKey, query.SortDescending);

        var matching = items.ToArray();
        var summaries = matching.Select(item => item.Summary).ToArray();
        var total = SumOverview(summaries);
        var pageSize = query.PageSize is 20 or 50 or 100 ? query.PageSize : 20;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)matching.Length / pageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);
        return new FinanceOverviewPageDto(
            matching.Skip((page - 1) * pageSize).Take(pageSize).ToArray(),
            total,
            page,
            pageSize,
            matching.Length,
            totalPages,
            matching.Select(item => item.ProjectId).ToArray());
    }

    public async Task<FinanceEntryOptionsDto> GetEntryOptionsAsync(CancellationToken cancellationToken)
    {
        var projects = await db.Projects.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.ProjectNumber)
            .Select(item => new FinanceOptionDto(item.Id, item.ProjectNumber + " · " + item.Name, null)).ToListAsync(cancellationToken);
        var contracts = await db.Contracts.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.ContractNumber)
            .Select(item => new FinanceOptionDto(item.Id, item.ContractNumber + " · " + item.Name, item.ProjectId)).ToListAsync(cancellationToken);
        var legalEntities = await db.LegalEntities.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Code)
            .Select(item => new FinanceOptionDto(item.Id, item.Code + " · " + item.ShortName, null)).ToListAsync(cancellationToken);
        var partners = await db.BusinessPartners.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.PartnerNumber)
            .Select(item => new FinanceOptionDto(item.Id, item.PartnerNumber + " · " + item.ShortName, null)).ToListAsync(cancellationToken);
        var accounts = await db.FinancialAccounts.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.AccountName)
            .Select(item => new FinanceOptionDto(item.Id, item.AccountName, item.LegalEntityId)).ToListAsync(cancellationToken);
        var receivables = await db.ReceivableEntries.AsNoTracking().Where(item => !item.IsVoided).OrderByDescending(item => item.EntryDate)
            .Select(item => new FinanceOptionDto(item.Id, item.EntryDate + " · 应收 " + item.Amount, item.ProjectId)).ToListAsync(cancellationToken);
        var payables = await db.PayableEntries.AsNoTracking().Where(item => !item.IsVoided).OrderByDescending(item => item.EntryDate)
            .Select(item => new FinanceOptionDto(item.Id, item.EntryDate + " · 应付 " + item.Amount, item.ProjectId)).ToListAsync(cancellationToken);
        var collections = await db.CollectionEntries.AsNoTracking().OrderByDescending(item => item.CollectionDate)
            .Select(item => new FinanceOptionDto(item.Id, item.CollectionDate + " · 收款 " + item.Amount, item.ProjectId)).ToListAsync(cancellationToken);
        var payments = await db.PaymentEntries.AsNoTracking().OrderByDescending(item => item.PaymentDate)
            .Select(item => new FinanceOptionDto(item.Id, item.PaymentDate + " · 付款 " + item.Amount, item.ProjectId)).ToListAsync(cancellationToken);
        var projectLegalEntities = await db.ProjectLegalEntities.AsNoTracking().Where(item => item.Project.IsActive && item.LegalEntity.IsActive)
            .OrderByDescending(item => item.IsPrimary).ThenBy(item => item.LegalEntity.Code)
            .Select(item => new FinanceOptionDto(item.LegalEntityId, item.LegalEntity.Code + " · " + item.LegalEntity.ShortName, item.ProjectId)).ToListAsync(cancellationToken);
        var projectTaxConfigurations = await db.ProjectTaxConfigurations.AsNoTracking().Where(item => item.Project.IsActive && item.IsActive)
            .OrderBy(item => item.TaxRate).ThenBy(item => item.InvoiceType)
            .Select(item => new FinanceOptionDto(item.Id,
                (item.TaxRate * 100m).ToString("0", CultureInfo.InvariantCulture) + "% · " + (item.InvoiceType == ProjectInvoiceType.Ordinary ? "普票" : "专票"), item.ProjectId))
            .ToListAsync(cancellationToken);
        return new FinanceEntryOptionsDto(projects, contracts, legalEntities, partners, accounts, receivables, payables, collections, payments, projectLegalEntities, projectTaxConfigurations);
    }

    public async Task<Guid> AddReceivableAsync(CreateReceivableRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, false, cancellationToken);
        var command = new CentralLedgerCommandService(db);
        return await command.CreateSettlementAsync(
            CreateCompatibilityActor(request.LegalEntityId, request.ProjectId),
            new CreateSettlementRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSettlementState.Final,
                LedgerSourceType.CentralLedger,
                null,
                request.LegalEntityId,
                request.BusinessPartnerId,
                null,
                request.ProjectId,
                request.ContractId,
                null,
                request.EntryDate,
                request.Amount,
                request.Amount,
                request.Description,
                request.DueDate),
            cancellationToken);
    }

    public async Task<Guid> RecordCollectionAsync(RecordCollectionRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, false, cancellationToken);
        await ValidateAccountAsync(request.AccountId, request.LegalEntityId, cancellationToken);
        if (request.ReceivableEntryId.HasValue && !await db.FinanceSettlements.AnyAsync(
                item => item.Id == request.ReceivableEntryId && item.ProjectId == request.ProjectId &&
                    item.Direction == LedgerDirection.Receivable && item.Status == LedgerRecordStatus.Active,
                cancellationToken))
        {
            throw new InvalidOperationException("应收记录不存在、已作废或不属于当前项目。");
        }
        var allocations = request.ReceivableEntryId.HasValue
            ? new[] { new FinanceAllocationRequest(request.ReceivableEntryId.Value, request.Amount, 1) }
            : await BuildProjectCollectionAllocationsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, request.Amount, null, cancellationToken);
        var command = new CentralLedgerCommandService(db);
        return await command.CreateCashAsync(
            CreateCompatibilityActor(request.LegalEntityId, request.ProjectId),
            new CreateFinanceCashRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerCashType.Collection,
                request.ReceivableEntryId.HasValue ? LedgerSourceType.CentralLedger : LedgerSourceType.ProjectCollection,
                request.ReceivableEntryId.HasValue ? null : request.ProjectId,
                request.LegalEntityId,
                request.BusinessPartnerId,
                null,
                request.AccountId,
                null,
                request.CollectionDate,
                request.Amount,
                request.PaymentMethod,
                request.Notes,
                allocations,
                ProjectId: request.ProjectId,
                ContractId: request.ContractId,
                EntryId: request.EntryId),
            cancellationToken);
    }

    public async Task<Guid> RecordRefundAsync(RecordRefundRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        var original = request.CollectionEntryId.HasValue
            ? await db.FinanceCashEntries.Include(item => item.Allocations)
                .SingleOrDefaultAsync(item => item.Id == request.CollectionEntryId && item.CashType == LedgerCashType.Collection, cancellationToken)
                ?? throw new InvalidOperationException("原收款记录不存在。")
            : null;
        if (original is not null && original.AccountId != request.AccountId)
            throw new InvalidOperationException("退款或收款冲销必须使用原收款账户。");
        var settlement = request.ReceivableEntryId.HasValue
            ? await db.FinanceSettlements.SingleOrDefaultAsync(item => item.Id == request.ReceivableEntryId && item.Direction == LedgerDirection.Receivable, cancellationToken)
                ?? throw new InvalidOperationException("关联应收记录不存在。")
            : original?.Allocations.Select(item => item.Settlement).FirstOrDefault();
        if (settlement is null && original is not null && original.Allocations.Count > 0)
        {
            settlement = await db.FinanceSettlements.SingleAsync(item => item.Id == original.Allocations.First().SettlementId, cancellationToken);
        }
        if (settlement is null) throw new ArgumentException("退款或收款冲销至少需要关联原收款或应收记录。", nameof(request));
        await ValidateAccountAsync(request.AccountId, settlement.LegalEntityId, cancellationToken);

        var entry = new FinanceCashEntry
        {
            Scope = settlement.Scope,
            Direction = LedgerDirection.Receivable,
            CashType = LedgerCashType.Collection,
            LegalEntityId = settlement.LegalEntityId,
            BusinessPartnerId = settlement.BusinessPartnerId,
            CounterLegalEntityId = settlement.CounterLegalEntityId,
            AccountId = request.AccountId,
            IsReversal = true,
            ReversesCashEntryId = original?.Id,
            BusinessDate = request.EntryDate,
            Amount = request.Amount,
            Notes = reason,
            SourceType = LedgerSourceType.CentralLedger
        };
        entry.Allocations.Add(new FinanceCashAllocation
        {
            CashEntry = entry,
            Settlement = settlement,
            ProjectId = settlement.ProjectId,
            ContractId = settlement.ContractId,
            ContractLineItemId = settlement.ContractLineItemId,
            BusinessPartnerId = settlement.BusinessPartnerId,
            Amount = request.Amount,
            AllocationOrder = 1
        });
        db.FinanceCashEntries.Add(entry);
        db.AccountTransactions.Add(CreateTransaction(request.AccountId, AccountTransactionDirection.Outflow, AccountTransactionSourceType.Refund, entry.Id, request.EntryDate, request.Amount, reason));
        if (settlement.ProjectId.HasValue) AddProjectAudit("RecordCollectionReversal", nameof(FinanceCashEntry), entry.Id, settlement.ProjectId.Value, $"退款/收款冲销 {entry.Amount:N2}", new { entry.BusinessDate, entry.Amount, request.AdjustmentType, Reason = reason });
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<Guid> AddPayableAsync(CreatePayableRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, true, cancellationToken);
        var command = new CentralLedgerCommandService(db);
        return await command.CreateSettlementAsync(
            CreateCompatibilityActor(request.LegalEntityId, request.ProjectId),
            new CreateSettlementRequest(
                LedgerScope.External,
                LedgerDirection.Payable,
                LedgerSettlementState.Final,
                LedgerSourceType.CentralLedger,
                null,
                request.LegalEntityId,
                request.BusinessPartnerId,
                null,
                request.ProjectId,
                request.ContractId,
                null,
                request.EntryDate,
                request.Amount,
                request.Amount,
                request.Description,
                request.DueDate),
            cancellationToken);
    }

    public async Task<Guid> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, true, cancellationToken);
        await ValidateAccountAsync(request.AccountId, request.LegalEntityId, cancellationToken);
        if (request.PayableEntryId.HasValue && !await db.FinanceSettlements.AnyAsync(
                item => item.Id == request.PayableEntryId && item.ProjectId == request.ProjectId &&
                    item.Direction == LedgerDirection.Payable && item.Status == LedgerRecordStatus.Active,
                cancellationToken))
        {
            throw new InvalidOperationException("应付记录不存在、已作废或不属于当前项目。");
        }
        var allocations = request.PayableEntryId.HasValue
            ? new[] { new FinanceAllocationRequest(request.PayableEntryId.Value, request.Amount, 1) }
            : [];
        var command = new CentralLedgerCommandService(db);
        return await command.CreateCashAsync(
            CreateCompatibilityActor(request.LegalEntityId, request.ProjectId),
            new CreateFinanceCashRequest(
                LedgerScope.External,
                LedgerDirection.Payable,
                LedgerCashType.Payment,
                LedgerSourceType.CentralLedger,
                null,
                request.LegalEntityId,
                request.BusinessPartnerId,
                null,
                request.AccountId,
                null,
                request.PaymentDate,
                request.Amount,
                request.PaymentMethod.ToString(),
                request.Notes,
                allocations,
                ProjectId: request.ProjectId,
                ContractId: request.ContractId),
            cancellationToken);
    }

    public async Task<Guid> AddDeductionAsync(CreateDeductionRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        var settlement = await db.FinanceSettlements.SingleOrDefaultAsync(item =>
                item.Id == request.PayableEntryId &&
                item.ProjectId == request.ProjectId &&
                item.LegalEntityId == request.LegalEntityId &&
                item.BusinessPartnerId == request.BusinessPartnerId &&
                item.Direction == LedgerDirection.Payable &&
                item.Status == LedgerRecordStatus.Active,
                cancellationToken);
        if (settlement is null)
        {
            throw new InvalidOperationException("应付记录不存在、已作废或与扣款维度不一致。");
        }
        return await new CentralLedgerCommandService(db).AddDeductionAsync(
            CreateCompatibilityActor(request.LegalEntityId, request.ProjectId),
            new AddFinanceDeductionRequest(settlement.Id, request.EntryDate, request.Amount, false, reason, settlement.ConcurrencyStamp),
            cancellationToken);
    }

    public async Task<Guid> RecordPaymentReversalAsync(RecordPaymentReversalRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        var payment = await db.FinanceCashEntries.Include(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.Id == request.PaymentEntryId && item.CashType == LedgerCashType.Payment, cancellationToken)
            ?? throw new InvalidOperationException("原付款记录不存在。");
        if (payment.AccountId != request.AccountId) throw new InvalidOperationException("付款冲销必须使用原付款账户。");
        var allocation = payment.Allocations.FirstOrDefault() ?? throw new InvalidOperationException("原付款没有可冲销的结算分摊。");
        var settlement = await db.FinanceSettlements.SingleAsync(item => item.Id == allocation.SettlementId, cancellationToken);
        var entry = new FinanceCashEntry
        {
            Scope = payment.Scope,
            Direction = LedgerDirection.Payable,
            CashType = LedgerCashType.Payment,
            LegalEntityId = payment.LegalEntityId,
            BusinessPartnerId = payment.BusinessPartnerId,
            CounterLegalEntityId = payment.CounterLegalEntityId,
            ProjectId = payment.ProjectId ?? settlement.ProjectId,
            ContractId = payment.ContractId ?? settlement.ContractId,
            AccountId = request.AccountId,
            IsReversal = true,
            ReversesCashEntryId = payment.Id,
            BusinessDate = request.EntryDate,
            Amount = request.Amount,
            Notes = reason,
            SourceType = LedgerSourceType.CentralLedger
        };
        entry.Allocations.Add(new FinanceCashAllocation
        {
            CashEntry = entry,
            Settlement = settlement,
            ProjectId = settlement.ProjectId,
            ContractId = settlement.ContractId,
            ContractLineItemId = settlement.ContractLineItemId,
            BusinessPartnerId = settlement.BusinessPartnerId,
            Amount = request.Amount,
            AllocationOrder = 1
        });
        db.FinanceCashEntries.Add(entry);
        db.AccountTransactions.Add(CreateTransaction(request.AccountId, AccountTransactionDirection.Inflow, AccountTransactionSourceType.PaymentReversal, entry.Id, request.EntryDate, request.Amount, reason));
        if (settlement.ProjectId.HasValue) AddProjectAudit("RecordPaymentReversal", nameof(FinanceCashEntry), entry.Id, settlement.ProjectId.Value, $"付款冲销 {entry.Amount:N2}", new { entry.BusinessDate, entry.Amount, request.AdjustmentType, Reason = reason });
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<Guid> TransferAsync(CreateAccountTransferRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        if (request.FromAccountId == request.ToAccountId)
        {
            throw new ArgumentException("转出账户和转入账户不能相同。", nameof(request));
        }

        var accounts = await db.FinancialAccounts
            .Where(item => (item.Id == request.FromAccountId || item.Id == request.ToAccountId) && item.IsActive)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (accounts.Count != 2)
        {
            throw new InvalidOperationException("转出或转入账户不存在或已停用。");
        }

        var transfer = new AccountTransfer
        {
            FromAccountId = request.FromAccountId,
            ToAccountId = request.ToAccountId,
            TransferDate = request.TransferDate,
            Amount = request.Amount,
            Description = NormalizeOptional(request.Description)
        };
        var outTransaction = CreateTransaction(request.FromAccountId, AccountTransactionDirection.Outflow, AccountTransactionSourceType.TransferOut, transfer.Id, request.TransferDate, request.Amount, transfer.Description);
        var inTransaction = CreateTransaction(request.ToAccountId, AccountTransactionDirection.Inflow, AccountTransactionSourceType.TransferIn, transfer.Id, request.TransferDate, request.Amount, transfer.Description);
        transfer.OutTransactionId = outTransaction.Id;
        transfer.InTransactionId = inTransaction.Id;
        db.AccountTransfers.Add(transfer);
        db.AccountTransactions.AddRange(outTransaction, inTransaction);
        await db.SaveChangesAsync(cancellationToken);
        return transfer.Id;
    }

    public async Task<Guid> AddInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.GrossAmount);
        var invoiceNumber = NormalizeRequired(request.InvoiceNumber, nameof(request.InvoiceNumber));
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, false, cancellationToken);
        var taxConfiguration = await GetTaxConfigurationAsync(request.ProjectId, request.ProjectTaxConfigurationId, cancellationToken);
        InvoiceAmountValidator.Validate(request.NetAmount, request.TaxAmount, request.GrossAmount, taxConfiguration.TaxRate);
        var ledgerDirection = request.Direction == InvoiceDirection.Output ? LedgerDirection.Receivable : LedgerDirection.Payable;
        if (await db.FinanceInvoices.AnyAsync(item =>
                item.LegalEntityId == request.LegalEntityId &&
                item.Direction == ledgerDirection &&
                item.InvoiceNumber == invoiceNumber,
                cancellationToken))
        {
            throw new InvalidOperationException($"发票号码已存在：{invoiceNumber}");
        }

        ValidateAllocations(request.ReceivableAllocations, request.GrossAmount);
        ValidateAllocations(request.LineItemAllocations, request.GrossAmount);
        var receivableIds = request.ReceivableAllocations.Select(item => item.TargetId).Distinct().ToArray();
        if (receivableIds.Length > 0 && await db.FinanceSettlements.CountAsync(item =>
                receivableIds.Contains(item.Id) &&
                item.ProjectId == request.ProjectId &&
                item.ContractId == request.ContractId &&
                item.SourceType == LedgerSourceType.ProjectQuantity &&
                item.LegalEntityId == request.LegalEntityId &&
                item.BusinessPartnerId == request.BusinessPartnerId &&
                item.Direction == ledgerDirection &&
                item.Status == LedgerRecordStatus.Active,
                cancellationToken) != receivableIds.Length)
        {
            throw new InvalidOperationException("发票关联的应收记录必须是当前项目、当前合同对应的有效工程量应收。");
        }

        var lineItemIds = request.LineItemAllocations.Select(item => item.TargetId).Distinct().ToArray();
        if (lineItemIds.Length > 0 && await db.ContractLineItems.CountAsync(item =>
                lineItemIds.Contains(item.Id) &&
                item.Contract.ProjectId == request.ProjectId &&
                (!request.ContractId.HasValue || item.ContractId == request.ContractId),
                cancellationToken) != lineItemIds.Length)
        {
            throw new InvalidOperationException("发票关联的合同清单项不存在或不属于当前项目/合同。");
        }

        var allocations = request.ReceivableAllocations
            .Select((item, index) => new FinanceAllocationRequest(item.TargetId, item.AllocatedAmount, index + 1))
            .ToArray();
        if (ledgerDirection == LedgerDirection.Receivable && allocations.Length == 0)
        {
            allocations = (await BuildProjectInvoiceAllocationsAsync(
                request.ProjectId,
                request.ContractId,
                request.LegalEntityId,
                request.BusinessPartnerId,
                request.GrossAmount,
                null,
                cancellationToken)).ToArray();
        }
        var command = new CentralLedgerCommandService(db);
        var invoiceId = await command.CreateInvoiceAsync(
            CreateCompatibilityActor(request.LegalEntityId, request.ProjectId),
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                ledgerDirection,
                LedgerSourceType.CentralLedger,
                null,
                request.LegalEntityId,
                request.BusinessPartnerId,
                null,
                invoiceNumber,
                request.InvoiceDate,
                request.GrossAmount,
                request.NetAmount,
                request.TaxAmount,
                taxConfiguration.TaxRate,
                NormalizeOptional(request.Notes),
                allocations,
                AutoAllocate: ledgerDirection == LedgerDirection.Payable && allocations.Length == 0,
                ProjectTaxConfigurationId: taxConfiguration.Id,
                InvoiceType: InvoiceTypeLabel(taxConfiguration.InvoiceType),
                Status: request.Status == InvoiceStatus.Voided ? LedgerRecordStatus.Voided : LedgerRecordStatus.Active,
                ProjectId: request.ProjectId,
                ContractId: request.ContractId),
            cancellationToken);
        AddProjectAudit("CreateInvoice", nameof(FinanceInvoice), invoiceId, request.ProjectId, $"登记发票 {invoiceNumber}，金额 {request.GrossAmount:N2}", new { request.InvoiceDate, InvoiceNumber = invoiceNumber, request.Direction, taxConfiguration.TaxRate, request.NetAmount, request.TaxAmount, request.GrossAmount, request.Status });
        await db.SaveChangesAsync(cancellationToken);
        return invoiceId;
    }

    public async Task UpdateReceivableAsync(FinanceRecordActor actor, UpdateReceivableRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, false, cancellationToken);
        var entry = await db.FinanceSettlements.SingleOrDefaultAsync(item => item.Id == request.Id && item.Direction == LedgerDirection.Receivable, cancellationToken)
            ?? throw new InvalidOperationException("应收记录不存在。");
        EnsureCurrent(entry.ConcurrencyStamp, request.ConcurrencyStamp, "应收记录");
        var before = CentralSettlementSnapshot(entry);
        entry.ProjectId = request.ProjectId;
        entry.ContractId = request.ContractId;
        entry.LegalEntityId = request.LegalEntityId;
        entry.BusinessPartnerId = request.BusinessPartnerId;
        entry.BusinessDate = request.EntryDate;
        entry.DueDate = request.DueDate;
        entry.SettlementDate = request.EntryDate;
        entry.OriginalAmount = request.Amount;
        entry.OriginalInvoiceAmount = request.Amount;
        entry.Notes = NormalizeOptional(request.Description);
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.ConcurrencyStamp = Guid.NewGuid();
        AddUpdateAudit(actor, "UpdateReceivable", nameof(FinanceSettlement), entry.Id, request.ProjectId, reason, before, CentralSettlementSnapshot(entry));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCollectionAsync(FinanceRecordActor actor, UpdateCollectionRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, false, cancellationToken);
        await ValidateAccountAsync(request.AccountId, request.LegalEntityId, cancellationToken);
        if (request.ReceivableEntryId.HasValue && !await db.FinanceSettlements.AnyAsync(item => item.Id == request.ReceivableEntryId && item.ProjectId == request.ProjectId && item.Direction == LedgerDirection.Receivable && item.Status == LedgerRecordStatus.Active, cancellationToken))
            throw new InvalidOperationException("应收记录不存在、已作废或不属于当前项目。");
        var entry = await db.FinanceCashEntries.Include(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.Id == request.Id && item.CashType == LedgerCashType.Collection && !item.IsReversal, cancellationToken)
            ?? throw new InvalidOperationException("收款记录不存在。");
        EnsureCashEntryBelongsToProject(entry, request.ProjectId, "收款记录");
        EnsureCurrent(entry.ConcurrencyStamp, request.ConcurrencyStamp, "收款记录");
        var before = CentralCashSnapshot(entry);
        entry.LegalEntityId = request.LegalEntityId;
        entry.BusinessPartnerId = request.BusinessPartnerId;
        entry.AccountId = request.AccountId;
        entry.ProjectId = request.ProjectId;
        entry.ContractId = request.ContractId;
        entry.BusinessDate = request.CollectionDate;
        entry.Amount = request.Amount;
        entry.PaymentMethod = NormalizeOptional(request.PaymentMethod);
        entry.SourceType = LedgerSourceType.ProjectCollection;
        entry.SourceId = request.ProjectId;
        entry.Notes = NormalizeOptional(request.Notes);
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.ConcurrencyStamp = Guid.NewGuid();
        var cash = await db.AccountTransactions.SingleOrDefaultAsync(item => item.SourceType == AccountTransactionSourceType.Collection && item.SourceId == entry.Id, cancellationToken)
            ?? throw new InvalidOperationException("收款对应的账户流水不存在。");
        cash.AccountId = entry.AccountId!.Value;
        cash.TransactionDate = entry.BusinessDate;
        cash.Amount = entry.Amount;
        cash.Description = entry.Notes;
        db.FinanceCashAllocations.RemoveRange(entry.Allocations);
        entry.Allocations.Clear();
        var allocations = request.ReceivableEntryId.HasValue
            ? new[] { new FinanceAllocationRequest(request.ReceivableEntryId.Value, request.Amount, 1) }
            : await BuildProjectCollectionAllocationsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, request.Amount, entry.Id, cancellationToken);
        foreach (var allocation in allocations)
        {
            var settlement = await db.FinanceSettlements.SingleAsync(item => item.Id == allocation.SettlementId, cancellationToken);
            db.FinanceCashAllocations.Add(new FinanceCashAllocation
            {
                CashEntry = entry,
                Settlement = settlement,
                ProjectId = settlement.ProjectId,
                ContractId = settlement.ContractId,
                ContractLineItemId = settlement.ContractLineItemId,
                BusinessPartnerId = settlement.BusinessPartnerId,
                Amount = allocation.Amount,
                AllocationOrder = allocation.AllocationOrder
            });
        }
        AddUpdateAudit(actor, "UpdateCollection", nameof(FinanceCashEntry), entry.Id, request.ProjectId, reason, before, CentralCashSnapshot(entry));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateInvoiceAsync(FinanceRecordActor actor, UpdateInvoiceRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.GrossAmount);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        var invoiceNumber = NormalizeRequired(request.InvoiceNumber, nameof(request.InvoiceNumber));
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, false, cancellationToken);
        var taxConfiguration = await GetTaxConfigurationAsync(request.ProjectId, request.ProjectTaxConfigurationId, cancellationToken);
        InvoiceAmountValidator.Validate(request.NetAmount, request.TaxAmount, request.GrossAmount, taxConfiguration.TaxRate);
        var ledgerDirection = request.Direction == InvoiceDirection.Output ? LedgerDirection.Receivable : LedgerDirection.Payable;
        if (await db.FinanceInvoices.AnyAsync(item => item.Id != request.Id && item.LegalEntityId == request.LegalEntityId && item.Direction == ledgerDirection && item.InvoiceNumber == invoiceNumber, cancellationToken))
            throw new InvalidOperationException($"发票号码已存在：{invoiceNumber}");
        var entry = await db.FinanceInvoices.Include(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("发票记录不存在。");
        if (entry.ProjectId.HasValue && entry.ProjectId != request.ProjectId)
            throw new InvalidOperationException("发票记录不属于当前项目。");
        EnsureAllocationsBelongToProject(entry.Allocations, request.ProjectId, "发票记录");
        EnsureCurrent(entry.ConcurrencyStamp, request.ConcurrencyStamp, "发票记录");
        var before = CentralInvoiceSnapshot(entry);
        var previousGross = entry.Amount;
        entry.LegalEntityId = request.LegalEntityId;
        entry.BusinessPartnerId = request.BusinessPartnerId;
        entry.ProjectId = request.ProjectId;
        entry.ContractId = request.ContractId;
        entry.Direction = ledgerDirection;
        entry.InvoiceNumber = invoiceNumber;
        entry.InvoiceDate = request.InvoiceDate;
        entry.ProjectTaxConfigurationId = taxConfiguration.Id;
        entry.InvoiceType = InvoiceTypeLabel(taxConfiguration.InvoiceType);
        entry.TaxRate = taxConfiguration.TaxRate;
        entry.NetAmount = request.NetAmount;
        entry.TaxAmount = request.TaxAmount;
        entry.Amount = request.GrossAmount;
        entry.Status = request.Status == InvoiceStatus.Voided ? LedgerRecordStatus.Voided : LedgerRecordStatus.Active;
        entry.Notes = NormalizeOptional(request.Notes);
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.ConcurrencyStamp = Guid.NewGuid();
        if (ledgerDirection == LedgerDirection.Receivable)
        {
            var allocations = await BuildProjectInvoiceAllocationsAsync(
                request.ProjectId,
                request.ContractId,
                request.LegalEntityId,
                request.BusinessPartnerId,
                request.GrossAmount,
                entry.Id,
                cancellationToken);
            db.FinanceInvoiceAllocations.RemoveRange(entry.Allocations);
            entry.Allocations.Clear();
            foreach (var allocation in allocations)
            {
                var settlement = await db.FinanceSettlements.SingleAsync(item => item.Id == allocation.SettlementId, cancellationToken);
                db.FinanceInvoiceAllocations.Add(new FinanceInvoiceAllocation
                {
                    Invoice = entry,
                    Settlement = settlement,
                    ProjectId = settlement.ProjectId,
                    ContractId = settlement.ContractId,
                    ContractLineItemId = settlement.ContractLineItemId,
                    BusinessPartnerId = settlement.BusinessPartnerId,
                    Amount = allocation.Amount,
                    AllocationOrder = allocation.AllocationOrder
                });
            }
        }
        else
        {
            RescaleAllocations(entry.Allocations, previousGross, request.GrossAmount, link => link.Amount, (link, amount) => link.Amount = amount);
        }
        AddUpdateAudit(actor, "UpdateInvoice", nameof(FinanceInvoice), entry.Id, request.ProjectId, reason, before, CentralInvoiceSnapshot(entry));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePayableAsync(FinanceRecordActor actor, UpdatePayableRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, true, cancellationToken);
        var entry = await db.FinanceSettlements.SingleOrDefaultAsync(item => item.Id == request.Id && item.Direction == LedgerDirection.Payable, cancellationToken)
            ?? throw new InvalidOperationException("应付记录不存在。");
        if (entry.ProjectId != request.ProjectId) throw new InvalidOperationException("应付记录不属于当前项目。");
        EnsureCurrent(entry.ConcurrencyStamp, request.ConcurrencyStamp, "应付记录");
        var before = CentralSettlementSnapshot(entry);
        entry.ProjectId = request.ProjectId;
        entry.ContractId = request.ContractId;
        entry.LegalEntityId = request.LegalEntityId;
        entry.BusinessPartnerId = request.BusinessPartnerId;
        entry.BusinessDate = request.EntryDate;
        entry.DueDate = request.DueDate;
        entry.SettlementDate = request.EntryDate;
        entry.OriginalAmount = request.Amount;
        entry.OriginalInvoiceAmount = request.Amount;
        entry.Notes = NormalizeOptional(request.Description);
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.ConcurrencyStamp = Guid.NewGuid();
        AddUpdateAudit(actor, "UpdatePayable", nameof(FinanceSettlement), entry.Id, request.ProjectId, reason, before, CentralSettlementSnapshot(entry));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePaymentAsync(FinanceRecordActor actor, UpdatePaymentRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, true, cancellationToken);
        await ValidateAccountAsync(request.AccountId, request.LegalEntityId, cancellationToken);
        if (request.PayableEntryId.HasValue && !await db.FinanceSettlements.AnyAsync(item => item.Id == request.PayableEntryId && item.ProjectId == request.ProjectId && item.Direction == LedgerDirection.Payable && item.Status == LedgerRecordStatus.Active, cancellationToken))
            throw new InvalidOperationException("应付记录不存在、已作废或不属于当前项目。");
        var entry = await db.FinanceCashEntries.Include(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.Id == request.Id && item.CashType == LedgerCashType.Payment && !item.IsReversal, cancellationToken)
            ?? throw new InvalidOperationException("付款记录不存在。");
        EnsureCashEntryBelongsToProject(entry, request.ProjectId, "付款记录");
        EnsureCurrent(entry.ConcurrencyStamp, request.ConcurrencyStamp, "付款记录");
        var before = CentralCashSnapshot(entry);
        entry.LegalEntityId = request.LegalEntityId;
        entry.BusinessPartnerId = request.BusinessPartnerId;
        entry.ProjectId = request.ProjectId;
        entry.ContractId = request.ContractId;
        entry.AccountId = request.AccountId;
        entry.BusinessDate = request.PaymentDate;
        entry.Amount = request.Amount;
        entry.PaymentMethod = request.PaymentMethod.ToString();
        entry.Notes = NormalizeOptional(request.Notes);
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.ConcurrencyStamp = Guid.NewGuid();
        var cash = await db.AccountTransactions.SingleOrDefaultAsync(item => item.SourceType == AccountTransactionSourceType.Payment && item.SourceId == entry.Id, cancellationToken)
            ?? throw new InvalidOperationException("付款对应的账户流水不存在。");
        cash.AccountId = entry.AccountId!.Value;
        cash.TransactionDate = entry.BusinessDate;
        cash.Amount = entry.Amount;
        cash.Description = entry.Notes;
        db.FinanceCashAllocations.RemoveRange(entry.Allocations);
        entry.Allocations.Clear();
        if (request.PayableEntryId.HasValue)
        {
            var settlement = await db.FinanceSettlements.SingleAsync(item => item.Id == request.PayableEntryId, cancellationToken);
            db.FinanceCashAllocations.Add(new FinanceCashAllocation
            {
                CashEntry = entry,
                Settlement = settlement,
                ProjectId = settlement.ProjectId,
                ContractId = settlement.ContractId,
                ContractLineItemId = settlement.ContractLineItemId,
                BusinessPartnerId = settlement.BusinessPartnerId,
                Amount = request.Amount,
                AllocationOrder = 1
            });
        }
        AddUpdateAudit(actor, "UpdatePayment", nameof(FinanceCashEntry), entry.Id, request.ProjectId, reason, before, CentralCashSnapshot(entry));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<FinanceProjectSummaryDto> GetProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken)
        => await GetSummaryAsync(new FinanceSummaryFilter(projectId), cancellationToken);

    public async Task<FinanceProjectSummaryDto> GetSummaryAsync(FinanceSummaryFilter filter, CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(item => item.Id == filter.ProjectId, cancellationToken))
        {
            throw new InvalidOperationException("项目不存在。");
        }
        var legalEntityIds = await db.LegalEntities.AsNoTracking().Where(item => item.IsActive).Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var actor = new CentralLedgerActor(
            "finance-compatibility-adapter",
            "财务兼容适配器",
            legalEntityIds,
            new HashSet<Guid> { filter.ProjectId },
            true,
            false,
            false,
            false);
        var queryService = new CentralLedgerQueryService(db);
        var common = new CentralLedgerQuery(
            LedgerScope.External,
            EndDate: filter.CutoffDate,
            LegalEntityId: filter.LegalEntityId,
            BusinessPartnerId: filter.BusinessPartnerId,
            ProjectId: filter.ProjectId,
            ContractId: filter.ContractId,
            PageSize: 1);
        var receivable = await queryService.SearchAsync(actor, common with { Direction = LedgerDirection.Receivable }, cancellationToken);
        var payable = await queryService.SearchAsync(actor, common with { Direction = LedgerDirection.Payable }, cancellationToken);
        return new FinanceProjectSummaryDto(
            filter.ProjectId,
            receivable.Totals.GrossSettlementAmount,
            receivable.Totals.CashAmount,
            receivable.Totals.UncollectedOrUnpaid,
            payable.Totals.GrossSettlementAmount,
            payable.Totals.CashAmount,
            payable.Totals.Deductions,
            payable.Totals.UncollectedOrUnpaid,
            receivable.Totals.InvoicedAmount,
            receivable.Totals.Uninvoiced,
            payable.Totals.InvoicedAmount,
            receivable.Totals.OverSettlementCash > 0m,
            payable.Totals.OverSettlementCash > 0m);
    }

    private static void ValidateAllocations(IReadOnlyList<InvoiceAllocationRequest> allocations, decimal grossAmount)
    {
        if (allocations.Count == 0)
        {
            return;
        }

        if (allocations.Any(item => item.AllocatedAmount <= 0m))
        {
            throw new ArgumentException("发票分配金额必须大于零。", nameof(allocations));
        }

        if (allocations.Select(item => item.TargetId).Distinct().Count() != allocations.Count)
        {
            throw new ArgumentException("同一发票不能重复关联同一条记录。", nameof(allocations));
        }

        if (Math.Abs(allocations.Sum(item => item.AllocatedAmount) - grossAmount) > 0.01m)
        {
            throw new ArgumentException("发票分配金额合计必须等于含税金额。", nameof(allocations));
        }
    }

    private void AddProjectAudit(string action, string entityType, Guid entityId, Guid projectId, string reason, object after) =>
        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            RelatedProjectId = projectId.ToString(),
            Reason = reason,
            AfterJson = JsonSerializer.Serialize(after)
        });

    private void AddUpdateAudit(FinanceRecordActor actor, string action, string entityType, Guid entityId, Guid projectId, string reason, object before, object after) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = NormalizeRequired(actor.UserId, nameof(actor.UserId)),
            UserName = NormalizeOptional(actor.UserName),
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            RelatedProjectId = projectId.ToString(),
            Reason = reason,
            BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after)
        });

    private static object ReceivableSnapshot(ReceivableEntry item) => new { item.ProjectId, item.ContractId, item.LegalEntityId, item.BusinessPartnerId, item.SourceType, item.EntryDate, item.DueDate, item.Amount, item.Description, item.IsVoided, item.ConcurrencyStamp };
    private static object CollectionSnapshot(CollectionEntry item) => new { item.ReceivableEntryId, item.ProjectId, item.ContractId, item.LegalEntityId, item.BusinessPartnerId, item.AccountId, item.CollectionDate, item.Amount, item.PaymentMethod, item.Notes, item.ConcurrencyStamp };
    private static object PayableSnapshot(PayableEntry item) => new { item.ProjectId, item.ContractId, item.LegalEntityId, item.BusinessPartnerId, item.SourceType, item.EntryDate, item.DueDate, item.Amount, item.Description, item.IsVoided, item.ConcurrencyStamp };
    private static object PaymentSnapshot(PaymentEntry item) => new { item.PayableEntryId, item.ProjectId, item.ContractId, item.LegalEntityId, item.BusinessPartnerId, item.AccountId, item.PaymentDate, item.Amount, item.PaymentMethod, item.Notes, item.ConcurrencyStamp };
    private static object InvoiceSnapshot(InvoiceEntry item) => new
    {
        item.ProjectId, item.ContractId, item.LegalEntityId, item.BusinessPartnerId, item.Direction, item.InvoiceNumber,
        item.InvoiceDate, item.ProjectTaxConfigurationId, item.InvoiceType, item.TaxRate, item.NetAmount, item.TaxAmount, item.GrossAmount, item.Status, item.ConcurrencyStamp,
        ReceivableAllocations = item.ReceivableLinks.Select(link => new { link.ReceivableEntryId, link.AllocatedAmount }).ToArray(),
        LineItemAllocations = item.LineItemLinks.Select(link => new { link.ContractLineItemId, link.AllocatedAmount }).ToArray()
    };
    private static object CentralSettlementSnapshot(FinanceSettlement item) => new
    {
        item.ProjectId, item.ContractId, item.ContractLineItemId, item.LegalEntityId, item.BusinessPartnerId,
        item.Direction, item.SettlementState, item.BusinessDate, item.DueDate, item.SettlementDate, item.OriginalAmount,
        item.OriginalInvoiceAmount, item.Notes, item.Status, item.ConcurrencyStamp
    };
    private static object CentralCashSnapshot(FinanceCashEntry item) => new
    {
        item.LegalEntityId, item.BusinessPartnerId, item.CounterLegalEntityId, item.AccountId, item.CounterAccountId,
        item.ProjectId, item.ContractId,
        item.Direction, item.CashType, item.BusinessDate, item.Amount, item.PaymentMethod, item.Notes, item.Status,
        item.IsReversal, item.ReversesCashEntryId, item.ConcurrencyStamp,
        Allocations = item.Allocations.Select(link => new { link.SettlementId, link.ProjectId, link.ContractId, link.Amount, link.AllocationOrder }).ToArray()
    };
    private static object CentralInvoiceSnapshot(FinanceInvoice item) => new
    {
        item.LegalEntityId, item.BusinessPartnerId, item.CounterLegalEntityId, item.Direction, item.InvoiceNumber,
        item.InvoiceDate, item.ProjectTaxConfigurationId, item.InvoiceType, item.TaxRate, item.NetAmount, item.TaxAmount,
        item.Amount, item.Status, item.ConcurrencyStamp,
        Allocations = item.Allocations.Select(link => new { link.SettlementId, link.ProjectId, link.ContractId, link.Amount, link.AllocationOrder }).ToArray()
    };

    private static void EnsureCurrent(Guid actual, Guid expected, string label)
    {
        if (actual != expected) throw new DbUpdateConcurrencyException($"{label}已被其他用户修改，请刷新后重试。");
    }

    private static void EnsureCashEntryBelongsToProject(FinanceCashEntry entry, Guid projectId, string label)
    {
        var hasForeignAllocation = entry.Allocations.Any(item => item.ProjectId != projectId);
        if (entry.ProjectId.HasValue)
        {
            if (entry.ProjectId != projectId || hasForeignAllocation)
                throw new InvalidOperationException($"{label}不属于当前项目或包含跨项目分摊。");
            return;
        }
        if (entry.SourceType == LedgerSourceType.ProjectCollection)
        {
            if (entry.SourceId != projectId || hasForeignAllocation)
                throw new InvalidOperationException($"{label}不属于当前项目或包含跨项目分摊。");
            return;
        }

        if (entry.Allocations.Count == 0 || hasForeignAllocation)
            throw new InvalidOperationException($"{label}不属于当前项目或包含跨项目分摊。");
    }

    private static void EnsureAllocationsBelongToProject(
        ICollection<FinanceInvoiceAllocation> allocations,
        Guid projectId,
        string label)
    {
        if (allocations.Any(item => item.ProjectId != projectId))
            throw new InvalidOperationException($"{label}不属于当前项目或包含跨项目分摊。");
    }

    private static void RescaleAllocations<T>(ICollection<T> links, decimal previousGross, decimal newGross, Func<T, decimal> getAmount, Action<T, decimal> setAmount)
    {
        if (links.Count == 0) return;
        var ordered = links.ToArray();
        if (previousGross <= 0m)
        {
            setAmount(ordered[^1], newGross);
            for (var index = 0; index < ordered.Length - 1; index++) setAmount(ordered[index], 0m);
            return;
        }
        decimal allocated = 0m;
        for (var index = 0; index < ordered.Length; index++)
        {
            var amount = index == ordered.Length - 1
                ? newGross - allocated
                : Math.Round(getAmount(ordered[index]) / previousGross * newGross, 2, MidpointRounding.AwayFromZero);
            setAmount(ordered[index], amount);
            allocated += amount;
        }
    }

    private async Task ValidateDimensionsAsync(
        Guid projectId,
        Guid? contractId,
        Guid legalEntityId,
        Guid? businessPartnerId,
        bool requirePartner,
        CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(item => item.Id == projectId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("项目不存在或已停用。");
        }

        if (!await db.ProjectLegalEntities.AnyAsync(item => item.ProjectId == projectId && item.LegalEntityId == legalEntityId, cancellationToken))
        {
            throw new InvalidOperationException("签约公司未关联到当前项目。");
        }

        if (contractId.HasValue && !await db.Contracts.AnyAsync(item => item.Id == contractId && item.ProjectId == projectId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("合同不存在、已停用或不属于当前项目。");
        }

        if (requirePartner && !businessPartnerId.HasValue)
        {
            throw new ArgumentException("应付和付款必须选择合作单位。", nameof(businessPartnerId));
        }

        if (businessPartnerId.HasValue && !await db.BusinessPartners.AnyAsync(item => item.Id == businessPartnerId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("合作单位不存在或已停用。");
        }
    }

    private async Task<IReadOnlyList<FinanceAllocationRequest>> BuildProjectInvoiceAllocationsAsync(
        Guid projectId,
        Guid? contractId,
        Guid legalEntityId,
        Guid? businessPartnerId,
        decimal amount,
        Guid? excludedInvoiceId,
        CancellationToken cancellationToken)
    {
        var settlements = await db.FinanceSettlements
            .Include(item => item.Adjustments)
            .Include(item => item.Deductions)
            .Include(item => item.InvoiceAllocations).ThenInclude(item => item.Invoice)
            .Where(item => item.ProjectId == projectId &&
                (!contractId.HasValue || item.ContractId == contractId) &&
                item.SourceType == LedgerSourceType.ProjectQuantity &&
                item.Direction == LedgerDirection.Receivable &&
                item.Status == LedgerRecordStatus.Active &&
                item.LegalEntityId == legalEntityId &&
                (!businessPartnerId.HasValue || item.BusinessPartnerId == businessPartnerId))
            .OrderBy(item => item.DueDate ?? DateOnly.MaxValue)
            .ThenBy(item => item.BusinessDate)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var remaining = amount;
        var order = 1;
        var result = new List<FinanceAllocationRequest>();
        foreach (var settlement in settlements)
        {
            if (remaining <= 0m) break;
            var shouldInvoice = settlement.OriginalInvoiceAmount + settlement.Adjustments
                .Where(item => item.Status == LedgerRecordStatus.Active)
                .Sum(item => item.InvoiceAmountDelta) - settlement.Deductions
                .Where(item => item.Status == LedgerRecordStatus.Active && item.ReduceInvoiceAmount)
                .Sum(item => item.Amount);
            var allocated = settlement.InvoiceAllocations
                .Where(item => item.Invoice.Status == LedgerRecordStatus.Active && item.Invoice.Id != excludedInvoiceId)
                .Sum(item => item.Amount);
            var capacity = Math.Max(shouldInvoice - allocated, 0m);
            if (capacity <= 0m) continue;
            var allocation = Math.Min(remaining, capacity);
            result.Add(new FinanceAllocationRequest(settlement.Id, allocation, order++));
            remaining -= allocation;
        }

        return result;
    }

    private async Task<IReadOnlyList<FinanceAllocationRequest>> BuildProjectCollectionAllocationsAsync(
        Guid projectId,
        Guid? contractId,
        Guid legalEntityId,
        Guid? businessPartnerId,
        decimal amount,
        Guid? excludedCashEntryId,
        CancellationToken cancellationToken)
    {
        var settlements = await db.FinanceSettlements
            .Include(item => item.Adjustments)
            .Include(item => item.Deductions)
            .Include(item => item.CashAllocations).ThenInclude(item => item.CashEntry)
            .Where(item => item.ProjectId == projectId &&
                (!contractId.HasValue || item.ContractId == contractId) &&
                item.SourceType == LedgerSourceType.ProjectQuantity &&
                item.Direction == LedgerDirection.Receivable &&
                item.Status == LedgerRecordStatus.Active &&
                item.LegalEntityId == legalEntityId &&
                (!businessPartnerId.HasValue || item.BusinessPartnerId == businessPartnerId))
            .OrderBy(item => item.DueDate ?? DateOnly.MaxValue)
            .ThenBy(item => item.BusinessDate)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        if (settlements.Count == 0)
            throw new InvalidOperationException("当前项目没有可用于收款的工程量应收，请先完善工程量明细或签约公司。");

        var remaining = amount;
        var order = 1;
        var result = new List<FinanceAllocationRequest>();
        foreach (var settlement in settlements)
        {
            if (remaining <= 0m) break;
            var gross = settlement.OriginalAmount + settlement.Adjustments
                .Where(item => item.Status == LedgerRecordStatus.Active)
                .Sum(item => item.AmountDelta);
            var deductions = settlement.Deductions
                .Where(item => item.Status == LedgerRecordStatus.Active)
                .Sum(item => item.Amount);
            var allocated = settlement.CashAllocations
                .Where(item => item.CashEntry.Status == LedgerRecordStatus.Active && item.CashEntry.Id != excludedCashEntryId)
                .Sum(item => item.CashEntry.IsReversal ? -item.Amount : item.Amount);
            var capacity = Math.Max(gross - deductions - allocated, 0m);
            if (capacity <= 0m) continue;
            var allocation = Math.Min(remaining, capacity);
            result.Add(new FinanceAllocationRequest(settlement.Id, allocation, order++));
            remaining -= allocation;
        }

        return result;
    }

    private async Task ValidateAccountAsync(Guid accountId, Guid legalEntityId, CancellationToken cancellationToken)
    {
        if (!await db.FinancialAccounts.AnyAsync(item => item.Id == accountId && item.LegalEntityId == legalEntityId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("资金账户不存在、已停用或不属于当前签约公司。");
        }
    }

    private async Task<ProjectTaxConfiguration> GetTaxConfigurationAsync(Guid projectId, Guid configurationId, CancellationToken cancellationToken)
    {
        return await db.ProjectTaxConfigurations.AsNoTracking().SingleOrDefaultAsync(item =>
                item.Id == configurationId && item.ProjectId == projectId && item.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException("项目税金配置不存在、已停用或不属于当前项目。");
    }

    private static string InvoiceTypeLabel(ProjectInvoiceType invoiceType) => invoiceType switch
    {
        ProjectInvoiceType.Ordinary => "普票",
        ProjectInvoiceType.Special => "专票",
        _ => throw new ArgumentOutOfRangeException(nameof(invoiceType), invoiceType, "发票类型无效。")
    };

    private static AccountTransaction CreateTransaction(
        Guid accountId,
        AccountTransactionDirection direction,
        AccountTransactionSourceType sourceType,
        Guid sourceId,
        DateOnly transactionDate,
        decimal amount,
        string? description) =>
        new()
        {
            AccountId = accountId,
            Direction = direction,
            SourceType = sourceType,
            SourceId = sourceId,
            TransactionDate = transactionDate,
            Amount = amount,
            Description = description
        };

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "金额必须大于零。");
        }
    }

    private static CentralLedgerActor CreateCompatibilityActor(Guid legalEntityId, Guid projectId) => new(
        "finance-compatibility-adapter",
        "财务兼容适配器",
        new HashSet<Guid> { legalEntityId },
        new HashSet<Guid> { projectId },
        true,
        false,
        false,
        false);

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空。", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IEnumerable<ProjectFinanceListItemDto> SortOverview(
        IEnumerable<ProjectFinanceListItemDto> items,
        string? sortKey,
        bool descending)
    {
        Func<ProjectFinanceListItemDto, object> selector = sortKey switch
        {
            "ProjectName" => item => item.ProjectName,
            "ReceivableAmount" => item => item.Summary.ReceivableAmount,
            "CollectedAmount" => item => item.Summary.CollectedAmount,
            "UncollectedAmount" => item => item.Summary.UncollectedAmount,
            "PayableAmount" => item => item.Summary.PayableAmount,
            "PaidAmount" => item => item.Summary.PaidAmount,
            "UnpaidAmount" => item => item.Summary.UnpaidAmount,
            "OutputInvoiceAmount" => item => item.Summary.OutputInvoiceAmount,
            "UninvoicedAmount" => item => item.Summary.UninvoicedAmount,
            _ => item => item.ProjectNumber
        };
        return descending ? items.OrderByDescending(selector).ThenBy(item => item.ProjectNumber) : items.OrderBy(selector).ThenBy(item => item.ProjectNumber);
    }

    private static FinanceProjectSummaryDto SumOverview(IReadOnlyCollection<FinanceProjectSummaryDto> summaries) =>
        new(
            Guid.Empty,
            summaries.Sum(item => item.ReceivableAmount),
            summaries.Sum(item => item.CollectedAmount),
            summaries.Sum(item => item.UncollectedAmount),
            summaries.Sum(item => item.PayableAmount),
            summaries.Sum(item => item.PaidAmount),
            summaries.Sum(item => item.DeductionAmount),
            summaries.Sum(item => item.UnpaidAmount),
            summaries.Sum(item => item.OutputInvoiceAmount),
            summaries.Sum(item => item.UninvoicedAmount),
            summaries.Sum(item => item.InputInvoiceAmount),
            summaries.Any(item => item.HasCollectionRisk),
            summaries.Any(item => item.HasPaymentRisk));
}
