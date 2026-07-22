using EngineeringManager.Application.EmployeeLedger;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using EngineeringManager.Domain.StageResults;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EngineeringManager.Infrastructure.EmployeeLedger;

public sealed class EmployeeLedgerService(ApplicationDbContext db, IFileStore? fileStore = null) : IEmployeeLedgerService
{
    public async Task<Guid> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        var category = NormalizeRequired(request.Category, nameof(request.Category));
        await ValidateDimensionsAsync(request.EmployeeId, request.ProjectId, request.DepartmentId, request.LegalEntityId, cancellationToken);
        var finalAmount = request.Amount + request.AdjustmentAmount;
        if (finalAmount < 0m)
        {
            throw new ArgumentException("报销最终金额不能小于零。", nameof(request));
        }

        Attachment? attachment = null;
        if (request.Attachment is not null)
        {
            if (fileStore is null)
            {
                throw new InvalidOperationException("当前环境未配置附件存储。");
            }

            if (request.Attachment.Content.Length == 0)
            {
                throw new ArgumentException("报销附件不能为空。", nameof(request));
            }

            var safeName = Path.GetFileName(request.Attachment.OriginalFileName);
            if (!string.Equals(safeName, request.Attachment.OriginalFileName, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(safeName))
            {
                throw new ArgumentException("报销附件文件名无效。", nameof(request));
            }

            await using var content = new MemoryStream(request.Attachment.Content, writable: false);
            var storedName = await fileStore.SaveAsync(content, safeName, cancellationToken);
            attachment = new Attachment
            {
                StoredName = storedName,
                OriginalFileName = safeName,
                ContentType = string.IsNullOrWhiteSpace(request.Attachment.ContentType) ? "application/octet-stream" : request.Attachment.ContentType.Trim(),
                SizeBytes = request.Attachment.Content.LongLength,
                Category = AttachmentCategory.General,
                Description = "员工报销附件"
            };
            db.Attachments.Add(attachment);
        }

        var expense = new ExpenseRecord
        {
            EmployeeId = request.EmployeeId,
            ProjectId = request.ProjectId,
            DepartmentId = request.DepartmentId,
            LegalEntityId = request.LegalEntityId,
            ExpenseDate = request.ExpenseDate,
            Category = category,
            OriginalAmount = request.Amount,
            AdjustmentAmount = request.AdjustmentAmount,
            Amount = finalAmount,
            ReceiptNumber = NormalizeOptional(request.ReceiptNumber),
            Attachment = attachment,
            Description = NormalizeOptional(request.Description)
        };
        db.ExpenseRecords.Add(expense);
        await db.SaveChangesAsync(cancellationToken);
        return expense.Id;
    }

