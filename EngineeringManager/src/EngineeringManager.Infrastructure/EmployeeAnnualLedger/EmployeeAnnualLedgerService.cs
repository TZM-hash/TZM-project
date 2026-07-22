using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using EngineeringManager.Domain.StageResults;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EngineeringManager.Infrastructure.EmployeeAnnualLedger;

public sealed class EmployeeAnnualLedgerService(ApplicationDbContext db, TimeProvider timeProvider, IFileStore? fileStore = null) : IEmployeeAnnualLedgerService
{
    public async Task<EmployeeWageEntryDto> AddWageEntryAsync(CreateEmployeeWageEntryRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(item => item.Id == request.EmployeeId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("员工不存在或已停用。");
        }

        var businessYear = await db.BusinessYears.SingleOrDefaultAsync(item => item.Id == request.BusinessYearId, cancellationToken)
            ?? throw new InvalidOperationException("业务年度不存在。");
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        if (today < businessYear.StartDate || today > businessYear.EndDate)
        {
            throw new InvalidOperationException("历史年度原始应付明细已锁定，不能新增工资记录。");
        }

        BusinessYearRules.EnsureContained(
            request.StartDate,
            request.EndDate,
            new BusinessYearPeriod(businessYear.StartDate, businessYear.EndDate),
            "工资段");
        await ValidateDimensionsAsync(request.LegalEntityId, request.ProjectId, request.LaborBusinessPartnerId, cancellationToken);
        EnsureMigrantLaborCompany(request.WageCategory, request.LaborBusinessPartnerId);
        var effectiveNature = request.EntryType == EmployeeWageEntryType.Penalty ? PayrollItemNature.Deduction : request.Nature;
        var amount = EmployeeAnnualLedgerCalculator.CalculateWageAmount(
            effectiveNature,
            request.Quantity,
            request.UnitPrice,
            request.ManualAmount,
            request.AdjustmentAmount);
        var entry = new EmployeeWageEntry
        {
            EmployeeId = request.EmployeeId,
            BusinessYearId = request.BusinessYearId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            EntryType = request.EntryType,
            WageCategory = request.WageCategory,
            CalculationMethod = request.CalculationMethod,
            Nature = effectiveNature,
            Quantity = request.Quantity,
            Unit = NormalizeOptional(request.Unit),
            UnitPrice = request.UnitPrice,
            AutomaticAmount = amount.AutomaticAmount,
            LegalEntityId = request.LegalEntityId,
            ProjectId = request.ProjectId,
            LaborBusinessPartnerId = request.LaborBusinessPartnerId,
            AdjustmentAmount = request.AdjustmentAmount,
            FinalAmount = amount.FinalAmount,
            Attachment = await SaveAttachmentAsync(request.Attachment, "员工工资附件", cancellationToken),
            Notes = NormalizeOptional(request.Notes)
        };
        db.EmployeeWageEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task<IReadOnlyList<EmployeeWageEntryDto>> GetWageEntriesAsync(
        Guid employeeId,
        Guid businessYearId,
        CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(item => item.Id == employeeId, cancellationToken))
        {
            throw new InvalidOperationException("员工不存在。");
        }

        return await db.EmployeeWageEntries.AsNoTracking()
            .Include(item => item.LegalEntity)
            .Include(item => item.Project)
            .Include(item => item.LaborBusinessPartner)
            .Include(item => item.Attachment)
            .Where(item => item.EmployeeId == employeeId && item.BusinessYearId == businessYearId)
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.Id)
            .Select(item => ToDto(item))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeWageEntryDto> UpdateWageEntryAsync(
        UpdateEmployeeWageEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await db.EmployeeWageEntries
            .Include(item => item.BusinessYear)
            .Include(item => item.LegalEntity)
            .Include(item => item.Project)
            .Include(item => item.LaborBusinessPartner)
            .Include(item => item.Attachment)
            .SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("工资明细不存在。");
        if (entry.IsSystemGenerated)
        {
            throw new InvalidOperationException("系统生成的个人垫付应付记录不能直接编辑，请修改来源工资台账。");
        }

        if (entry.ConcurrencyStamp != request.ConcurrencyStamp)
        {
            throw new InvalidOperationException("工资明细已被其他用户修改，请刷新后重试。");
        }

        BusinessYearRules.EnsureContained(
            request.StartDate,
            request.EndDate,
            new BusinessYearPeriod(entry.BusinessYear.StartDate, entry.BusinessYear.EndDate),
            "工资段");
        await ValidateDimensionsAsync(request.LegalEntityId, request.ProjectId, request.LaborBusinessPartnerId, cancellationToken);
        EnsureMigrantLaborCompany(request.WageCategory, request.LaborBusinessPartnerId);
        var effectiveNature = request.EntryType == EmployeeWageEntryType.Penalty ? PayrollItemNature.Deduction : request.Nature;
        var amount = EmployeeAnnualLedgerCalculator.CalculateWageAmount(
            effectiveNature,
            request.Quantity,
            request.UnitPrice,
            request.ManualAmount,
            request.AdjustmentAmount);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        var before = JsonSerializer.Serialize(WageSnapshot(entry));

        entry.StartDate = request.StartDate;
        entry.EndDate = request.EndDate;
        entry.EntryType = request.EntryType;
        entry.WageCategory = request.WageCategory;
        entry.CalculationMethod = request.CalculationMethod;
        entry.Nature = effectiveNature;
        entry.Quantity = request.Quantity;
        entry.Unit = NormalizeOptional(request.Unit);
        entry.UnitPrice = request.UnitPrice;
        entry.AutomaticAmount = amount.AutomaticAmount;
        entry.LegalEntityId = request.LegalEntityId;
        entry.ProjectId = request.ProjectId;
        entry.LaborBusinessPartnerId = request.LaborBusinessPartnerId;
        entry.AdjustmentAmount = request.AdjustmentAmount;
        entry.FinalAmount = amount.FinalAmount;
        entry.Notes = NormalizeOptional(request.Notes);
        if (request.Attachment is not null)
        {
            entry.Attachment = await SaveAttachmentAsync(request.Attachment, "员工工资附件", cancellationToken);
        }

        entry.ConcurrencyStamp = Guid.NewGuid();
        db.AuditLogs.Add(new AuditLog
        {
            UserId = request.UserId,
            Action = "UpdateEmployeeWageEntry",
            EntityType = nameof(EmployeeWageEntry),
            EntityId = entry.Id.ToString(),
            Reason = reason,
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(WageSnapshot(entry))
        });
        await db.SaveChangesAsync(cancellationToken);

        await db.Entry(entry).Reference(item => item.LegalEntity).LoadAsync(cancellationToken);
        await db.Entry(entry).Reference(item => item.Project).LoadAsync(cancellationToken);
        await db.Entry(entry).Reference(item => item.LaborBusinessPartner).LoadAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task<EmployeeFinancialAdjustmentDto> AddAdjustmentAsync(CreateEmployeeFinancialAdjustmentRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount == 0m)
        {
            throw new ArgumentException("管理员调整金额不能为零。", nameof(request));
        }

        if (request.AdjustmentType == EmployeeFinancialAdjustmentType.Reversal)
        {
            throw new ArgumentException("调整冲销必须通过冲销操作创建。", nameof(request));
        }

        var notes = NormalizeRequired(request.Notes, nameof(request.Notes));
        if (!await db.Employees.AnyAsync(item => item.Id == request.EmployeeId, cancellationToken))
        {
            throw new InvalidOperationException("员工不存在。");
        }

        var businessYear = await db.BusinessYears.SingleOrDefaultAsync(item => item.Id == request.BusinessYearId, cancellationToken)
            ?? throw new InvalidOperationException("业务年度不存在。");
        BusinessYearRules.EnsureContained(
            request.AdjustmentDate,
            request.AdjustmentDate,
            new BusinessYearPeriod(businessYear.StartDate, businessYear.EndDate),
            "调整记录");
        var adjustment = new EmployeeFinancialAdjustment
        {
            EmployeeId = request.EmployeeId,
            BusinessYearId = request.BusinessYearId,
            AdjustmentDate = request.AdjustmentDate,
            Amount = request.Amount,
            AdjustmentType = request.AdjustmentType,
            Notes = notes
        };
        db.EmployeeFinancialAdjustments.Add(adjustment);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(adjustment);
    }

