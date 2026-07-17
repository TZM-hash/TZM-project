using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
            OpeningBalance = request.OpeningBalance
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
                account.IsActive);
        }).ToArray();
    }

    public async Task<IReadOnlyList<ProjectFinanceListItemDto>> ListProjectSummariesAsync(CancellationToken cancellationToken)
    {
        var projects = await db.Projects
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.ProjectNumber)
            .Select(item => new { item.Id, item.ProjectNumber, item.Name })
            .ToListAsync(cancellationToken);
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
        return new FinanceEntryOptionsDto(projects, contracts, legalEntities, partners, accounts, receivables, payables, collections, payments);
    }

    public async Task<Guid> AddReceivableAsync(CreateReceivableRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, false, cancellationToken);
        var entry = new ReceivableEntry
        {
            ProjectId = request.ProjectId,
            ContractId = request.ContractId,
            LegalEntityId = request.LegalEntityId,
            BusinessPartnerId = request.BusinessPartnerId,
            SourceType = request.SourceType,
            EntryDate = request.EntryDate,
            DueDate = request.DueDate,
            Amount = request.Amount,
            Description = NormalizeOptional(request.Description)
        };
        db.ReceivableEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<Guid> RecordCollectionAsync(RecordCollectionRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, false, cancellationToken);
        await ValidateAccountAsync(request.AccountId, request.LegalEntityId, cancellationToken);
        if (request.ReceivableEntryId.HasValue && !await db.ReceivableEntries.AnyAsync(
                item => item.Id == request.ReceivableEntryId && item.ProjectId == request.ProjectId && !item.IsVoided,
                cancellationToken))
        {
            throw new InvalidOperationException("应收记录不存在、已作废或不属于当前项目。");
        }

        var entry = new CollectionEntry
        {
            ReceivableEntryId = request.ReceivableEntryId,
            ProjectId = request.ProjectId,
            ContractId = request.ContractId,
            LegalEntityId = request.LegalEntityId,
            BusinessPartnerId = request.BusinessPartnerId,
            AccountId = request.AccountId,
            CollectionDate = request.CollectionDate,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Notes = NormalizeOptional(request.Notes)
        };
        db.CollectionEntries.Add(entry);
        db.AccountTransactions.Add(CreateTransaction(entry.AccountId, AccountTransactionDirection.Inflow, AccountTransactionSourceType.Collection, entry.Id, entry.CollectionDate, entry.Amount, entry.Notes));
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<Guid> RecordRefundAsync(RecordRefundRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        CollectionEntry? collection = null;
        if (request.CollectionEntryId.HasValue)
        {
            collection = await db.CollectionEntries.SingleOrDefaultAsync(item => item.Id == request.CollectionEntryId, cancellationToken)
                ?? throw new InvalidOperationException("原收款记录不存在。");
            if (collection.AccountId != request.AccountId)
            {
                throw new InvalidOperationException("退款或收款冲销必须使用原收款账户。");
            }
        }

        if (request.ReceivableEntryId.HasValue && !await db.ReceivableEntries.AnyAsync(item => item.Id == request.ReceivableEntryId, cancellationToken))
        {
            throw new InvalidOperationException("关联应收记录不存在。");
        }

        if (collection is null && !request.ReceivableEntryId.HasValue)
        {
            throw new ArgumentException("退款或收款冲销至少需要关联原收款或应收记录。", nameof(request));
        }

        if (!await db.FinancialAccounts.AnyAsync(item => item.Id == request.AccountId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("资金账户不存在或已停用。");
        }

        var entry = new RefundOrReversalEntry
        {
            CollectionEntryId = request.CollectionEntryId,
            ReceivableEntryId = request.ReceivableEntryId,
            AccountId = request.AccountId,
            EntryDate = request.EntryDate,
            Amount = request.Amount,
            AdjustmentType = request.AdjustmentType,
            Reason = reason
        };
        db.RefundOrReversalEntries.Add(entry);
        db.AccountTransactions.Add(CreateTransaction(entry.AccountId, AccountTransactionDirection.Outflow, AccountTransactionSourceType.Refund, entry.Id, entry.EntryDate, entry.Amount, entry.Reason));
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<Guid> AddPayableAsync(CreatePayableRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, true, cancellationToken);
        var entry = new PayableEntry
        {
            ProjectId = request.ProjectId,
            ContractId = request.ContractId,
            LegalEntityId = request.LegalEntityId,
            BusinessPartnerId = request.BusinessPartnerId,
            SourceType = request.SourceType,
            EntryDate = request.EntryDate,
            DueDate = request.DueDate,
            Amount = request.Amount,
            Description = NormalizeOptional(request.Description)
        };
        db.PayableEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<Guid> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, true, cancellationToken);
        await ValidateAccountAsync(request.AccountId, request.LegalEntityId, cancellationToken);
        if (request.PayableEntryId.HasValue && !await db.PayableEntries.AnyAsync(
                item => item.Id == request.PayableEntryId && item.ProjectId == request.ProjectId && !item.IsVoided,
                cancellationToken))
        {
            throw new InvalidOperationException("应付记录不存在、已作废或不属于当前项目。");
        }

        var entry = new PaymentEntry
        {
            PayableEntryId = request.PayableEntryId,
            ProjectId = request.ProjectId,
            ContractId = request.ContractId,
            LegalEntityId = request.LegalEntityId,
            BusinessPartnerId = request.BusinessPartnerId,
            AccountId = request.AccountId,
            PaymentDate = request.PaymentDate,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Notes = NormalizeOptional(request.Notes)
        };
        db.PaymentEntries.Add(entry);
        db.AccountTransactions.Add(CreateTransaction(entry.AccountId, AccountTransactionDirection.Outflow, AccountTransactionSourceType.Payment, entry.Id, entry.PaymentDate, entry.Amount, entry.Notes));
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<Guid> AddDeductionAsync(CreateDeductionRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        if (!await db.PayableEntries.AnyAsync(item =>
                item.Id == request.PayableEntryId &&
                item.ProjectId == request.ProjectId &&
                item.LegalEntityId == request.LegalEntityId &&
                item.BusinessPartnerId == request.BusinessPartnerId &&
                !item.IsVoided,
                cancellationToken))
        {
            throw new InvalidOperationException("应付记录不存在、已作废或与扣款维度不一致。");
        }

        var entry = new DeductionEntry
        {
            PayableEntryId = request.PayableEntryId,
            ProjectId = request.ProjectId,
            LegalEntityId = request.LegalEntityId,
            BusinessPartnerId = request.BusinessPartnerId,
            EntryDate = request.EntryDate,
            Amount = request.Amount,
            Reason = reason
        };
        db.DeductionEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<Guid> RecordPaymentReversalAsync(RecordPaymentReversalRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        var payment = await db.PaymentEntries.SingleOrDefaultAsync(item => item.Id == request.PaymentEntryId, cancellationToken)
            ?? throw new InvalidOperationException("原付款记录不存在。");
        if (payment.AccountId != request.AccountId)
        {
            throw new InvalidOperationException("付款冲销必须使用原付款账户。");
        }

        var entry = new PaymentReversalEntry
        {
            PaymentEntryId = request.PaymentEntryId,
            AccountId = request.AccountId,
            EntryDate = request.EntryDate,
            Amount = request.Amount,
            AdjustmentType = request.AdjustmentType,
            Reason = reason
        };
        db.PaymentReversalEntries.Add(entry);
        db.AccountTransactions.Add(CreateTransaction(entry.AccountId, AccountTransactionDirection.Inflow, AccountTransactionSourceType.PaymentReversal, entry.Id, entry.EntryDate, entry.Amount, entry.Reason));
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
        InvoiceAmountValidator.Validate(request.NetAmount, request.TaxAmount, request.GrossAmount, request.TaxRate);
        var invoiceNumber = NormalizeRequired(request.InvoiceNumber, nameof(request.InvoiceNumber));
        await ValidateDimensionsAsync(request.ProjectId, request.ContractId, request.LegalEntityId, request.BusinessPartnerId, false, cancellationToken);
        if (await db.InvoiceEntries.AnyAsync(item =>
                item.LegalEntityId == request.LegalEntityId &&
                item.Direction == request.Direction &&
                item.InvoiceNumber == invoiceNumber,
                cancellationToken))
        {
            throw new InvalidOperationException($"发票号码已存在：{invoiceNumber}");
        }

        ValidateAllocations(request.ReceivableAllocations, request.GrossAmount);
        ValidateAllocations(request.LineItemAllocations, request.GrossAmount);
        var receivableIds = request.ReceivableAllocations.Select(item => item.TargetId).Distinct().ToArray();
        if (receivableIds.Length > 0 && await db.ReceivableEntries.CountAsync(item =>
                receivableIds.Contains(item.Id) &&
                item.ProjectId == request.ProjectId &&
                !item.IsVoided,
                cancellationToken) != receivableIds.Length)
        {
            throw new InvalidOperationException("发票关联的应收记录不存在、已作废或不属于当前项目。");
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

        var invoice = new InvoiceEntry
        {
            ProjectId = request.ProjectId,
            ContractId = request.ContractId,
            LegalEntityId = request.LegalEntityId,
            BusinessPartnerId = request.BusinessPartnerId,
            Direction = request.Direction,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = request.InvoiceDate,
            InvoiceType = NormalizeOptional(request.InvoiceType),
            TaxRate = request.TaxRate,
            NetAmount = request.NetAmount,
            TaxAmount = request.TaxAmount,
            GrossAmount = request.GrossAmount,
            Status = request.Status
        };
        foreach (var allocation in request.ReceivableAllocations)
        {
            invoice.ReceivableLinks.Add(new InvoiceReceivableLink
            {
                Invoice = invoice,
                ReceivableEntryId = allocation.TargetId,
                AllocatedAmount = allocation.AllocatedAmount
            });
        }

        foreach (var allocation in request.LineItemAllocations)
        {
            invoice.LineItemLinks.Add(new InvoiceLineItemLink
            {
                Invoice = invoice,
                ContractLineItemId = allocation.TargetId,
                AllocatedAmount = allocation.AllocatedAmount
            });
        }

        db.InvoiceEntries.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        return invoice.Id;
    }

    public async Task<FinanceProjectSummaryDto> GetProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken)
        => await GetSummaryAsync(new FinanceSummaryFilter(projectId), cancellationToken);

    public async Task<FinanceProjectSummaryDto> GetSummaryAsync(FinanceSummaryFilter filter, CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(item => item.Id == filter.ProjectId, cancellationToken))
        {
            throw new InvalidOperationException("项目不存在。");
        }

        var receivables = db.ReceivableEntries.Where(item => item.ProjectId == filter.ProjectId && !item.IsVoided);
        var collections = db.CollectionEntries.Where(item => item.ProjectId == filter.ProjectId);
        var payables = db.PayableEntries.Where(item => item.ProjectId == filter.ProjectId && !item.IsVoided);
        var payments = db.PaymentEntries.Where(item => item.ProjectId == filter.ProjectId);
        var deductions = db.DeductionEntries.Where(item => item.ProjectId == filter.ProjectId);
        var invoices = db.InvoiceEntries.Where(item => item.ProjectId == filter.ProjectId && item.Status != InvoiceStatus.Voided);
        var refunds = db.RefundOrReversalEntries.AsQueryable();
        var reversals = db.PaymentReversalEntries.AsQueryable();
        if (filter.CutoffDate.HasValue)
        {
            receivables = receivables.Where(item => item.EntryDate <= filter.CutoffDate);
            collections = collections.Where(item => item.CollectionDate <= filter.CutoffDate);
            payables = payables.Where(item => item.EntryDate <= filter.CutoffDate);
            payments = payments.Where(item => item.PaymentDate <= filter.CutoffDate);
            deductions = deductions.Where(item => item.EntryDate <= filter.CutoffDate);
            invoices = invoices.Where(item => item.InvoiceDate <= filter.CutoffDate);
            refunds = refunds.Where(item => item.EntryDate <= filter.CutoffDate);
            reversals = reversals.Where(item => item.EntryDate <= filter.CutoffDate);
        }
        if (filter.ContractId.HasValue)
        {
            receivables = receivables.Where(item => item.ContractId == filter.ContractId);
            collections = collections.Where(item => item.ContractId == filter.ContractId);
            payables = payables.Where(item => item.ContractId == filter.ContractId);
            payments = payments.Where(item => item.ContractId == filter.ContractId);
            deductions = deductions.Where(item => item.Payable.ContractId == filter.ContractId);
            invoices = invoices.Where(item => item.ContractId == filter.ContractId);
        }

        if (filter.LegalEntityId.HasValue)
        {
            receivables = receivables.Where(item => item.LegalEntityId == filter.LegalEntityId);
            collections = collections.Where(item => item.LegalEntityId == filter.LegalEntityId);
            payables = payables.Where(item => item.LegalEntityId == filter.LegalEntityId);
            payments = payments.Where(item => item.LegalEntityId == filter.LegalEntityId);
            deductions = deductions.Where(item => item.LegalEntityId == filter.LegalEntityId);
            invoices = invoices.Where(item => item.LegalEntityId == filter.LegalEntityId);
        }

        if (filter.BusinessPartnerId.HasValue)
        {
            receivables = receivables.Where(item => item.BusinessPartnerId == filter.BusinessPartnerId);
            collections = collections.Where(item => item.BusinessPartnerId == filter.BusinessPartnerId);
            payables = payables.Where(item => item.BusinessPartnerId == filter.BusinessPartnerId);
            payments = payments.Where(item => item.BusinessPartnerId == filter.BusinessPartnerId);
            deductions = deductions.Where(item => item.BusinessPartnerId == filter.BusinessPartnerId);
            invoices = invoices.Where(item => item.BusinessPartnerId == filter.BusinessPartnerId);
        }

        var receivableIds = receivables.Select(item => item.Id);
        var collectionIds = collections.Select(item => item.Id);
        var paymentIds = payments.Select(item => item.Id);
        var receivableAmount = await receivables.SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var collectionAmount = await collections.SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var refundAmount = await refunds.Where(item =>
                (item.ReceivableEntryId.HasValue && receivableIds.Contains(item.ReceivableEntryId.Value)) ||
                (item.CollectionEntryId.HasValue && collectionIds.Contains(item.CollectionEntryId.Value)))
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var payableAmount = await payables.SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var paymentAmount = await payments.SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var reversalAmount = await reversals.Where(item => paymentIds.Contains(item.PaymentEntryId)).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var deductionAmount = await deductions.SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var outputInvoiceAmount = await invoices.Where(item => item.Direction == InvoiceDirection.Output).SumAsync(item => (decimal?)item.GrossAmount, cancellationToken) ?? 0m;
        var inputInvoiceAmount = await invoices.Where(item => item.Direction == InvoiceDirection.Input).SumAsync(item => (decimal?)item.GrossAmount, cancellationToken) ?? 0m;
        var ledger = LedgerCalculator.Calculate(receivableAmount, collectionAmount, refundAmount, payableAmount, paymentAmount, reversalAmount, deductionAmount);
        return new FinanceProjectSummaryDto(
            filter.ProjectId,
            ledger.ReceivableAmount,
            ledger.CollectedAmount,
            ledger.UncollectedAmount,
            ledger.PayableAmount,
            ledger.PaidAmount,
            ledger.DeductionAmount,
            ledger.UnpaidAmount,
            outputInvoiceAmount,
            ledger.ReceivableAmount - outputInvoiceAmount,
            inputInvoiceAmount,
            ledger.HasCollectionRisk,
            ledger.HasPaymentRisk);
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

    private async Task ValidateAccountAsync(Guid accountId, Guid legalEntityId, CancellationToken cancellationToken)
    {
        if (!await db.FinancialAccounts.AnyAsync(item => item.Id == accountId && item.LegalEntityId == legalEntityId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("资金账户不存在、已停用或不属于当前签约公司。");
        }
    }

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
