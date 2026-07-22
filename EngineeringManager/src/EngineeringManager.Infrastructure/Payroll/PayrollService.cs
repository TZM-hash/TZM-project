using System.Text.Json;
using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Payroll;

public sealed class PayrollService(ApplicationDbContext db) : IPayrollService
{
    public async Task<PayrollDisbursementBatchDetailsDto> SaveDisbursementBatchAsync(
        string userId,
        SavePayrollDisbursementBatchRequest request,
        CancellationToken cancellationToken)
    {
        var number = NormalizeRequired(request.BatchNumber, nameof(request.BatchNumber));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        if (await db.PayrollBatches.AnyAsync(item => item.BatchNumber == number && item.Id != request.Id, cancellationToken))
        {
            throw new InvalidOperationException($"工资批次编号已存在：{number}");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = request.Id.HasValue
            ? await db.PayrollBatches
                .Include(item => item.Payments)
                .Include(item => item.CrewAllocations)
                .SingleOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("工资批次不存在。")
            : new PayrollBatch();
        if (request.Id.HasValue && request.ConcurrencyStamp != batch.ConcurrencyStamp)
        {
            throw new DbUpdateConcurrencyException("工资批次已被其他用户修改，请刷新后重试。");
        }

        await ValidateDisbursementDimensionsAsync(request, cancellationToken);
        var resolvedLines = await ResolveDisbursementLinesAsync(batch, request, cancellationToken);
        var inputs = resolvedLines.Select(item => item.Input).ToArray();
        var summary = request.Status == PayrollBatchStatus.Confirmed
            ? PayrollDisbursementRules.EnsureCanReview(request.ActualAmount, request.ProjectId, inputs)
            : PayrollDisbursementRules.Calculate(request.ActualAmount, inputs);

        var before = request.Id.HasValue ? BatchSnapshot(batch) : null;
        var effectiveDate = request.PaymentDate ?? DateOnly.FromDateTime(DateTime.Today);
        batch.BatchNumber = number;
        batch.Name = name;
        batch.BatchType = PayrollBatchType.Temporary;
        batch.StartDate = effectiveDate;
        batch.EndDate = effectiveDate;
        batch.PaymentDate = request.PaymentDate;
        batch.ProjectId = request.ProjectId;
        batch.LegalEntityId = request.LegalEntityId;
        batch.AccountId = request.AccountId;
        batch.ActualAmount = request.ActualAmount;
        batch.PaymentMethod = request.PaymentMethod;
        batch.VoucherNumber = NormalizeOptional(request.VoucherNumber);
        batch.DisbursementType = request.DisbursementType;
        batch.FundingSource = request.FundingSource;
        batch.RepaysPersonalAdvanceAccountId = request.RepaysPersonalAdvanceAccountId;
        batch.Status = request.Status;
        batch.Notes = NormalizeOptional(request.Notes);
        batch.IsUnifiedDisbursement = true;
        batch.UpdatedAt = DateTimeOffset.UtcNow;
        batch.ConcurrencyStamp = Guid.NewGuid();
        if (request.Status == PayrollBatchStatus.Confirmed)
        {
            batch.ReviewedAt = DateTimeOffset.UtcNow;
            batch.ReviewedByUserId = userId;
        }

        if (!request.Id.HasValue)
        {
            db.PayrollBatches.Add(batch);
        }

        ApplyDisbursementLines(batch, request.Lines, resolvedLines);
        await ApplyCrewAllocationsAsync(batch, request.CrewAllocations, summary, cancellationToken);
        await ApplyBatchTransactionAsync(batch, cancellationToken);
        await SyncPersonalAdvanceEffectsAsync(batch, cancellationToken);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = request.Id.HasValue ? "UpdatePayrollDisbursementBatch" : "CreatePayrollDisbursementBatch",
            EntityType = nameof(PayrollBatch),
            EntityId = batch.Id.ToString(),
            Reason = reason,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(BatchSnapshot(batch))
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetDisbursementBatchAsync(batch.Id, cancellationToken))!;
    }

