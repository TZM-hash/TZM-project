using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Payroll;

public sealed class PayrollService(ApplicationDbContext db) : IPayrollService
{
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
        var employeeIds = batch.Items.Select(item => item.EmployeeId).Concat(batch.Payments.Select(item => item.EmployeeId)).Distinct().ToArray();
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