    public async Task<EmployeeReceiptDto> RecordReceiptAsync(RecordEmployeeReceiptRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0m)
        {
            throw new ArgumentException("领款金额必须大于零。", nameof(request));
        }

        if (!await db.Employees.AnyAsync(item => item.Id == request.EmployeeId, cancellationToken))
        {
            throw new InvalidOperationException("员工不存在。");
        }

        var businessYear = await db.BusinessYears.SingleOrDefaultAsync(item => item.Id == request.BusinessYearId, cancellationToken)
            ?? throw new InvalidOperationException("业务年度不存在。");
        BusinessYearRules.EnsureContained(
            request.ReceiptDate,
            request.ReceiptDate,
            new BusinessYearPeriod(businessYear.StartDate, businessYear.EndDate),
            "领款记录");
        if (!await db.LegalEntities.AnyAsync(item => item.Id == request.PaymentLegalEntityId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("付款公司不存在或已停用。");
        }

        if (!await db.FinancialAccounts.AnyAsync(item =>
                item.Id == request.AccountId &&
                item.LegalEntityId == request.PaymentLegalEntityId &&
                item.IsActive,
                cancellationToken))
        {
            throw new InvalidOperationException("付款账户不存在、已停用或不属于付款公司。");
        }

        await ValidateDimensionsAsync(null, request.ProjectId, request.LaborBusinessPartnerId, cancellationToken);
        var recipient = NormalizeRequired(request.ActualRecipientName, nameof(request.ActualRecipientName));
        var receipt = new EmployeeReceipt
        {
            EmployeeId = request.EmployeeId,
            BusinessYearId = request.BusinessYearId,
            ReceiptDate = request.ReceiptDate,
            ReceiptType = request.ReceiptType,
            Amount = request.Amount,
            PaymentLegalEntityId = request.PaymentLegalEntityId,
            AccountId = request.AccountId,
            PaymentMethod = request.PaymentMethod,
            ActualRecipientName = recipient,
            ProjectId = request.ProjectId,
            LaborBusinessPartnerId = request.LaborBusinessPartnerId,
            Notes = NormalizeOptional(request.Notes)
        };
        var transaction = new AccountTransaction
        {
            AccountId = request.AccountId,
            Direction = AccountTransactionDirection.Outflow,
            SourceType = AccountTransactionSourceType.EmployeeReceipt,
            SourceId = receipt.Id,
            TransactionDate = request.ReceiptDate,
            Amount = request.Amount,
            Description = $"员工领款：{recipient}"
        };
        receipt.AccountTransactionId = transaction.Id;
        db.EmployeeReceipts.Add(receipt);
        db.AccountTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(receipt);
    }