    public async Task<PayrollDisbursementBatchDetailsDto?> GetDisbursementBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await db.PayrollBatches.AsNoTracking()
            .Include(item => item.Payments)
            .Include(item => item.CrewAllocations)
            .SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken);
        if (batch is null) return null;
        var lines = batch.Payments.Select(ToDisbursementLineDto).OrderBy(item => item.RecipientType).ThenBy(item => item.RecipientNameSnapshot).ToArray();
        var summary = PayrollDisbursementRules.Calculate(batch.ActualAmount, batch.Payments.Select(ToDisbursementSummaryInput));
        return new PayrollDisbursementBatchDetailsDto(
            ToDisbursementBatchDto(batch),
            summary,
            lines,
            batch.CrewAllocations.Select(item => new PayrollCrewAllocationDto(item.Id, item.CrewBusinessPartnerId, item.ContractId, item.PayableEntryId, item.Notes, item.ConcurrencyStamp)).ToArray());
    }

    public Task<PayrollDisbursementOverviewDto> GetDisbursementOverviewAsync(CancellationToken cancellationToken) =>
        SearchDisbursementOverviewAsync(null, false, cancellationToken);

    public async Task<PayrollDisbursementOverviewDto> SearchDisbursementOverviewAsync(string? search, bool canViewSensitiveData, CancellationToken cancellationToken)
    {
        var query = db.PayrollBatches.AsNoTracking()
            .Where(item => item.IsUnifiedDisbursement && item.Status != PayrollBatchStatus.Voided);
        foreach (var term in SearchTerms.Parse(search))
        {
            var hasDate = SearchTerms.TryParseDate(term, out var date);
            var hasAmount = SearchTerms.TryParseDecimal(term, out var amount);
            query = query.Where(item =>
                item.BatchNumber.Contains(term)
                || item.Name.Contains(term)
                || (item.StageOrMilestoneName != null && item.StageOrMilestoneName.Contains(term))
                || (item.VoucherNumber != null && item.VoucherNumber.Contains(term))
                || (item.Notes != null && item.Notes.Contains(term))
                || (item.Project != null && (item.Project.ProjectNumber.Contains(term) || item.Project.Name.Contains(term) || (item.Project.Notes != null && item.Project.Notes.Contains(term))))
                || (item.LegalEntity != null && (item.LegalEntity.Code.Contains(term) || item.LegalEntity.Name.Contains(term) || item.LegalEntity.ShortName.Contains(term)))
                || (item.Account != null && (item.Account.AccountName.Contains(term) || (item.Account.BankName != null && item.Account.BankName.Contains(term)) || (item.Account.Notes != null && item.Account.Notes.Contains(term)) || (canViewSensitiveData && item.Account.AccountNumber != null && item.Account.AccountNumber.Contains(term))))
                || (hasDate && (item.StartDate == date || item.EndDate == date || item.PaymentDate == date))
                || (hasAmount && item.ActualAmount == amount)
                || item.Payments.Any(payment =>
                    (payment.PayeeName != null && payment.PayeeName.Contains(term))
                    || (payment.RecipientNameSnapshot != null && payment.RecipientNameSnapshot.Contains(term))
                    || (payment.TradeSnapshot != null && payment.TradeSnapshot.Contains(term))
                    || (payment.Notes != null && payment.Notes.Contains(term))
                    || (payment.Employee != null && (payment.Employee.EmployeeNumber.Contains(term) || payment.Employee.Name.Contains(term) || (payment.Employee.PositionTitle != null && payment.Employee.PositionTitle.Contains(term))))
                    || (payment.ConstructionWorker != null && (payment.ConstructionWorker.Name.Contains(term) || (payment.ConstructionWorker.Trade != null && payment.ConstructionWorker.Trade.Contains(term)) || (payment.ConstructionWorker.Phone != null && payment.ConstructionWorker.Phone.Contains(term)) || (payment.ConstructionWorker.Notes != null && payment.ConstructionWorker.Notes.Contains(term))))
                    || (payment.CrewBusinessPartner != null && (payment.CrewBusinessPartner.PartnerNumber.Contains(term) || payment.CrewBusinessPartner.Name.Contains(term) || payment.CrewBusinessPartner.ShortName.Contains(term)))
                    || (canViewSensitiveData && ((payment.IdentityNumberSnapshot != null && payment.IdentityNumberSnapshot.Contains(term)) || (payment.BankAccountSnapshot != null && payment.BankAccountSnapshot.Contains(term)) || (payment.ConstructionWorker != null && payment.ConstructionWorker.IdentityNumber != null && payment.ConstructionWorker.IdentityNumber.Contains(term)))))
                || item.CrewAllocations.Any(allocation =>
                    (allocation.CrewBusinessPartner.PartnerNumber.Contains(term) || allocation.CrewBusinessPartner.Name.Contains(term) || allocation.CrewBusinessPartner.ShortName.Contains(term))
                    || (allocation.Notes != null && allocation.Notes.Contains(term))));
        }

        var ids = await query
            .OrderByDescending(item => item.PaymentDate)
            .ThenBy(item => item.BatchNumber)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var batches = new List<PayrollDisbursementBatchListItemDto>(ids.Count);
        foreach (var id in ids)
        {
            var details = (await GetDisbursementBatchAsync(id, cancellationToken))!;
            batches.Add(new PayrollDisbursementBatchListItemDto(details.Batch, details.Summary, details.Lines.Count));
        }
        return new PayrollDisbursementOverviewDto(
            batches.Sum(item => item.Batch.ActualAmount),
            batches.Sum(item => item.Summary.EmployeeAmount),
            batches.Sum(item => item.Summary.CrewAmount),
            batches.Sum(item => item.Summary.Difference),
            batches);
    }

    public async Task<PayrollBatchDto> CreateBatchAsync(CreatePayrollBatchRequest request, CancellationToken cancellationToken)
    {
        var number = NormalizeRequired(request.BatchNumber, nameof(request.BatchNumber));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        if (request.EndDate < request.StartDate)
        {
            throw new ArgumentException("工资批次结束日期不能早于开始日期。", nameof(request));
        }

        if (await db.PayrollBatches.AnyAsync(item => item.BatchNumber == number, cancellationToken))
        {
            throw new InvalidOperationException($"工资批次编号已存在：{number}");
        }

        if (request.ProjectId.HasValue && !await db.Projects.AnyAsync(item => item.Id == request.ProjectId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("工资批次项目不存在或已停用。");
        }

        if (request.LegalEntityId.HasValue && !await db.LegalEntities.AnyAsync(item => item.Id == request.LegalEntityId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("工资批次签约公司不存在或已停用。");
        }

        var batch = new PayrollBatch
        {
            BatchNumber = number,
            Name = name,
            BatchType = request.BatchType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ProjectId = request.ProjectId,
            LegalEntityId = request.LegalEntityId,
            StageOrMilestoneName = NormalizeOptional(request.StageOrMilestoneName)
        };
        db.PayrollBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(batch);
    }

    public async Task<PayrollItemDto> AddItemAsync(CreatePayrollItemRequest request, CancellationToken cancellationToken)
    {
        var batch = await db.PayrollBatches.SingleOrDefaultAsync(item => item.Id == request.PayrollBatchId, cancellationToken)
            ?? throw new InvalidOperationException("工资批次不存在。");
        if (batch.Status != PayrollBatchStatus.Draft)
        {
            throw new InvalidOperationException("只有草稿工资批次可以新增工资项目。");
        }

        if (!await db.Employees.AnyAsync(item => item.Id == request.EmployeeId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("员工不存在或已停用。");
        }

        var amount = CalculateItemAmount(request);
        await ValidateAllocationsAsync(request.CostAllocations, amount, cancellationToken);
        var item = new PayrollItem
        {
            PayrollBatchId = request.PayrollBatchId,
            EmployeeId = request.EmployeeId,
            ItemType = request.ItemType,
            Nature = request.Nature,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            Amount = amount,
            Description = NormalizeOptional(request.Description)
        };
        foreach (var allocation in request.CostAllocations)
        {
            item.CostAllocations.Add(new PayrollCostAllocation
            {
                PayrollItem = item,
                ProjectId = allocation.ProjectId,
                LegalEntityId = allocation.LegalEntityId,
                Amount = allocation.Amount
            });
        }

        db.PayrollItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task<Guid> RecordPaymentAsync(RecordPayrollPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "工资支付金额必须大于零。");
        }

        var payeeName = NormalizeRequired(request.PayeeName, nameof(request.PayeeName));
        var batch = await db.PayrollBatches.SingleOrDefaultAsync(item => item.Id == request.PayrollBatchId && item.Status != PayrollBatchStatus.Voided, cancellationToken)
            ?? throw new InvalidOperationException("工资批次不存在或已作废。");
        if (!await db.PayrollItems.AnyAsync(item => item.PayrollBatchId == request.PayrollBatchId && item.EmployeeId == request.EmployeeId, cancellationToken))
        {
            throw new InvalidOperationException("当前工资批次没有该员工的工资项目。");
        }

        if (!await db.FinancialAccounts.AnyAsync(item =>
                item.Id == request.AccountId &&
                item.IsActive &&
                (!batch.LegalEntityId.HasValue || item.LegalEntityId == batch.LegalEntityId),
                cancellationToken))
        {
            throw new InvalidOperationException("发薪账户不存在、已停用或不属于工资批次签约公司。");
        }

        if (request.PayeeBusinessPartnerId.HasValue && !await db.BusinessPartners.AnyAsync(item => item.Id == request.PayeeBusinessPartnerId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("代收合作单位不存在或已停用。");
        }

        if (request.PayeeType == PayrollPayeeType.CrewLeader && (!request.PayeeBusinessPartnerId.HasValue || !await db.BusinessPartnerRoles.AnyAsync(item =>
                item.BusinessPartnerId == request.PayeeBusinessPartnerId && item.RoleType == BusinessPartnerRoleType.ConstructionCrew,
                cancellationToken)))
        {
            throw new InvalidOperationException("班组负责人代收必须关联施工班组。");
        }

        var payment = new PayrollPayment
        {
            PayrollBatchId = request.PayrollBatchId,
            EmployeeId = request.EmployeeId,
            AccountId = request.AccountId,
            PaymentDate = request.PaymentDate,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            PayeeType = request.PayeeType,
            PayeeName = payeeName,
            PayeeBusinessPartnerId = request.PayeeBusinessPartnerId,
            Notes = NormalizeOptional(request.Notes)
        };
        var transaction = new AccountTransaction
        {
            AccountId = request.AccountId,
            Direction = AccountTransactionDirection.Outflow,
            SourceType = AccountTransactionSourceType.PayrollPayment,
            SourceId = payment.Id,
            TransactionDate = request.PaymentDate,
            Amount = request.Amount,
            Description = $"工资支付：{payeeName}"
        };
        payment.AccountTransactionId = transaction.Id;
        db.PayrollPayments.Add(payment);
        db.AccountTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);
        return payment.Id;
    }

    public async Task<PayrollBatchSummaryDto> GetBatchSummaryAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await db.PayrollBatches
            .AsNoTracking()
            .Include(item => item.Items)
                .ThenInclude(item => item.Employee)
            .Include(item => item.Payments)
            .SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken)
            ?? throw new InvalidOperationException("工资批次不存在。");
        var employeeIds = batch.Items.Select(item => item.EmployeeId)
            .Concat(batch.Payments.Where(item => item.EmployeeId.HasValue).Select(item => item.EmployeeId!.Value))
            .Distinct()
            .ToArray();
        var summaries = employeeIds.Select(employeeId =>
        {
            var employeeItems = batch.Items.Where(item => item.EmployeeId == employeeId).ToArray();
            var employee = employeeItems.Select(item => item.Employee).FirstOrDefault() ?? db.Employees.AsNoTracking().Single(item => item.Id == employeeId);
            var summary = PayrollCalculator.Calculate(
                employeeItems.Select(item => new PayrollComponentInput(item.Nature, item.Amount)),
                batch.Payments.Where(item => item.EmployeeId == employeeId).Sum(item => item.Amount));
            return new EmployeePayrollSummaryDto(
                employeeId,
                employee.EmployeeNumber,
                employee.Name,
                summary.GrossEarnings,
                summary.DeductionAmount,
                summary.PayableAmount,
                summary.PaidAmount,
                summary.UnpaidAmount,
                summary.HasOverpaymentRisk,
                summary.HasDeductionRisk);
        }).ToArray();
        return new PayrollBatchSummaryDto(
            batch.Id,
            summaries.Sum(item => item.GrossEarnings),
            summaries.Sum(item => item.DeductionAmount),
            summaries.Sum(item => item.PayableAmount),
            summaries.Sum(item => item.PaidAmount),
            summaries.Sum(item => item.UnpaidAmount),
            summaries.Any(item => item.HasOverpaymentRisk),
            summaries.Any(item => item.HasDeductionRisk),
            summaries);
    }

    public async Task<IReadOnlyList<PayrollBatchDto>> ListBatchesAsync(CancellationToken cancellationToken)
    {
        var batches = await db.PayrollBatches.AsNoTracking().OrderByDescending(item => item.EndDate).ThenBy(item => item.BatchNumber).ToListAsync(cancellationToken);
        return batches.Select(ToDto).ToArray();
    }

    public async Task<PayrollOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var batches = await ListBatchesAsync(cancellationToken);
        var items = new List<PayrollBatchListItemDto>(batches.Count);
        foreach (var batch in batches)
        {
            items.Add(new PayrollBatchListItemDto(batch, await GetBatchSummaryAsync(batch.Id, cancellationToken)));
        }

        return new PayrollOverviewDto(
            items.Sum(item => item.Summary.GrossEarnings),
            items.Sum(item => item.Summary.DeductionAmount),
            items.Sum(item => item.Summary.PayableAmount),
            items.Sum(item => item.Summary.PaidAmount),
            items.Sum(item => item.Summary.UnpaidAmount),
            items.Any(item => item.Summary.HasOverpaymentRisk),
            items.Any(item => item.Summary.HasDeductionRisk),
            items);
    }

    private async Task ValidateAllocationsAsync(IReadOnlyList<PayrollCostAllocationRequest> allocations, decimal amount, CancellationToken cancellationToken)
    {
        if (allocations.Count == 0)
        {
            return;
        }

        if (allocations.Any(item => item.Amount <= 0m) || Math.Abs(allocations.Sum(item => item.Amount) - amount) > 0.01m)
        {
            throw new ArgumentException("工资成本分摊金额必须大于零且合计等于工资项目金额。", nameof(allocations));
        }

        if (allocations.Select(item => new { item.ProjectId, item.LegalEntityId }).Distinct().Count() != allocations.Count)
        {
            throw new ArgumentException("同一工资项目不能重复分摊到相同项目和签约公司。", nameof(allocations));
        }

        foreach (var allocation in allocations)
        {
            if (!await db.ProjectLegalEntities.AnyAsync(item => item.ProjectId == allocation.ProjectId && item.LegalEntityId == allocation.LegalEntityId, cancellationToken))
            {
                throw new InvalidOperationException("工资成本分摊的签约公司必须关联到对应项目。");
            }
        }
    }

    private static decimal CalculateItemAmount(CreatePayrollItemRequest request)
    {
        var usesQuantity = request.Quantity.HasValue || request.UnitPrice.HasValue;
        if (usesQuantity)
        {
            if (!request.Quantity.HasValue || !request.UnitPrice.HasValue || request.ManualAmount.HasValue)
            {
                throw new ArgumentException("量价工资必须同时填写数量和单价，且不能再填写手工金额。", nameof(request));
            }

            if (request.Quantity <= 0m || request.UnitPrice < 0m)
            {
                throw new ArgumentException("工资数量必须大于零，单价不能为负数。", nameof(request));
            }

            return decimal.Round(request.Quantity.Value * request.UnitPrice.Value, 2, MidpointRounding.AwayFromZero);
        }

        if (!request.ManualAmount.HasValue || request.ManualAmount <= 0m)
        {
            throw new ArgumentException("非量价工资项目必须填写大于零的金额。", nameof(request));
        }

        return request.ManualAmount.Value;
    }

    private static PayrollBatchDto ToDto(PayrollBatch batch) =>
        new(batch.Id, batch.BatchNumber, batch.Name, batch.BatchType, batch.StartDate, batch.EndDate, batch.ProjectId, batch.LegalEntityId, batch.Status);

    private async Task ValidateDisbursementDimensionsAsync(SavePayrollDisbursementBatchRequest request, CancellationToken cancellationToken)
    {
        if (request.Status == PayrollBatchStatus.Confirmed && (!request.PaymentDate.HasValue || !request.LegalEntityId.HasValue || !request.AccountId.HasValue))
        {
            throw new InvalidOperationException("已核对工资批次必须填写发放日期、发放公司和付款账户。");
        }
        if (request.LegalEntityId.HasValue && !await db.LegalEntities.AnyAsync(item => item.Id == request.LegalEntityId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("发放公司不存在或已停用。");
        }
        if (request.ProjectId.HasValue && !await db.Projects.AnyAsync(item => item.Id == request.ProjectId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("发放项目不存在或已停用。");
        }
        if (request.AccountId.HasValue && (!request.LegalEntityId.HasValue || !await db.FinancialAccounts.AnyAsync(item =>
                item.Id == request.AccountId && item.LegalEntityId == request.LegalEntityId && item.IsActive,
                cancellationToken)))
        {
            throw new InvalidOperationException("付款账户不存在、已停用或不属于发放公司。");
        }

        if (request.FundingSource == PayrollFundingSource.PersonalAdvance)
        {
            if (!request.AccountId.HasValue || !await db.FinancialAccounts.AnyAsync(item =>
                    item.Id == request.AccountId &&
                    item.AccountType == FinancialAccountType.PersonalAdvance &&
                    item.OwnerEmployeeId.HasValue &&
                    item.IsActive,
                    cancellationToken))
            {
                throw new InvalidOperationException("个人垫付必须选择有员工所有人的个人垫付账户。");
            }
        }
        else if (request.AccountId.HasValue && await db.FinancialAccounts.AnyAsync(item => item.Id == request.AccountId && item.AccountType == FinancialAccountType.PersonalAdvance, cancellationToken))
        {
            throw new InvalidOperationException("公司账户资金不能选择个人垫付账户。");
        }

        if (request.RepaysPersonalAdvanceAccountId.HasValue)
        {
            if (request.DisbursementType != PayrollDisbursementType.Other || request.FundingSource != PayrollFundingSource.CompanyAccount)
            {
                throw new InvalidOperationException("个人垫付归还必须是公司账户的其他付款批次。");
            }

            if (!await db.FinancialAccounts.AnyAsync(item =>
                    item.Id == request.RepaysPersonalAdvanceAccountId &&
                    item.AccountType == FinancialAccountType.PersonalAdvance &&
                    item.OwnerEmployeeId.HasValue &&
                    item.IsActive,
                    cancellationToken))
            {
                throw new InvalidOperationException("归还目标不是有效的个人垫付账户。");
            }
        }

        foreach (var line in request.Lines)
        {
            if (line.PaymentCategory != (request.DisbursementType == PayrollDisbursementType.Other ? PayrollPaymentCategory.Other : PayrollPaymentCategory.Wage))
            {
                throw new InvalidOperationException("付款行类别必须与工资批次类别一致。");
            }

            if (line.PaymentCategory == PayrollPaymentCategory.Wage && line.WageCategory == EmployeeWageCategory.MigrantWorkerWage)
            {
                if (!line.LaborBusinessPartnerId.HasValue || !await db.BusinessPartnerRoles.AnyAsync(item =>
                        item.BusinessPartnerId == line.LaborBusinessPartnerId &&
                        item.RoleType == BusinessPartnerRoleType.ConstructionCrew &&
                        item.Partner.IsActive,
                        cancellationToken))
                {
                    throw new InvalidOperationException("民工工资必须关联劳务公司。");
                }
            }

            if (line.LaborBusinessPartnerId.HasValue && !await db.BusinessPartnerRoles.AnyAsync(item =>
                    item.BusinessPartnerId == line.LaborBusinessPartnerId &&
                    item.RoleType == BusinessPartnerRoleType.ConstructionCrew &&
                    item.Partner.IsActive,
                    cancellationToken))
            {
                throw new InvalidOperationException("劳务公司不存在、已停用或没有施工班组角色。");
            }

            if (line.ProjectId.HasValue && !await db.Projects.AnyAsync(item => item.Id == line.ProjectId && item.IsActive, cancellationToken))
            {
                throw new InvalidOperationException("付款行项目不存在或已停用。");
            }
        }
    }

    private async Task<IReadOnlyList<ResolvedDisbursementLine>> ResolveDisbursementLinesAsync(
        PayrollBatch batch,
        SavePayrollDisbursementBatchRequest request,
        CancellationToken cancellationToken)
    {
        var employeeIds = request.Lines.Where(item => item.EmployeeId.HasValue).Select(item => item.EmployeeId!.Value).Distinct().ToArray();
        var workerIds = request.Lines.Where(item => item.ConstructionWorkerId.HasValue).Select(item => item.ConstructionWorkerId!.Value).Distinct().ToArray();
        var crewIds = request.Lines.Where(item => item.CrewBusinessPartnerId.HasValue).Select(item => item.CrewBusinessPartnerId!.Value).Distinct().ToArray();
        var employees = await db.Employees.Where(item => employeeIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var workers = await db.ConstructionWorkers.Where(item => workerIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var crews = await db.BusinessPartners.Where(item => crewIds.Contains(item.Id)).Include(item => item.Roles).ToDictionaryAsync(item => item.Id, cancellationToken);
        var memberships = await db.ConstructionCrewMemberships.AsNoTracking()
            .Where(item => workerIds.Contains(item.ConstructionWorkerId) && crewIds.Contains(item.CrewBusinessPartnerId))
            .ToListAsync(cancellationToken);
        var result = new List<ResolvedDisbursementLine>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            var input = new PayrollDisbursementLineInput(line.RecipientType, line.EmployeeId, line.ConstructionWorkerId, line.CrewBusinessPartnerId, line.Amount);
            var matchesExistingRecipient = line.Id.HasValue && batch.Payments.Any(item =>
                item.Id == line.Id.Value &&
                item.RecipientType == line.RecipientType &&
                item.EmployeeId == line.EmployeeId &&
                item.ConstructionWorkerId == line.ConstructionWorkerId &&
                item.CrewBusinessPartnerId == line.CrewBusinessPartnerId);
            switch (line.RecipientType)
            {
                case PayrollRecipientType.Employee:
                    if (!line.EmployeeId.HasValue || !employees.TryGetValue(line.EmployeeId.Value, out var employee) || !employee.IsActive && !matchesExistingRecipient) throw new InvalidOperationException("员工不存在或已停用。");
                    result.Add(new ResolvedDisbursementLine(input, $"employee:{employee.Id:N}", employee.Name, employee.IdentityNumber, employee.Phone, employee.BankAccountNumber, employee.PositionTitle, null));
                    break;
                case PayrollRecipientType.CrewWorker:
                    if (!line.ConstructionWorkerId.HasValue || !workers.TryGetValue(line.ConstructionWorkerId.Value, out var worker) || !worker.IsActive && !matchesExistingRecipient) throw new InvalidOperationException("班组人员不存在或已停用。");
                    if (!line.CrewBusinessPartnerId.HasValue || !crews.TryGetValue(line.CrewBusinessPartnerId.Value, out var crew) ||
                        (!crew.IsActive || !crew.Roles.Any(role => role.RoleType == BusinessPartnerRoleType.ConstructionCrew)) && !matchesExistingRecipient) throw new InvalidOperationException("施工班组不存在或已停用。");
                    var validMembership = matchesExistingRecipient || memberships.Any(item => item.ConstructionWorkerId == worker.Id && item.CrewBusinessPartnerId == crew.Id &&
                        (!request.PaymentDate.HasValue || item.StartDate <= request.PaymentDate.Value && (!item.EndDate.HasValue || item.EndDate.Value >= request.PaymentDate.Value)));
                    if (!validMembership) throw new InvalidOperationException("班组人员在发放日期不属于所选施工班组。");
                    result.Add(new ResolvedDisbursementLine(input, $"crew-worker:{worker.Id:N}", worker.Name, worker.IdentityNumber, worker.Phone, worker.BankAccountNumber, worker.Trade, crew.Name));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request), $"不支持的人员来源：{line.RecipientType}。");
            }
        }
        return result;
    }

    private void ApplyDisbursementLines(
        PayrollBatch batch,
        IReadOnlyList<PayrollDisbursementLineRequest> requests,
        IReadOnlyList<ResolvedDisbursementLine> resolved)
    {
        var requestedIds = requests.Where(item => item.Id.HasValue).Select(item => item.Id!.Value).ToHashSet();
        foreach (var obsolete in batch.Payments.Where(item => !requestedIds.Contains(item.Id)).ToArray())
        {
            db.PayrollPayments.Remove(obsolete);
        }
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var data = resolved[index];
            var line = request.Id.HasValue
                ? batch.Payments.SingleOrDefault(item => item.Id == request.Id.Value) ?? throw new InvalidOperationException("工资人员明细不存在或不属于当前批次。")
                : new PayrollPayment { Batch = batch };
            line.RecipientType = request.RecipientType;
            line.RecipientKey = data.RecipientKey;
            line.EmployeeId = request.EmployeeId;
            line.ConstructionWorkerId = request.ConstructionWorkerId;
            line.CrewBusinessPartnerId = request.CrewBusinessPartnerId;
            line.PaymentCategory = request.PaymentCategory;
            line.WageCategory = request.WageCategory;
            line.LaborBusinessPartnerId = request.LaborBusinessPartnerId;
            line.ProjectId = request.ProjectId;
            line.Amount = request.Amount;
            line.PayeeType = request.RecipientType == PayrollRecipientType.Employee ? PayrollPayeeType.Employee : PayrollPayeeType.CrewLeader;
            line.PayeeName = data.Name;
            line.RecipientNameSnapshot = data.Name;
            line.IdentityNumberSnapshot = data.IdentityNumber;
            line.PhoneSnapshot = data.Phone;
            line.BankAccountSnapshot = data.BankAccountNumber;
            line.TradeSnapshot = data.Trade;
            line.CrewNameSnapshot = data.CrewName;
            line.Notes = NormalizeOptional(request.Notes);
            line.AccountId = null;
            line.PaymentDate = null;
            line.AccountTransactionId = null;
            line.ConcurrencyStamp = Guid.NewGuid();
            if (!request.Id.HasValue)
            {
                batch.Payments.Add(line);
                db.PayrollPayments.Add(line);
            }
        }
    }

    private async Task ApplyCrewAllocationsAsync(
        PayrollBatch batch,
        IReadOnlyList<PayrollCrewAllocationRequest> requests,
        PayrollDisbursementSummary summary,
        CancellationToken cancellationToken)
    {
        var requestMap = requests.ToDictionary(item => item.CrewBusinessPartnerId);
        var crewIds = summary.CrewAmounts.Select(item => item.CrewBusinessPartnerId).ToArray();
        if (requestMap.Keys.Except(crewIds).Any()) throw new InvalidOperationException("班组工程款关联包含没有人员明细的施工班组。");
        foreach (var obsolete in batch.CrewAllocations.Where(item => !crewIds.Contains(item.CrewBusinessPartnerId)).ToArray())
        {
            db.PayrollCrewAllocations.Remove(obsolete);
        }
        foreach (var crewId in crewIds)
        {
            var request = requestMap.GetValueOrDefault(crewId) ?? new PayrollCrewAllocationRequest(crewId, null, null, null);
            if (request.ContractId.HasValue && !await db.Contracts.AnyAsync(item => item.Id == request.ContractId && item.ProjectId == batch.ProjectId && item.BusinessPartnerId == crewId, cancellationToken))
                throw new InvalidOperationException("班组施工合同与工资批次项目或班组不匹配。");
            if (request.PayableEntryId.HasValue && !await db.PayableEntries.AnyAsync(item => item.Id == request.PayableEntryId && item.ProjectId == batch.ProjectId && item.BusinessPartnerId == crewId && !item.IsVoided, cancellationToken))
                throw new InvalidOperationException("班组应付记录与工资批次项目或班组不匹配。");
            var allocation = batch.CrewAllocations.SingleOrDefault(item => item.CrewBusinessPartnerId == crewId);
            if (allocation is null)
            {
                allocation = new PayrollCrewAllocation { Batch = batch, CrewBusinessPartnerId = crewId };
                batch.CrewAllocations.Add(allocation);
                db.PayrollCrewAllocations.Add(allocation);
            }
            allocation.ContractId = request.ContractId;
            allocation.PayableEntryId = request.PayableEntryId;
            allocation.Notes = NormalizeOptional(request.Notes);
            allocation.ConcurrencyStamp = Guid.NewGuid();
        }
    }

    private async Task ApplyBatchTransactionAsync(PayrollBatch batch, CancellationToken cancellationToken)
    {
        var shouldPost = batch.Status is PayrollBatchStatus.Confirmed or PayrollBatchStatus.Closed or PayrollBatchStatus.ModifiedPendingReview;
        if (!shouldPost)
        {
            if (!batch.AccountTransactionId.HasValue) return;
            var original = await db.AccountTransactions.SingleOrDefaultAsync(item => item.Id == batch.AccountTransactionId.Value, cancellationToken)
                ?? throw new InvalidOperationException("工资批次关联的账户流水不存在。");
            var reversal = await db.AccountTransactions.SingleOrDefaultAsync(
                item => item.SourceType == AccountTransactionSourceType.PayrollPaymentReversal && item.SourceId == batch.Id,
                cancellationToken);
            if (reversal is null)
            {
                reversal = new AccountTransaction
                {
                    Direction = AccountTransactionDirection.Inflow,
                    SourceType = AccountTransactionSourceType.PayrollPaymentReversal,
                    SourceId = batch.Id
                };
                db.AccountTransactions.Add(reversal);
            }
            reversal.AccountId = original.AccountId;
            reversal.TransactionDate = batch.PaymentDate ?? original.TransactionDate;
            reversal.Amount = original.Amount;
            reversal.Description = $"工资批次冲销：{batch.BatchNumber} · {batch.Name}";
            return;
        }

        if (!batch.AccountId.HasValue || !batch.PaymentDate.HasValue) throw new InvalidOperationException("有效工资批次必须填写付款账户和发放日期。");
        AccountTransaction accountTransaction;
        if (batch.AccountTransactionId.HasValue)
        {
            accountTransaction = await db.AccountTransactions.SingleOrDefaultAsync(item => item.Id == batch.AccountTransactionId.Value, cancellationToken)
                ?? throw new InvalidOperationException("工资批次关联的账户流水不存在。");
        }
        else
        {
            accountTransaction = new AccountTransaction
            {
                Direction = AccountTransactionDirection.Outflow,
                SourceType = AccountTransactionSourceType.PayrollPayment,
                SourceId = batch.Id
            };
            batch.AccountTransactionId = accountTransaction.Id;
            db.AccountTransactions.Add(accountTransaction);
        }
        accountTransaction.AccountId = batch.AccountId.Value;
        accountTransaction.TransactionDate = batch.PaymentDate.Value;
        accountTransaction.Amount = batch.ActualAmount;
        accountTransaction.Description = $"工资批次：{batch.BatchNumber} · {batch.Name}";
        var existingReversal = await db.AccountTransactions.SingleOrDefaultAsync(
            item => item.SourceType == AccountTransactionSourceType.PayrollPaymentReversal && item.SourceId == batch.Id,
            cancellationToken);
        if (existingReversal is not null)
        {
            existingReversal.AccountId = batch.AccountId.Value;
            existingReversal.TransactionDate = batch.PaymentDate.Value;
            existingReversal.Amount = 0m;
            existingReversal.Description = $"工资批次已恢复有效：{batch.BatchNumber} · {batch.Name}";
        }
    }

    private async Task SyncPersonalAdvanceEffectsAsync(PayrollBatch batch, CancellationToken cancellationToken)
    {
        var source = await db.EmployeeWageEntries.SingleOrDefaultAsync(item => item.SourcePersonalAdvanceBatchId == batch.Id, cancellationToken);
        var shouldPost = batch.Status is PayrollBatchStatus.Confirmed or PayrollBatchStatus.Closed or PayrollBatchStatus.ModifiedPendingReview;
        var personallyFunded = shouldPost && batch.FundingSource == PayrollFundingSource.PersonalAdvance && batch.ActualAmount > 0m;
        if (personallyFunded)
        {
            if (!batch.AccountId.HasValue || !batch.PaymentDate.HasValue)
            {
                throw new InvalidOperationException("个人垫付批次必须填写账户和发放日期。");
            }

            var account = await db.FinancialAccounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == batch.AccountId.Value, cancellationToken)
                ?? throw new InvalidOperationException("个人垫付账户不存在。");
            if (!account.OwnerEmployeeId.HasValue)
            {
                throw new InvalidOperationException("个人垫付账户必须关联员工所有人。");
            }

            var businessYear = await db.BusinessYears.SingleOrDefaultAsync(item =>
                    item.StartDate <= batch.PaymentDate.Value && item.EndDate >= batch.PaymentDate.Value,
                    cancellationToken)
                ?? throw new InvalidOperationException("个人垫付日期没有对应的业务年度。");
            source ??= new EmployeeWageEntry { SourcePersonalAdvanceBatchId = batch.Id };
            source.EmployeeId = account.OwnerEmployeeId.Value;
            source.BusinessYearId = businessYear.Id;
            source.StartDate = batch.PaymentDate.Value;
            source.EndDate = batch.PaymentDate.Value;
            source.EntryType = EmployeeWageEntryType.Other;
            source.WageCategory = EmployeeWageCategory.SocialSecurityWage;
            source.CalculationMethod = EmployeeWageCalculationMethod.FixedAmount;
            source.Nature = PayrollItemNature.Earning;
            source.AutomaticAmount = batch.ActualAmount;
            source.FinalAmount = batch.ActualAmount;
            source.LegalEntityId = batch.LegalEntityId;
            source.ProjectId = batch.ProjectId;
            source.IsSystemGenerated = true;
            source.ExcludeFromWageCost = true;
            source.Notes = $"个人垫付待归还：{account.OwnerName ?? "账户所有人"} · 批次 {batch.BatchNumber}";
            source.ConcurrencyStamp = Guid.NewGuid();
            if (db.Entry(source).State == EntityState.Detached)
            {
                db.EmployeeWageEntries.Add(source);
            }
        }
        else if (source is not null)
        {
            source.AutomaticAmount = 0m;
            source.FinalAmount = 0m;
            source.Notes = $"个人垫付待归还已冲销：批次 {batch.BatchNumber}";
            source.ConcurrencyStamp = Guid.NewGuid();
        }

        var repayment = await db.AccountTransactions.SingleOrDefaultAsync(item =>
            item.SourceType == AccountTransactionSourceType.PersonalAdvanceRepayment && item.SourceId == batch.Id,
            cancellationToken);
        var shouldRepay = shouldPost && batch.DisbursementType == PayrollDisbursementType.Other && batch.FundingSource == PayrollFundingSource.CompanyAccount && batch.RepaysPersonalAdvanceAccountId.HasValue;
        if (shouldRepay)
        {
            repayment ??= new AccountTransaction
            {
                SourceType = AccountTransactionSourceType.PersonalAdvanceRepayment,
                SourceId = batch.Id,
                Direction = AccountTransactionDirection.Inflow
            };
            repayment.AccountId = batch.RepaysPersonalAdvanceAccountId!.Value;
            repayment.TransactionDate = batch.PaymentDate ?? batch.StartDate;
            repayment.Amount = batch.ActualAmount;
            repayment.Description = $"个人垫付归还：{batch.BatchNumber} · {batch.Name}";
            if (db.Entry(repayment).State == EntityState.Detached)
            {
                db.AccountTransactions.Add(repayment);
            }
        }
        else if (repayment is not null)
        {
            repayment.Amount = 0m;
            repayment.Description = $"个人垫付归还已冲销：{batch.BatchNumber} · {batch.Name}";
        }
    }

    private static PayrollDisbursementBatchDto ToDisbursementBatchDto(PayrollBatch batch) => new(
        batch.Id, batch.BatchNumber, batch.Name, batch.PaymentDate, batch.ProjectId, batch.LegalEntityId, batch.AccountId,
        batch.ActualAmount, batch.PaymentMethod, batch.VoucherNumber, batch.Status, batch.Notes, batch.IsUnifiedDisbursement, batch.ConcurrencyStamp,
        batch.DisbursementType, batch.FundingSource, batch.RepaysPersonalAdvanceAccountId);

    private static PayrollDisbursementLineDto ToDisbursementLineDto(PayrollPayment item) => new(
        item.Id, item.RecipientType, item.EmployeeId, item.ConstructionWorkerId, item.CrewBusinessPartnerId,
        item.Amount, item.RecipientNameSnapshot ?? item.PayeeName, item.IdentityNumberSnapshot, item.PhoneSnapshot, item.BankAccountSnapshot,
        item.TradeSnapshot, item.CrewNameSnapshot, item.Notes, item.ConcurrencyStamp,
        item.PaymentCategory, item.WageCategory, item.LaborBusinessPartnerId, item.ProjectId);

    private static PayrollDisbursementLineInput ToDisbursementSummaryInput(PayrollPayment item) =>
        new(
            item.RecipientType,
            item.EmployeeId,
            item.ConstructionWorkerId,
            item.CrewBusinessPartnerId,
            item.Amount);

    private static object BatchSnapshot(PayrollBatch batch) => new
    {
        batch.BatchNumber,
        batch.Name,
        batch.PaymentDate,
        batch.ProjectId,
        batch.LegalEntityId,
        batch.AccountId,
        batch.ActualAmount,
        batch.PaymentMethod,
        batch.VoucherNumber,
        batch.Status,
        batch.Notes,
        Lines = batch.Payments.Select(item => new { item.Id, item.RecipientType, item.EmployeeId, item.ConstructionWorkerId, item.CrewBusinessPartnerId, item.Amount, item.RecipientNameSnapshot, item.Notes }),
        CrewAllocations = batch.CrewAllocations.Select(item => new { item.CrewBusinessPartnerId, item.ContractId, item.PayableEntryId, item.Notes })
    };

    private sealed record ResolvedDisbursementLine(
        PayrollDisbursementLineInput Input,
        string RecipientKey,
        string Name,
        string? IdentityNumber,
        string? Phone,
        string? BankAccountNumber,
        string? Trade,
        string? CrewName);

    private static PayrollItemDto ToDto(PayrollItem item) =>
        new(item.Id, item.EmployeeId, item.ItemType, item.Nature, item.Quantity, item.UnitPrice, item.Amount, item.Description);

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空。", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