    public async Task<IReadOnlyList<EmployeeExpenseDto>> GetExpensesAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(item => item.Id == employeeId, cancellationToken))
        {
            throw new InvalidOperationException("员工不存在。");
        }

        return await db.ExpenseRecords.AsNoTracking()
            .Include(item => item.Project)
            .Include(item => item.Attachment)
            .Where(item => item.EmployeeId == employeeId && !item.IsVoided)
            .OrderByDescending(item => item.ExpenseDate)
            .ThenByDescending(item => item.Id)
            .Select(item => new EmployeeExpenseDto(
                item.Id,
                item.EmployeeId,
                item.ExpenseDate,
                item.Amount,
                item.ProjectId,
                item.Project == null ? null : item.Project.Name,
                item.ReceiptNumber,
                item.AttachmentId,
                item.Attachment == null ? null : item.Attachment.OriginalFileName,
                item.Description,
                item.ConcurrencyStamp))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeExpenseDto> UpdateExpenseAsync(UpdateExpenseRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        var expense = await db.ExpenseRecords
            .Include(item => item.Project)
            .Include(item => item.Attachment)
            .SingleOrDefaultAsync(item => item.Id == request.Id && !item.IsVoided, cancellationToken)
            ?? throw new InvalidOperationException("报销记录不存在或已作废。");
        if (expense.ConcurrencyStamp != request.ConcurrencyStamp)
        {
            throw new InvalidOperationException("报销记录已被其他用户修改，请刷新后重试。");
        }

        if (request.ProjectId.HasValue && !await db.ProjectLegalEntities.AnyAsync(
                item => item.ProjectId == request.ProjectId && item.LegalEntityId == expense.LegalEntityId,
                cancellationToken))
        {
            throw new InvalidOperationException("项目不存在、已停用或未关联报销公司。");
        }

        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        var before = JsonSerializer.Serialize(ExpenseSnapshot(expense));
        expense.ExpenseDate = request.ExpenseDate;
        expense.ProjectId = request.ProjectId;
        expense.Amount = request.Amount;
        expense.OriginalAmount = request.Amount;
        expense.AdjustmentAmount = 0m;
        expense.Category = "报销";
        expense.ReceiptNumber = NormalizeOptional(request.ReceiptNumber);
        expense.Description = NormalizeOptional(request.Description);
        if (request.Attachment is not null)
        {
            expense.Attachment = await SaveAttachmentAsync(request.Attachment, "员工报销附件", cancellationToken);
        }

        expense.ConcurrencyStamp = Guid.NewGuid();
        db.AuditLogs.Add(new AuditLog
        {
            UserId = request.UserId,
            Action = "UpdateEmployeeExpense",
            EntityType = nameof(ExpenseRecord),
            EntityId = expense.Id.ToString(),
            Reason = reason,
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(ExpenseSnapshot(expense))
        });
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(expense).Reference(item => item.Project).LoadAsync(cancellationToken);
        await db.Entry(expense).Reference(item => item.Attachment).LoadAsync(cancellationToken);
        return ToDto(expense);
    }

    public async Task<Guid> RecordExpensePaymentAsync(RecordExpensePaymentRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        if (request.RecordKind == EmployeeLedgerRecordKind.Payable)
        {
            throw new ArgumentException("报销实际记录只能是支付或退回/冲销。", nameof(request));
        }

        var expense = await db.ExpenseRecords.SingleOrDefaultAsync(item => item.Id == request.ExpenseRecordId && !item.IsVoided, cancellationToken)
            ?? throw new InvalidOperationException("报销记录不存在或已作废。");
        await ValidateAccountAsync(request.AccountId, expense.LegalEntityId, cancellationToken);
        var payment = new ExpensePayment
        {
            ExpenseRecordId = request.ExpenseRecordId,
            AccountId = request.AccountId,
            PaymentDate = request.PaymentDate,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            RecordKind = request.RecordKind,
            Notes = NormalizeOptional(request.Notes)
        };
        var direction = request.RecordKind == EmployeeLedgerRecordKind.Payment ? AccountTransactionDirection.Outflow : AccountTransactionDirection.Inflow;
        var transaction = CreateTransaction(request.AccountId, direction, AccountTransactionSourceType.ExpensePayment, payment.Id, request.PaymentDate, request.Amount, payment.Notes);
        payment.AccountTransactionId = transaction.Id;
        db.ExpensePayments.Add(payment);
        db.AccountTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);
        return payment.Id;
    }

    public async Task<Guid> RecordAdvanceAsync(RecordEmployeeAdvanceRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        await ValidateDimensionsAsync(request.EmployeeId, request.ProjectId, null, request.LegalEntityId, cancellationToken);
        if (request.Action == EmployeeAdvanceAction.PayrollDeduction && request.AccountId.HasValue)
        {
            throw new ArgumentException("工资抵扣借支不应选择资金账户。", nameof(request));
        }

        if (request.Action != EmployeeAdvanceAction.PayrollDeduction && !request.AccountId.HasValue)
        {
            throw new ArgumentException("借支发放或归还必须选择资金账户。", nameof(request));
        }

        if (request.AccountId.HasValue)
        {
            await ValidateAccountAsync(request.AccountId.Value, request.LegalEntityId, cancellationToken);
        }

        var advance = new EmployeeAdvance
        {
            EmployeeId = request.EmployeeId,
            ProjectId = request.ProjectId,
            LegalEntityId = request.LegalEntityId,
            AccountId = request.AccountId,
            EntryDate = request.EntryDate,
            Amount = request.Amount,
            Action = request.Action,
            Description = NormalizeOptional(request.Description)
        };
        db.EmployeeAdvances.Add(advance);
        if (request.Action != EmployeeAdvanceAction.PayrollDeduction)
        {
            var direction = request.Action == EmployeeAdvanceAction.Disbursement ? AccountTransactionDirection.Outflow : AccountTransactionDirection.Inflow;
            var sourceType = request.Action == EmployeeAdvanceAction.Disbursement ? AccountTransactionSourceType.EmployeeAdvanceDisbursement : AccountTransactionSourceType.EmployeeAdvanceRepayment;
            var transaction = CreateTransaction(request.AccountId!.Value, direction, sourceType, advance.Id, request.EntryDate, request.Amount, advance.Description);
            advance.AccountTransactionId = transaction.Id;
            db.AccountTransactions.Add(transaction);
        }

        await db.SaveChangesAsync(cancellationToken);
        return advance.Id;
    }

    public async Task<Guid> CreateOtherPayableAsync(CreateEmployeeOtherPayableRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        if (request.EntryType is not (EmployeeLedgerEntryType.Dividend or EmployeeLedgerEntryType.Interest or EmployeeLedgerEntryType.Other))
        {
            throw new ArgumentException("其他员工往来应付只支持分红、利息或其他类型。", nameof(request));
        }

        await ValidateDimensionsAsync(request.EmployeeId, request.ProjectId, null, request.LegalEntityId, cancellationToken);
        var entry = new EmployeeOtherPayment
        {
            EmployeeId = request.EmployeeId,
            ProjectId = request.ProjectId,
            LegalEntityId = request.LegalEntityId,
            EntryType = request.EntryType,
            RecordKind = EmployeeLedgerRecordKind.Payable,
            EntryDate = request.EntryDate,
            Amount = request.Amount,
            Description = NormalizeOptional(request.Description)
        };
        db.EmployeeOtherPayments.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<IReadOnlyList<EmployeeOtherPayableDto>> GetOtherPayablesAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(item => item.Id == employeeId, cancellationToken))
        {
            throw new InvalidOperationException("员工不存在。");
        }

        return await db.EmployeeOtherPayments.AsNoTracking()
            .Include(item => item.LegalEntity)
            .Include(item => item.Project)
            .Include(item => item.Attachment)
            .Where(item => item.EmployeeId == employeeId && item.RecordKind == EmployeeLedgerRecordKind.Payable)
            .OrderByDescending(item => item.EntryDate)
            .ThenByDescending(item => item.Id)
            .Select(item => new EmployeeOtherPayableDto(
                item.Id,
                item.EmployeeId,
                item.EntryDate,
                item.Amount,
                item.EntryType,
                item.LegalEntityId,
                item.LegalEntity.Name,
                item.ProjectId,
                item.Project == null ? null : item.Project.Name,
                item.AttachmentId,
                item.Attachment == null ? null : item.Attachment.OriginalFileName,
                item.Description,
                item.ConcurrencyStamp))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeOtherPayableDto> UpdateOtherPayableAsync(
        UpdateEmployeeOtherPayableRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        if (request.EntryType is not (EmployeeLedgerEntryType.Dividend or EmployeeLedgerEntryType.Interest or EmployeeLedgerEntryType.Other))
        {
            throw new ArgumentException("其他员工往来应付只支持分红、利息或其他类型。", nameof(request));
        }

        var entry = await db.EmployeeOtherPayments
            .Include(item => item.LegalEntity)
            .Include(item => item.Project)
            .Include(item => item.Attachment)
            .SingleOrDefaultAsync(item => item.Id == request.Id && item.RecordKind == EmployeeLedgerRecordKind.Payable, cancellationToken)
            ?? throw new InvalidOperationException("员工往来应付记录不存在。");
        if (entry.ConcurrencyStamp != request.ConcurrencyStamp)
        {
            throw new InvalidOperationException("员工往来应付记录已被其他用户修改，请刷新后重试。");
        }

        await ValidateDimensionsAsync(entry.EmployeeId, request.ProjectId, null, request.LegalEntityId, cancellationToken);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        var before = JsonSerializer.Serialize(new { entry.EntryDate, entry.Amount, entry.EntryType, entry.LegalEntityId, entry.ProjectId, entry.Description });
        entry.EntryDate = request.EntryDate;
        entry.Amount = request.Amount;
        entry.EntryType = request.EntryType;
        entry.LegalEntityId = request.LegalEntityId;
        entry.ProjectId = request.ProjectId;
        entry.Description = NormalizeOptional(request.Description);
        entry.ConcurrencyStamp = Guid.NewGuid();
        db.AuditLogs.Add(new AuditLog
        {
            UserId = request.UserId,
            Action = "UpdateEmployeeOtherPayable",
            EntityType = nameof(EmployeeOtherPayment),
            EntityId = entry.Id.ToString(),
            Reason = reason,
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(new { entry.EntryDate, entry.Amount, entry.EntryType, entry.LegalEntityId, entry.ProjectId, entry.Description })
        });
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(entry).Reference(item => item.LegalEntity).LoadAsync(cancellationToken);
        await db.Entry(entry).Reference(item => item.Project).LoadAsync(cancellationToken);
        return new EmployeeOtherPayableDto(
            entry.Id,
            entry.EmployeeId,
            entry.EntryDate,
            entry.Amount,
            entry.EntryType,
            entry.LegalEntityId,
            entry.LegalEntity?.Name ?? string.Empty,
            entry.ProjectId,
            entry.Project?.Name,
            entry.AttachmentId,
            entry.Attachment?.OriginalFileName,
            entry.Description,
            entry.ConcurrencyStamp);
    }

    public async Task<Guid> RecordOtherPaymentAsync(RecordEmployeeOtherPaymentRequest request, CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount);
        if (request.RecordKind == EmployeeLedgerRecordKind.Payable)
        {
            throw new ArgumentException("其他员工往来实际记录只能是支付或退回/冲销。", nameof(request));
        }

        var payable = await db.EmployeeOtherPayments.SingleOrDefaultAsync(item => item.Id == request.PayableEntryId && item.RecordKind == EmployeeLedgerRecordKind.Payable, cancellationToken)
            ?? throw new InvalidOperationException("员工往来应付记录不存在。");
        await ValidateAccountAsync(request.AccountId, payable.LegalEntityId, cancellationToken);
        var payment = new EmployeeOtherPayment
        {
            EmployeeId = payable.EmployeeId,
            ProjectId = payable.ProjectId,
            LegalEntityId = payable.LegalEntityId,
            EntryType = payable.EntryType,
            RecordKind = request.RecordKind,
            RelatedPayableId = payable.Id,
            AccountId = request.AccountId,
            EntryDate = request.EntryDate,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Description = NormalizeOptional(request.Description)
        };
        var direction = request.RecordKind == EmployeeLedgerRecordKind.Payment ? AccountTransactionDirection.Outflow : AccountTransactionDirection.Inflow;
        var transaction = CreateTransaction(request.AccountId, direction, AccountTransactionSourceType.EmployeeOtherPayment, payment.Id, request.EntryDate, request.Amount, payment.Description);
        payment.AccountTransactionId = transaction.Id;
        db.EmployeeOtherPayments.Add(payment);
        db.AccountTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);
        return payment.Id;
    }

    public async Task<EmployeeLedgerSummaryDto> GetEmployeeSummaryAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(item => item.Id == employeeId, cancellationToken))
        {
            throw new InvalidOperationException("员工不存在。");
        }

        var expensePayable = await db.ExpenseRecords.Where(item => item.EmployeeId == employeeId && !item.IsVoided).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var expensePayments = db.ExpensePayments.Where(item => item.Expense.EmployeeId == employeeId);
        var expensePaid = await expensePayments.Where(item => item.RecordKind == EmployeeLedgerRecordKind.Payment).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var expenseRefund = await expensePayments.Where(item => item.RecordKind == EmployeeLedgerRecordKind.RefundOrReversal).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var advances = db.EmployeeAdvances.Where(item => item.EmployeeId == employeeId);
        var advanceDisbursed = await advances.Where(item => item.Action == EmployeeAdvanceAction.Disbursement).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var advanceRepaid = await advances.Where(item => item.Action == EmployeeAdvanceAction.Repayment).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var advanceDeducted = await advances.Where(item => item.Action == EmployeeAdvanceAction.PayrollDeduction).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var otherEntries = db.EmployeeOtherPayments.Where(item => item.EmployeeId == employeeId);
        var otherPayable = await otherEntries.Where(item => item.RecordKind == EmployeeLedgerRecordKind.Payable).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var otherPaid = await otherEntries.Where(item => item.RecordKind == EmployeeLedgerRecordKind.Payment).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var otherRefund = await otherEntries.Where(item => item.RecordKind == EmployeeLedgerRecordKind.RefundOrReversal).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var summary = EmployeeLedgerCalculator.Calculate(expensePayable, expensePaid, expenseRefund, advanceDisbursed, advanceRepaid, advanceDeducted, otherPayable, otherPaid - otherRefund);
        return new EmployeeLedgerSummaryDto(
            employeeId,
            summary.ExpensePayableAmount,
            summary.ExpensePaidAmount,
            summary.ExpenseUnpaidAmount,
            summary.AdvanceOutstandingAmount,
            summary.OtherPayableAmount,
            summary.OtherPaidAmount,
            summary.OtherUnpaidAmount,
            summary.HasExpenseOverpaymentRisk,
            summary.HasOtherOverpaymentRisk,
            summary.HasAdvanceOverSettlementRisk);
    }

    public async Task<EmployeeLedgerOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var employees = await db.Employees.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.EmployeeNumber)
            .Select(item => new { item.Id, item.EmployeeNumber, item.Name }).ToListAsync(cancellationToken);
        var items = new List<EmployeeLedgerListItemDto>(employees.Count);
        foreach (var employee in employees)
        {
            items.Add(new EmployeeLedgerListItemDto(employee.Id, employee.EmployeeNumber, employee.Name, await GetEmployeeSummaryAsync(employee.Id, cancellationToken)));
        }

        return new EmployeeLedgerOverviewDto(
            items.Sum(item => item.Summary.ExpensePayableAmount),
            items.Sum(item => item.Summary.ExpensePaidAmount),
            items.Sum(item => item.Summary.ExpenseUnpaidAmount),
            items.Sum(item => item.Summary.AdvanceOutstandingAmount),
            items.Sum(item => item.Summary.OtherPayableAmount),
            items.Sum(item => item.Summary.OtherPaidAmount),
            items.Sum(item => item.Summary.OtherUnpaidAmount),
            items.Any(item => item.Summary.HasExpenseOverpaymentRisk || item.Summary.HasOtherOverpaymentRisk || item.Summary.HasAdvanceOverSettlementRisk),
            items);
    }

    private async Task ValidateDimensionsAsync(Guid employeeId, Guid? projectId, Guid? departmentId, Guid legalEntityId, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(item => item.Id == employeeId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("员工不存在或已停用。");
        }

        if (projectId.HasValue && !await db.ProjectLegalEntities.AnyAsync(item => item.ProjectId == projectId && item.LegalEntityId == legalEntityId, cancellationToken))
        {
            throw new InvalidOperationException("项目不存在、已停用或未关联所选签约公司。");
        }

        if (departmentId.HasValue && !await db.OrganizationUnits.AnyAsync(item => item.Id == departmentId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("部门不存在或已停用。");
        }

        if (!await db.LegalEntities.AnyAsync(item => item.Id == legalEntityId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("签约公司不存在或已停用。");
        }
    }

    private async Task ValidateAccountAsync(Guid accountId, Guid legalEntityId, CancellationToken cancellationToken)
    {
        if (!await db.FinancialAccounts.AnyAsync(item => item.Id == accountId && item.LegalEntityId == legalEntityId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("资金账户不存在、已停用或不属于所选签约公司。");
        }
    }

    private static AccountTransaction CreateTransaction(Guid accountId, AccountTransactionDirection direction, AccountTransactionSourceType sourceType, Guid sourceId, DateOnly date, decimal amount, string? description) =>
        new() { AccountId = accountId, Direction = direction, SourceType = sourceType, SourceId = sourceId, TransactionDate = date, Amount = amount, Description = description };

    private async Task<Attachment?> SaveAttachmentAsync(
        ExpenseAttachmentUpload? upload,
        string description,
        CancellationToken cancellationToken)
    {
        if (upload is null)
        {
            return null;
        }

        if (fileStore is null)
        {
            throw new InvalidOperationException("当前环境未配置附件存储。");
        }

        if (upload.Content.Length == 0)
        {
            throw new ArgumentException("附件不能为空。", nameof(upload));
        }

        var safeName = Path.GetFileName(upload.OriginalFileName);
        if (string.IsNullOrWhiteSpace(safeName) || !string.Equals(safeName, upload.OriginalFileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("附件文件名无效。", nameof(upload));
        }

        await using var content = new MemoryStream(upload.Content, writable: false);
        var storedName = await fileStore.SaveAsync(content, safeName, cancellationToken);
        var attachment = new Attachment
        {
            StoredName = storedName,
            OriginalFileName = safeName,
            ContentType = string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType.Trim(),
            SizeBytes = upload.Content.LongLength,
            Category = AttachmentCategory.General,
            Description = description
        };
        db.Attachments.Add(attachment);
        return attachment;
    }

    private static EmployeeExpenseDto ToDto(ExpenseRecord item) => new(
        item.Id,
        item.EmployeeId,
        item.ExpenseDate,
        item.Amount,
        item.ProjectId,
        item.Project?.Name,
        item.ReceiptNumber,
        item.AttachmentId,
        item.Attachment?.OriginalFileName,
        item.Description,
        item.ConcurrencyStamp);

    private static object ExpenseSnapshot(ExpenseRecord item) => new
    {
        item.ExpenseDate,
        item.Amount,
        item.OriginalAmount,
        item.AdjustmentAmount,
        item.ProjectId,
        item.ReceiptNumber,
        item.AttachmentId,
        item.Description
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

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