    public async Task<EmployeeFinancialAdjustmentDto> ReverseAdjustmentAsync(
        Guid adjustmentId,
        DateOnly reversalDate,
        string notes,
        CancellationToken cancellationToken)
    {
        var original = await db.EmployeeFinancialAdjustments.SingleOrDefaultAsync(item => item.Id == adjustmentId, cancellationToken)
            ?? throw new InvalidOperationException("管理员调整记录不存在。");
        if (original.AdjustmentType == EmployeeFinancialAdjustmentType.Reversal ||
            await db.EmployeeFinancialAdjustments.AnyAsync(item => item.ReversalOfId == adjustmentId, cancellationToken))
        {
            throw new InvalidOperationException("该调整已经冲销或本身就是冲销记录。");
        }

        var businessYear = await db.BusinessYears.SingleOrDefaultAsync(
            item => item.StartDate <= reversalDate && item.EndDate >= reversalDate,
            cancellationToken) ?? throw new InvalidOperationException("冲销日期没有对应的业务年度。");
        var reversal = new EmployeeFinancialAdjustment
        {
            EmployeeId = original.EmployeeId,
            BusinessYearId = businessYear.Id,
            AdjustmentDate = reversalDate,
            Amount = -original.Amount,
            AdjustmentType = EmployeeFinancialAdjustmentType.Reversal,
            Notes = NormalizeRequired(notes, nameof(notes)),
            ReversalOfId = original.Id
        };
        db.EmployeeFinancialAdjustments.Add(reversal);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(reversal);
    }

    public async Task<EmployeeAnnualLedgerDto> GetAnnualLedgerAsync(
        Guid employeeId,
        Guid businessYearId,
        CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(item => item.Id == employeeId, cancellationToken))
        {
            throw new InvalidOperationException("员工不存在。");
        }

        var businessYear = await db.BusinessYears.AsNoTracking().SingleOrDefaultAsync(item => item.Id == businessYearId, cancellationToken)
            ?? throw new InvalidOperationException("业务年度不存在。");
        var payables = new List<AnnualLedgerPayableInput>();
        var receipts = new List<AnnualLedgerReceiptInput>();

        var newWages = await db.EmployeeWageEntries.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .Select(item => new
            {
                item.Id,
                item.StartDate,
                item.EndDate,
                item.FinalAmount,
                item.SourcePayrollItemId,
                item.WageCategory,
                item.ProjectId,
                item.LaborBusinessPartnerId,
                item.Notes
            })
            .ToListAsync(cancellationToken);
        payables.AddRange(newWages.Select(item => new AnnualLedgerPayableInput(item.Id, AnnualLedgerEntryCategory.Wage, item.StartDate, item.FinalAmount)));
        var linkedPayrollItemIds = newWages.Where(item => item.SourcePayrollItemId.HasValue).Select(item => item.SourcePayrollItemId!.Value).ToArray();
        var payrollItems = await db.PayrollItems.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId && item.Batch.Status != PayrollBatchStatus.Voided && !linkedPayrollItemIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Batch.StartDate, item.Nature, item.Amount })
            .ToListAsync(cancellationToken);
        payables.AddRange(payrollItems.Select(item => new AnnualLedgerPayableInput(
            item.Id,
            AnnualLedgerEntryCategory.Wage,
            item.StartDate,
            item.Nature == PayrollItemNature.Deduction ? -item.Amount : item.Amount)));

        var expenses = await db.ExpenseRecords.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId && !item.IsVoided)
            .Select(item => new { item.Id, item.ExpenseDate, item.Amount })
            .ToListAsync(cancellationToken);
        payables.AddRange(expenses.Select(item => new AnnualLedgerPayableInput(item.Id, AnnualLedgerEntryCategory.Expense, item.ExpenseDate, item.Amount)));
        var expenseReversals = await db.ExpensePayments.AsNoTracking()
            .Where(item => item.Expense.EmployeeId == employeeId && item.RecordKind == EmployeeLedgerRecordKind.RefundOrReversal)
            .Select(item => new { item.Id, item.PaymentDate, item.Amount })
            .ToListAsync(cancellationToken);
        payables.AddRange(expenseReversals.Select(item => new AnnualLedgerPayableInput(item.Id, AnnualLedgerEntryCategory.Expense, item.PaymentDate, item.Amount)));

        var otherPayables = await db.EmployeeOtherPayments.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId && item.RecordKind == EmployeeLedgerRecordKind.Payable)
            .Select(item => new { item.Id, item.EntryDate, item.Amount })
            .ToListAsync(cancellationToken);
        payables.AddRange(otherPayables.Select(item => new AnnualLedgerPayableInput(item.Id, AnnualLedgerEntryCategory.OtherPayable, item.EntryDate, item.Amount)));
        var otherReversals = await db.EmployeeOtherPayments.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId && item.RecordKind == EmployeeLedgerRecordKind.RefundOrReversal)
            .Select(item => new { item.Id, item.EntryDate, item.Amount })
            .ToListAsync(cancellationToken);
        payables.AddRange(otherReversals.Select(item => new AnnualLedgerPayableInput(item.Id, AnnualLedgerEntryCategory.OtherPayable, item.EntryDate, item.Amount)));

        var advanceRepayments = await db.EmployeeAdvances.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId && item.Action == EmployeeAdvanceAction.Repayment)
            .Select(item => new { item.Id, item.EntryDate, item.Amount })
            .ToListAsync(cancellationToken);
        payables.AddRange(advanceRepayments.Select(item => new AnnualLedgerPayableInput(item.Id, AnnualLedgerEntryCategory.Adjustment, item.EntryDate, item.Amount)));
        var adjustments = await db.EmployeeFinancialAdjustments.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .Select(item => new { item.Id, item.AdjustmentDate, item.Amount, item.AdjustmentType })
            .ToListAsync(cancellationToken);
        payables.AddRange(adjustments.Select(item => new AnnualLedgerPayableInput(
            item.Id,
            item.AdjustmentType == EmployeeFinancialAdjustmentType.HistoricalOpeningBalance
                ? AnnualLedgerEntryCategory.OpeningBalance
                : AnnualLedgerEntryCategory.Adjustment,
            item.AdjustmentDate,
            item.Amount)));

        var payrollReceiptRows = await db.PayrollPayments.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId && item.Batch.Status != PayrollBatchStatus.Voided && (item.Batch.PaymentDate.HasValue || item.PaymentDate.HasValue))
            .Select(item => new
            {
                item.Id,
                item.PayrollBatchId,
                item.Batch.BatchNumber,
                item.Batch.IsUnifiedDisbursement,
                ReceiptDate = item.Batch.PaymentDate ?? item.PaymentDate!.Value,
                item.Amount,
                Recipient = item.RecipientNameSnapshot ?? item.PayeeName,
                item.Notes,
                item.PaymentCategory
            })
            .ToListAsync(cancellationToken);
        receipts.AddRange(payrollReceiptRows.Select(item => new AnnualLedgerReceiptInput(item.Id, item.ReceiptDate, item.Amount)));
        var expenseReceipts = await db.ExpensePayments.AsNoTracking()
            .Where(item => item.Expense.EmployeeId == employeeId && item.RecordKind == EmployeeLedgerRecordKind.Payment)
            .Select(item => new AnnualLedgerReceiptInput(item.Id, item.PaymentDate, item.Amount))
            .ToListAsync(cancellationToken);
        receipts.AddRange(expenseReceipts);
        var otherReceipts = await db.EmployeeOtherPayments.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId && item.RecordKind == EmployeeLedgerRecordKind.Payment)
            .Select(item => new AnnualLedgerReceiptInput(item.Id, item.EntryDate, item.Amount))
            .ToListAsync(cancellationToken);
        receipts.AddRange(otherReceipts);
        var advanceReceipts = await db.EmployeeAdvances.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId &&
                (item.Action == EmployeeAdvanceAction.Disbursement || item.Action == EmployeeAdvanceAction.PayrollDeduction))
            .Select(item => new AnnualLedgerReceiptInput(item.Id, item.EntryDate, item.Amount))
            .ToListAsync(cancellationToken);
        receipts.AddRange(advanceReceipts);
        var directReceipts = await db.EmployeeReceipts.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .Select(item => new AnnualLedgerReceiptInput(item.Id, item.ReceiptDate, item.Amount))
            .ToListAsync(cancellationToken);
        receipts.AddRange(directReceipts);

        var summary = EmployeeAnnualLedgerCalculator.Calculate(
            businessYear.StartDate,
            businessYear.EndDate,
            payables,
            receipts);
        var payableLines = payables
            .OrderBy(item => item.EntryDate)
            .ThenBy(item => item.SourceId)
            .Select(item =>
            {
                var wage = newWages.SingleOrDefault(wageItem => wageItem.Id == item.SourceId);
                var adjustment = adjustments.SingleOrDefault(adjustmentItem => adjustmentItem.Id == item.SourceId);
                return new EmployeeAnnualLedgerPayableLineDto(
                    item.SourceId,
                    item.Category,
                    wage is not null ? "EmployeeWageEntry" : adjustment is not null ? "EmployeeFinancialAdjustment" : item.Category.ToString(),
                    item.EntryDate,
                    wage?.EndDate,
                    item.Amount,
                    wage?.Notes,
                    wage is not null &&
                    wage.WageCategory == EmployeeWageCategory.MigrantWorkerWage &&
                    !wage.ProjectId.HasValue &&
                    !wage.LaborBusinessPartnerId.HasValue);
            })
            .ToArray();
        var receiptLines = receipts
            .OrderBy(item => item.ReceiptDate)
            .ThenBy(item => item.ReceiptId)
            .Select(item =>
            {
                var payroll = payrollReceiptRows.SingleOrDefault(source => source.Id == item.ReceiptId);
                return new EmployeeAnnualLedgerReceiptLineDto(
                    item.ReceiptId,
                    payroll is null ? "Receipt" : payroll.IsUnifiedDisbursement ? "PayrollDisbursement" : "PayrollPayment",
                    item.ReceiptDate,
                    item.Amount,
                    payroll?.Recipient,
                    payroll?.Notes,
                    payroll?.PayrollBatchId,
                    payroll?.Id,
                    payroll?.PaymentCategory ?? PayrollPaymentCategory.Other);
            })
            .ToArray();
        return new EmployeeAnnualLedgerDto(employeeId, businessYearId, summary, payableLines, receiptLines);
    }

    private async Task ValidateDimensionsAsync(
        Guid? legalEntityId,
        Guid? projectId,
        Guid? laborBusinessPartnerId,
        CancellationToken cancellationToken)
    {
        if (legalEntityId.HasValue && !await db.LegalEntities.AnyAsync(item => item.Id == legalEntityId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("公司不存在或已停用。");
        }

        if (projectId.HasValue && !await db.Projects.AnyAsync(item => item.Id == projectId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("项目不存在或已停用。");
        }

        if (laborBusinessPartnerId.HasValue && !await db.BusinessPartnerRoles.AnyAsync(item =>
                item.BusinessPartnerId == laborBusinessPartnerId &&
                item.RoleType == BusinessPartnerRoleType.ConstructionCrew &&
                item.Partner.IsActive,
                cancellationToken))
        {
            throw new InvalidOperationException("劳务单位不存在、已停用或没有施工班组角色。");
        }
    }

    private static EmployeeWageEntryDto ToDto(EmployeeWageEntry item) =>
        new(
            item.Id,
            item.EmployeeId,
            item.BusinessYearId,
            item.StartDate,
            item.EndDate,
            item.WageCategory,
            item.CalculationMethod,
            item.Nature,
            item.Quantity,
            item.Unit,
            item.UnitPrice,
            item.AutomaticAmount,
            item.LegalEntityId,
            item.ProjectId,
            item.LaborBusinessPartnerId,
            item.AdjustmentAmount,
            item.FinalAmount,
            item.Notes,
            item.WageCategory == EmployeeWageCategory.MigrantWorkerWage && !item.LaborBusinessPartnerId.HasValue,
            item.EntryType,
            item.AttachmentId,
            item.Attachment?.OriginalFileName,
            item.SourcePersonalAdvanceBatchId,
            item.IsSystemGenerated,
            item.ExcludeFromWageCost,
            item.ConcurrencyStamp,
            item.LegalEntity?.Name,
            item.Project?.Name,
            item.LaborBusinessPartner?.Name);

    private async Task<Attachment?> SaveAttachmentAsync(
        EmployeePayableAttachmentUpload? upload,
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

    private static object WageSnapshot(EmployeeWageEntry item) => new
    {
        item.StartDate,
        item.EndDate,
        item.EntryType,
        item.WageCategory,
        item.CalculationMethod,
        item.Nature,
        item.Quantity,
        item.Unit,
        item.UnitPrice,
        item.AutomaticAmount,
        item.LegalEntityId,
        item.ProjectId,
        item.LaborBusinessPartnerId,
        item.AdjustmentAmount,
        item.FinalAmount,
        item.AttachmentId,
        item.Notes
    };

    private static void EnsureMigrantLaborCompany(EmployeeWageCategory wageCategory, Guid? laborBusinessPartnerId)
    {
        if (wageCategory == EmployeeWageCategory.MigrantWorkerWage && !laborBusinessPartnerId.HasValue)
        {
            throw new ArgumentException("民工工资必须关联劳务公司。", nameof(laborBusinessPartnerId));
        }
    }

    private static EmployeeFinancialAdjustmentDto ToDto(EmployeeFinancialAdjustment item) =>
        new(item.Id, item.EmployeeId, item.BusinessYearId, item.AdjustmentDate, item.Amount, item.AdjustmentType, item.Notes, item.ReversalOfId);

    private static EmployeeReceiptDto ToDto(EmployeeReceipt item) =>
        new(
            item.Id,
            item.EmployeeId,
            item.BusinessYearId,
            item.ReceiptDate,
            item.ReceiptType,
            item.Amount,
            item.PaymentLegalEntityId,
            item.AccountId,
            item.PaymentMethod,
            item.ActualRecipientName,
            item.ProjectId,
            item.LaborBusinessPartnerId,
            item.Notes);

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
