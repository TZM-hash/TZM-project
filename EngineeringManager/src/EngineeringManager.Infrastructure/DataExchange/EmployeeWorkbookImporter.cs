using System.Globalization;
using System.Text.RegularExpressions;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Personnel;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.DataExchange;

internal sealed class EmployeeWorkbookImporter(ApplicationDbContext db)
{
    private static readonly DateOnly ImportYearStart = new(2026, 1, 1);
    private static readonly DateOnly ImportYearEnd = new(2026, 12, 31);
    private const string ImportYearName = "2026年度";

    public async Task<EmployeeWorkbookAnalysis> AnalyzeAsync(
        IReadOnlyList<SimpleXlsxSheet> sheets,
        string sourceFileName,
        CancellationToken cancellationToken)
    {
        var errors = new List<ImportErrorDto>();
        var masterSheet = sheets.SingleOrDefault(item => string.Equals(item.Name.Trim(), "员工总表", StringComparison.OrdinalIgnoreCase));
        if (masterSheet is null)
        {
            return new EmployeeWorkbookAnalysis(0, [new ImportErrorDto(1, "员工总表", "完整员工工作簿必须包含“员工总表”工作表。", null)], [], [], null, null);
        }

        var masterHeaders = HeaderMap(masterSheet.Rows.Count > 0 ? masterSheet.Rows[0] : []);
        var requiredColumns = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["name"] = ["姓名"],
            ["identity"] = ["身份证号"],
            ["actual"] = ["实际应付工资"],
            ["paid"] = ["已付"],
            ["unpaid"] = ["未付"]
        };
        var masterColumnIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var required in requiredColumns)
        {
            var index = FindColumn(masterHeaders, required.Value);
            if (!index.HasValue)
            {
                errors.Add(new ImportErrorDto(1, required.Value[0], "员工总表缺少必要列。", null));
            }
            else
            {
                masterColumnIndexes[required.Key] = index.Value;
            }
        }

        if (errors.Count > 0)
        {
            return new EmployeeWorkbookAnalysis(0, errors, [], [], null, null);
        }

        AddOptionalColumn(masterHeaders, masterColumnIndexes, "phone", "联系电话");
        AddOptionalColumn(masterHeaders, masterColumnIndexes, "position", "工种");
        AddOptionalColumn(masterHeaders, masterColumnIndexes, "start", "开工时间");
        AddOptionalColumn(masterHeaders, masterColumnIndexes, "end", "最后一天上班时间");
        AddOptionalColumn(masterHeaders, masterColumnIndexes, "salary", "工资");
        AddOptionalColumn(masterHeaders, masterColumnIndexes, "unit", "工资（单位）", "工资(单位)");
        AddOptionalColumn(masterHeaders, masterColumnIndexes, "attendance", "全勤工资");
        AddOptionalColumn(masterHeaders, masterColumnIndexes, "leaveDeduction", "请假扣除");
        AddOptionalColumn(masterHeaders, masterColumnIndexes, "reimbursement", "应付报销款及加班费");
        AddOptionalColumn(masterHeaders, masterColumnIndexes, "bonus", "年终分红");
        AddOptionalColumn(masterHeaders, masterColumnIndexes, "notes", "备注");
        AddOptionalColumn(masterHeaders, masterColumnIndexes, "bankAccount", "银行卡号");

        var masterRows = new List<EmployeeWorkbookMasterRow>();
        for (var index = 1; index < masterSheet.Rows.Count; index++)
        {
            var row = masterSheet.Rows[index];
            var name = TextAt(row, masterColumnIndexes["name"]);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var rowNumber = index + 1;
            var identity = TextAt(row, masterColumnIndexes["identity"]);
            var salary = ReadAmount(row, masterColumnIndexes, "salary", rowNumber, "工资", errors, allowText: true);
            var attendance = ReadAmount(row, masterColumnIndexes, "attendance", rowNumber, "全勤工资", errors);
            var leaveDeduction = ReadAmount(row, masterColumnIndexes, "leaveDeduction", rowNumber, "请假扣除", errors);
            var reimbursement = ReadAmount(row, masterColumnIndexes, "reimbursement", rowNumber, "应付报销款及加班费", errors);
            var bonus = ReadAmount(row, masterColumnIndexes, "bonus", rowNumber, "年终分红", errors);
            var actual = ReadAmount(row, masterColumnIndexes, "actual", rowNumber, "实际应付工资", errors);
            var paid = ReadAmount(row, masterColumnIndexes, "paid", rowNumber, "已付", errors) ?? 0m;
            var unpaid = ReadAmount(row, masterColumnIndexes, "unpaid", rowNumber, "未付", errors) ?? 0m;
            if (!actual.HasValue)
            {
                actual = RoundMoney((attendance ?? 0m) - (leaveDeduction ?? 0m) + (reimbursement ?? 0m) + (bonus ?? 0m));
            }

            masterRows.Add(new EmployeeWorkbookMasterRow(
                rowNumber,
                name,
                identity,
                TextAt(row, masterColumnIndexes.GetValueOrDefault("phone", -1)),
                TextAt(row, masterColumnIndexes.GetValueOrDefault("position", -1)),
                ParseDate(ValueAt(row, masterColumnIndexes.GetValueOrDefault("start", -1))),
                ParseDate(ValueAt(row, masterColumnIndexes.GetValueOrDefault("end", -1))),
                salary,
                TextAt(row, masterColumnIndexes.GetValueOrDefault("unit", -1)),
                attendance,
                leaveDeduction,
                reimbursement,
                bonus,
                actual.Value,
                paid,
                unpaid,
                TextAt(row, masterColumnIndexes.GetValueOrDefault("notes", -1)),
                TextAt(row, masterColumnIndexes.GetValueOrDefault("bankAccount", -1))));
        }

        var masterByKey = masterRows
            .GroupBy(item => EmployeeKey(item.IdentityNumber, item.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var details = ParseDetails(sheets, masterRows, masterByKey, errors);
        var paymentAccount = await db.FinancialAccounts.AsNoTracking()
            .Include(item => item.LegalEntity)
            .Where(item => item.IsActive && item.LegalEntity.IsActive)
            .OrderByDescending(item => item.IsDefaultPayment)
            .ThenBy(item => item.LegalEntity.Code)
            .ThenBy(item => item.AccountName)
            .Select(item => new EmployeeWorkbookPaymentContext(item.Id, item.LegalEntityId))
            .FirstOrDefaultAsync(cancellationToken);

        var requiresPaymentAccount = masterRows.Any(item => item.PaidAmount > 0m) || details.Any(item => item.Paid > 0m);
        if (requiresPaymentAccount && paymentAccount is null)
        {
            errors.Add(new ImportErrorDto(1, "付款账户", "员工工作簿包含已付款数据，但系统没有可用的公司付款账户。", null));
        }

        var requiresLegalEntity = details.Any(item => item.Payable > 0m);
        if (requiresLegalEntity && paymentAccount is null)
        {
            errors.Add(new ImportErrorDto(1, "付款公司", "员工工作簿包含应付款明细，但系统没有可用的付款公司。", null));
        }

        return new EmployeeWorkbookAnalysis(
            masterRows.Count + details.Count,
            errors,
            masterRows,
            details,
            paymentAccount?.AccountId,
            paymentAccount?.LegalEntityId);
    }

    private static List<EmployeeWorkbookDetailRow> ParseDetails(
        IReadOnlyList<SimpleXlsxSheet> sheets,
        IReadOnlyList<EmployeeWorkbookMasterRow> masterRows,
        IReadOnlyDictionary<string, EmployeeWorkbookMasterRow> masterByKey,
        List<ImportErrorDto> errors)
    {
        var details = new List<EmployeeWorkbookDetailRow>();
        foreach (var sheet in sheets.Where(item => !string.Equals(item.Name.Trim(), "员工总表", StringComparison.OrdinalIgnoreCase)))
        {
            var detailHeaderIndex = FindDetailHeaderIndex(sheet.Rows);
            if (!detailHeaderIndex.HasValue)
            {
                continue;
            }

            var detailHeaders = HeaderMap(sheet.Rows[detailHeaderIndex.Value]);
            var monthColumn = FindColumn(detailHeaders, "月份");
            var payableColumn = FindColumn(detailHeaders, "应付报销款");
            var paymentDateColumn = FindColumn(detailHeaders, "公司付款日期");
            var paidColumn = FindColumn(detailHeaders, "公司已付款");
            var noteColumn = FindColumn(detailHeaders, "备注");
            if (!monthColumn.HasValue || !payableColumn.HasValue || !paymentDateColumn.HasValue || !paidColumn.HasValue)
            {
                continue;
            }

            var participants = ReadParticipants(sheet, detailHeaderIndex.Value, masterRows, masterByKey);
            if (participants.Count == 0)
            {
                participants = masterRows
                    .Where(item => sheet.Name.Contains(item.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(item => new EmployeeWorkbookParticipant(EmployeeKey(item.IdentityNumber, item.Name), item.Name, item.IdentityNumber, item.Phone, item.PositionTitle, item.PaidAmount))
                    .ToList();
            }

            var candidateRows = new List<EmployeeWorkbookRawDetail>();
            for (var rowIndex = detailHeaderIndex.Value + 1; rowIndex < sheet.Rows.Count; rowIndex++)
            {
                var row = sheet.Rows[rowIndex];
                var monthText = TextAt(row, monthColumn.Value);
                var noteText = noteColumn.HasValue ? TextAt(row, noteColumn.Value) : string.Empty;
                if (IsTotalRow(row))
                {
                    break;
                }

                var rawPayable = ValueAt(row, payableColumn.Value);
                var rawPaid = ValueAt(row, paidColumn.Value);
                if (!HasValue(rawPayable) && !HasValue(rawPaid))
                {
                    continue;
                }

                var payable = ReadAmount(rawPayable, rowIndex + 1, "应付报销款", errors);
                var paid = ReadAmount(rawPaid, rowIndex + 1, "公司已付款", errors);
                if (payable is null && paid is null)
                {
                    continue;
                }

                var payableAmount = RoundMoney(payable ?? 0m);
                var paidAmount = RoundMoney(paid ?? 0m);
                if (payableAmount == 0m && paidAmount == 0m)
                {
                    continue;
                }

                var date = ParseDate(ValueAt(row, paymentDateColumn.Value));
                candidateRows.Add(new EmployeeWorkbookRawDetail(
                    rowIndex + 1,
                    monthText,
                    noteText,
                    payableAmount,
                    paidAmount,
                    NormalizeDate(date, monthText, paid > 0m ? ImportYearEnd : ImportYearStart)));
            }

            var payableOwner = FindOwnerByMasterAmount(participants, masterByKey, candidateRows.Sum(item => item.Payable));
            var paidSoFar = participants.ToDictionary(item => item.EmployeeKey, _ => 0m, StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidateRows)
            {
                var owner = ResolveOwner(participants, masterByKey, candidate, payableOwner, paidSoFar);
                if (owner is null)
                {
                    errors.Add(new ImportErrorDto(candidate.RowNumber, "姓名", "无法从员工明细工作表确定归属员工。", sheet.Name));
                    continue;
                }

                if (paidSoFar.TryGetValue(owner.EmployeeKey, out var currentPaid))
                {
                    paidSoFar[owner.EmployeeKey] = RoundMoney(currentPaid + candidate.Paid);
                }

                details.Add(new EmployeeWorkbookDetailRow(
                    owner.EmployeeKey,
                    owner.Name,
                    owner.IdentityNumber,
                    sheet.Name,
                    candidate.RowNumber,
                    candidate.MonthLabel,
                    candidate.Payable,
                    candidate.Paid,
                    candidate.Date,
                    candidate.Note));
            }
        }

        return details;
    }

    private static List<EmployeeWorkbookParticipant> ReadParticipants(
        SimpleXlsxSheet sheet,
        int detailHeaderIndex,
        IReadOnlyList<EmployeeWorkbookMasterRow> masterRows,
        IReadOnlyDictionary<string, EmployeeWorkbookMasterRow> masterByKey)
    {
        var result = new List<EmployeeWorkbookParticipant>();
        var topHeaders = HeaderMap(sheet.Rows.Count > 0 ? sheet.Rows[0] : []);
        var nameColumn = FindColumn(topHeaders, "姓名");
        var identityColumn = FindColumn(topHeaders, "身份证号");
        var phoneColumn = FindColumn(topHeaders, "联系电话");
        var positionColumn = FindColumn(topHeaders, "工种");
        if (nameColumn.HasValue)
        {
            for (var index = 1; index < detailHeaderIndex; index++)
            {
                var row = sheet.Rows[index];
                var name = TextAt(row, nameColumn.Value);
                if (string.IsNullOrWhiteSpace(name) || IsParticipantSectionLabel(name))
                {
                    continue;
                }

                var identity = identityColumn.HasValue ? TextAt(row, identityColumn.Value) : string.Empty;
                var key = EmployeeKey(identity, name);
                var master = masterByKey.GetValueOrDefault(key);
                result.Add(new EmployeeWorkbookParticipant(
                    key,
                    name,
                    identity,
                    phoneColumn.HasValue ? TextAt(row, phoneColumn.Value) : master?.Phone,
                    positionColumn.HasValue ? TextAt(row, positionColumn.Value) : master?.PositionTitle,
                    master?.PaidAmount ?? 0m));
            }
        }

        return result
            .GroupBy(item => item.EmployeeKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static EmployeeWorkbookParticipant? ResolveOwner(
        List<EmployeeWorkbookParticipant> participants,
        IReadOnlyDictionary<string, EmployeeWorkbookMasterRow> masterByKey,
        EmployeeWorkbookRawDetail candidate,
        EmployeeWorkbookParticipant? payableOwner,
        IReadOnlyDictionary<string, decimal> paidSoFar)
    {
        if (participants.Count == 0)
        {
            return null;
        }

        var text = $"{candidate.MonthLabel}{candidate.Note}";
        var named = participants
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) && text.Contains(item.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Name.Length)
            .FirstOrDefault();
        if (named is not null)
        {
            return named;
        }

        if (participants.Count == 1)
        {
            return participants[0];
        }

        if (candidate.Payable > 0m && payableOwner is not null)
        {
            return payableOwner;
        }

        return participants
            .Where(item => masterByKey.TryGetValue(item.EmployeeKey, out var master) &&
                master.PaidAmount > 0m &&
                paidSoFar.GetValueOrDefault(item.EmployeeKey) + candidate.Paid <= master.PaidAmount + 0.01m)
            .OrderBy(item => paidSoFar.GetValueOrDefault(item.EmployeeKey))
            .FirstOrDefault()
            ?? participants[0];
    }

    private static EmployeeWorkbookParticipant? FindOwnerByMasterAmount(
        IReadOnlyList<EmployeeWorkbookParticipant> participants,
        IReadOnlyDictionary<string, EmployeeWorkbookMasterRow> masterByKey,
        decimal amount)
    {
        if (amount <= 0m)
        {
            return null;
        }

        return participants.FirstOrDefault(item =>
            masterByKey.TryGetValue(item.EmployeeKey, out var master) &&
            Math.Abs(master.ReimbursementAmount.GetValueOrDefault() - amount) <= 0.01m);
    }

    public async Task ApplyAsync(
        EmployeeWorkbookAnalysis analysis,
        string sourceFileName,
        CancellationToken cancellationToken)
    {
        var businessYear = await db.BusinessYears.SingleOrDefaultAsync(item =>
                item.StartDate == ImportYearStart && item.EndDate == ImportYearEnd,
                cancellationToken)
            ?? await db.BusinessYears.SingleOrDefaultAsync(item => item.Name == ImportYearName, cancellationToken);
        if (businessYear is null)
        {
            businessYear = new BusinessYear { Name = ImportYearName, StartDate = ImportYearStart, EndDate = ImportYearEnd };
            db.BusinessYears.Add(businessYear);
        }

        var employees = await db.Employees
            .Include(item => item.Person)
            .ThenInclude(item => item!.ConstructionWorker)
            .Include(item => item.Person)
            .ThenInclude(item => item!.EngagementHistory)
            .ToListAsync(cancellationToken);
        var employeeByKey = BuildEmployeeMap(employees);
        var sourceName = Path.GetFileName(sourceFileName);
        var detailByEmployee = analysis.Details
            .GroupBy(item => item.EmployeeKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        foreach (var master in analysis.MasterRows)
        {
            var key = EmployeeKey(master.IdentityNumber, master.Name);
            var employee = FindOrCreateEmployee(employeeByKey, master.Name, master.IdentityNumber, master.Phone, master.PositionTitle, master.BankAccountNumber);
            ApplyEmployeeProfile(employee, master, analysis.PaymentLegalEntityId);

            detailByEmployee.TryGetValue(key, out var employeeDetails);
            employeeDetails ??= [];
            var detailPayableTotal = RoundMoney(employeeDetails.Sum(item => item.Payable));
            var detailPaidTotal = RoundMoney(employeeDetails.Sum(item => item.Paid));
            var wageAmount = RoundMoney(master.ActualAmount - master.ReimbursementAmount.GetValueOrDefault());

            if (master.HasWageData)
            {
                UpsertWageEntry(employee, businessYear, master, wageAmount, sourceName);
            }

            foreach (var detail in employeeDetails.Where(item => item.Payable > 0m))
            {
                UpsertOtherPayment(employee, analysis.PaymentLegalEntityId, detail, sourceName);
            }

            foreach (var detail in employeeDetails.Where(item => item.Paid > 0m))
            {
                UpsertReceipt(employee, businessYear, analysis.PaymentContext, detail, sourceName);
            }

            var summaryPaidDifference = RoundMoney(master.PaidAmount - detailPaidTotal);
            if (summaryPaidDifference > 0m)
            {
                var summaryDetail = new EmployeeWorkbookDetailRow(
                    key,
                    employee.Name,
                    employee.IdentityNumber ?? string.Empty,
                    "员工总表",
                    master.RowNumber,
                    "主表已付汇总",
                    0m,
                    summaryPaidDifference,
                    ImportYearEnd,
                    "员工总表已付金额与明细合计的差额");
                UpsertReceipt(employee, businessYear, analysis.PaymentContext, summaryDetail, sourceName);
                detailPaidTotal = RoundMoney(detailPaidTotal + summaryPaidDifference);
            }

            var calculatedUnpaid = RoundMoney(wageAmount + detailPayableTotal - detailPaidTotal);
            var adjustmentAmount = RoundMoney(master.UnpaidAmount - calculatedUnpaid);
            UpsertReconciliationAdjustment(employee, businessYear, master, adjustmentAmount, sourceName);
        }

        foreach (var detailOnly in analysis.Details.Select(item => item.EmployeeKey).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (analysis.MasterRows.Any(item => string.Equals(EmployeeKey(item.IdentityNumber, item.Name), detailOnly, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var detail = analysis.Details.First(item => string.Equals(item.EmployeeKey, detailOnly, StringComparison.OrdinalIgnoreCase));
            var employee = FindOrCreateEmployee(employeeByKey, detail.EmployeeName, detail.IdentityNumber, null, null, null);
            foreach (var payable in analysis.Details.Where(item => string.Equals(item.EmployeeKey, detailOnly, StringComparison.OrdinalIgnoreCase) && item.Payable > 0m))
            {
                UpsertOtherPayment(employee, analysis.PaymentLegalEntityId, payable, sourceName);
            }

            foreach (var paid in analysis.Details.Where(item => string.Equals(item.EmployeeKey, detailOnly, StringComparison.OrdinalIgnoreCase) && item.Paid > 0m))
            {
                UpsertReceipt(employee, businessYear, analysis.PaymentContext, paid, sourceName);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private void UpsertWageEntry(
        Employee employee,
        BusinessYear businessYear,
        EmployeeWorkbookMasterRow master,
        decimal amount,
        string sourceFileName)
    {
        var marker = Marker(sourceFileName, "员工总表", master.RowNumber, "工资");
        var entry = db.EmployeeWageEntries.SingleOrDefault(item => item.Notes != null && item.Notes.Contains(marker));
        if (entry is null)
        {
            entry = new EmployeeWageEntry { Employee = employee, BusinessYear = businessYear };
            db.EmployeeWageEntries.Add(entry);
        }

        var startDate = NormalizeDate(master.StartDate, null, ImportYearStart);
        var endDate = NormalizeDate(master.EndDate, null, ImportYearEnd);
        var automaticAmount = RoundMoney(master.AttendanceAmount ?? master.SalaryAmount ?? 0m);
        entry.EmployeeId = employee.Id;
        entry.BusinessYearId = businessYear.Id;
        entry.StartDate = startDate;
        entry.EndDate = endDate < startDate ? startDate : endDate;
        entry.EntryType = master.BonusAmount > 0m ? EmployeeWageEntryType.Bonus : EmployeeWageEntryType.Attendance;
        entry.WageCategory = EmployeeWageCategory.SocialSecurityWage;
        entry.CalculationMethod = CalculationMethod(master.SalaryUnit);
        entry.Nature = amount < 0m ? PayrollItemNature.Deduction : PayrollItemNature.Earning;
        entry.Quantity = null;
        entry.Unit = master.SalaryUnit;
        entry.UnitPrice = master.SalaryAmount;
        entry.AutomaticAmount = automaticAmount;
        entry.AdjustmentAmount = RoundMoney(amount - automaticAmount);
        entry.FinalAmount = amount;
        entry.Notes = Limit($"{marker};公式=全勤工资-请假扣除+应付报销款及年终分红-已拆分明细;结果={master.ActualAmount:0.00};备注={master.Notes}", 1000);
        entry.IsSystemGenerated = false;
        entry.ExcludeFromWageCost = false;
        entry.ConcurrencyStamp = Guid.NewGuid();
    }

    private void UpsertOtherPayment(
        Employee employee,
        Guid? legalEntityId,
        EmployeeWorkbookDetailRow detail,
        string sourceFileName)
    {
        if (!legalEntityId.HasValue || detail.Payable <= 0m)
        {
            return;
        }

        var marker = Marker(sourceFileName, detail.SheetName, detail.RowNumber, "应付款");
        var payment = db.EmployeeOtherPayments.SingleOrDefault(item => item.Description != null && item.Description.Contains(marker));
        if (payment is null)
        {
            payment = new EmployeeOtherPayment { Employee = employee };
            db.EmployeeOtherPayments.Add(payment);
        }

        payment.EmployeeId = employee.Id;
        payment.LegalEntityId = legalEntityId.Value;
        payment.EntryType = detail.MonthLabel.Contains("借款", StringComparison.OrdinalIgnoreCase) ? EmployeeLedgerEntryType.Other : EmployeeLedgerEntryType.Expense;
        payment.RecordKind = EmployeeLedgerRecordKind.Payable;
        payment.EntryDate = detail.Date;
        payment.Amount = detail.Payable;
        payment.Description = Limit($"{marker};项目={detail.MonthLabel};说明={detail.Note}", 500);
        payment.ConcurrencyStamp = Guid.NewGuid();
    }

    private void UpsertReceipt(
        Employee employee,
        BusinessYear businessYear,
        EmployeeWorkbookPaymentContext? paymentContext,
        EmployeeWorkbookDetailRow detail,
        string sourceFileName)
    {
        if (paymentContext is null || detail.Paid <= 0m)
        {
            return;
        }

        var marker = Marker(sourceFileName, detail.SheetName, detail.RowNumber, "已付款");
        var receipt = db.EmployeeReceipts.SingleOrDefault(item => item.Notes != null && item.Notes.Contains(marker));
        if (receipt is null)
        {
            receipt = new EmployeeReceipt { Employee = employee };
            db.EmployeeReceipts.Add(receipt);
        }

        receipt.EmployeeId = employee.Id;
        receipt.BusinessYearId = businessYear.Id;
        receipt.ReceiptDate = detail.Date;
        receipt.ReceiptType = ReceiptType(detail.Note);
        receipt.Amount = detail.Paid;
        receipt.PaymentLegalEntityId = paymentContext.LegalEntityId;
        receipt.AccountId = paymentContext.AccountId;
        receipt.PaymentMethod = PaymentMethodFromNote(detail.Note);
        receipt.ActualRecipientName = employee.Name;
        receipt.Notes = Limit($"{marker};说明={detail.Note}", 1000);
        receipt.ConcurrencyStamp = Guid.NewGuid();

        AccountTransaction? transaction = null;
        if (receipt.AccountTransactionId.HasValue)
        {
            transaction = db.AccountTransactions.SingleOrDefault(item => item.Id == receipt.AccountTransactionId.Value);
        }

        if (transaction is null)
        {
            transaction = new AccountTransaction
            {
                Direction = AccountTransactionDirection.Outflow,
                SourceType = AccountTransactionSourceType.EmployeeReceipt,
                SourceId = receipt.Id
            };
            receipt.AccountTransactionId = transaction.Id;
            db.AccountTransactions.Add(transaction);
        }

        transaction.AccountId = receipt.AccountId;
        transaction.TransactionDate = receipt.ReceiptDate;
        transaction.Amount = receipt.Amount;
        transaction.Description = $"员工领款：{employee.Name}";
    }

    private void UpsertReconciliationAdjustment(
        Employee employee,
        BusinessYear businessYear,
        EmployeeWorkbookMasterRow master,
        decimal amount,
        string sourceFileName)
    {
        var marker = Marker(sourceFileName, "员工总表", master.RowNumber, "未付校正");
        var adjustment = db.EmployeeFinancialAdjustments.SingleOrDefault(item => item.Notes.Contains(marker));
        if (adjustment is null)
        {
            adjustment = new EmployeeFinancialAdjustment { Employee = employee, BusinessYear = businessYear };
            db.EmployeeFinancialAdjustments.Add(adjustment);
        }

        adjustment.EmployeeId = employee.Id;
        adjustment.BusinessYearId = businessYear.Id;
        adjustment.AdjustmentDate = ImportYearEnd;
        adjustment.Amount = amount;
        adjustment.AdjustmentType = EmployeeFinancialAdjustmentType.AdministratorAdjustment;
        adjustment.Notes = Limit($"{marker};Excel未付={master.UnpaidAmount:0.00};导入后未付校正", 1000);
    }

    private void ApplyEmployeeProfile(Employee employee, EmployeeWorkbookMasterRow master, Guid? defaultLegalEntityId)
    {
        var person = EnsurePerson(employee, master.Name, master.IdentityNumber, master.Phone, master.BankAccountNumber);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentAffiliation = person.EngagementHistory
            .Where(item => item.IsPrimary && item.StartDate <= today && (item.EndDate is null || item.EndDate >= today))
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefault();
        var currentExternalAffiliation = currentAffiliation?.Scope == PersonnelScope.External
            ? currentAffiliation
            : null;
        var currentInternalAffiliation = currentAffiliation?.Scope == PersonnelScope.Internal
            ? currentAffiliation
            : null;
        var profileIsActive = currentExternalAffiliation is not null ? person.IsActive : employee.IsActive;
        PersonPublicDataSynchronizer.Apply(
            person,
            master.Name,
            string.IsNullOrWhiteSpace(master.Phone) ? person.Phone : master.Phone,
            string.IsNullOrWhiteSpace(master.IdentityNumber) ? person.IdentityNumber : master.IdentityNumber,
            string.IsNullOrWhiteSpace(master.BankAccountNumber) ? person.BankAccountNumber : master.BankAccountNumber,
            person.BankName,
            person.Notes,
            profileIsActive);
        PersonPublicDataSynchronizer.ApplyActiveProfile(person, profileIsActive, currentAffiliation);
        if (currentExternalAffiliation is null)
        {
            employee.PositionTitle = string.IsNullOrWhiteSpace(master.PositionTitle) ? employee.PositionTitle : master.PositionTitle;
            employee.HireDate = master.StartDate ?? employee.HireDate;
            employee.LeaveDate = master.EndDate ?? employee.LeaveDate;
        }
        if (master.SalaryAmount.HasValue)
        {
            var unit = master.SalaryUnit;
            if (unit.Contains('日')) employee.DefaultDailyRate = master.SalaryAmount;
            else if (unit.Contains('时')) employee.DefaultHourlyRate = master.SalaryAmount;
            else employee.DefaultMonthlySalary = unit.Contains('年')
                ? RoundMoney(master.SalaryAmount.Value / 12m)
                : master.SalaryAmount;
        }

        if (!employee.DefaultLegalEntityId.HasValue && defaultLegalEntityId.HasValue)
        {
            employee.DefaultLegalEntityId = defaultLegalEntityId;
        }

        if (currentInternalAffiliation is not null)
        {
            currentInternalAffiliation.PositionTitle = employee.PositionTitle;
            if (!currentInternalAffiliation.LegalEntityId.HasValue && employee.DefaultLegalEntityId.HasValue)
            {
                currentInternalAffiliation.LegalEntityId = employee.DefaultLegalEntityId;
            }
        }

        employee.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static Dictionary<string, Employee> BuildEmployeeMap(IEnumerable<Employee> employees)
    {
        var map = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
        foreach (var employee in employees)
        {
            var identityKey = IdentityKey(employee.Person?.IdentityNumber ?? employee.IdentityNumber);
            if (identityKey is not null)
            {
                map[identityKey] = employee;
            }
        }

        return map;
    }

    private Employee FindOrCreateEmployee(
        Dictionary<string, Employee> employeeByKey,
        string name,
        string identityNumber,
        string? phone,
        string? position,
        string? bankAccountNumber)
    {
        var identityKey = IdentityKey(identityNumber);
        if (identityKey is not null && employeeByKey.TryGetValue(identityKey, out var employee))
        {
            return employee;
        }

        var person = new Person
        {
            PersonNumber = $"PER-{Guid.NewGuid():N}"
        };
        employee = new Employee
        {
            Person = person,
            PersonId = person.Id,
            EmployeeNumber = NextEmployeeNumber(),
            PositionTitle = position,
            EmployeeType = EmployeeType.Labor
        };
        person.Employee = employee;
        PersonPublicDataSynchronizer.Apply(person, name, phone, identityNumber, bankAccountNumber, null, null, true);
        person.EngagementHistory.Add(new PersonnelEngagementHistory
        {
            Person = person,
            Scope = PersonnelScope.Internal,
            InternalType = EmployeeType.Labor,
            PositionTitle = position,
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            IsPrimary = true,
            Reason = "完整员工工作簿导入"
        });
        db.People.Add(person);
        if (identityKey is not null) employeeByKey[identityKey] = employee;
        return employee;
    }

    private Person EnsurePerson(Employee employee, string name, string identityNumber, string? phone, string? bankAccountNumber)
    {
        if (employee.Person is not null) return employee.Person;

        var person = new Person
        {
            PersonNumber = $"PER-{Guid.NewGuid():N}",
            Employee = employee
        };
        employee.Person = person;
        employee.PersonId = person.Id;
        PersonPublicDataSynchronizer.Apply(person, name, phone, identityNumber, bankAccountNumber, employee.BankName, employee.Notes, employee.IsActive);
        person.EngagementHistory.Add(new PersonnelEngagementHistory
        {
            Person = person,
            Scope = PersonnelScope.Internal,
            InternalType = employee.EmployeeType,
            PositionTitle = employee.PositionTitle,
            StartDate = employee.HireDate ?? DateOnly.FromDateTime(DateTime.Today),
            IsPrimary = true,
            Reason = "完整员工工作簿补建人员主档"
        });
        db.People.Add(person);
        return person;
    }

    private string NextEmployeeNumber()
    {
        var used = db.Employees.Local.Select(item => item.EmployeeNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; ; index++)
        {
            var number = $"YG{index:0000}";
            if (!used.Contains(number))
            {
                return number;
            }
        }
    }

    private static string EmployeeKey(string? identityNumber, string name) =>
        !string.IsNullOrWhiteSpace(identityNumber)
            ? IdentityKey(identityNumber)!
            : $"姓名:{name.Trim()}";

    private static string? IdentityKey(string? identityNumber)
    {
        var normalized = PersonPublicDataSynchronizer.NormalizeIdentityNumber(identityNumber);
        return normalized is null ? null : $"身份证:{normalized}";
    }

    private static string Marker(string sourceFileName, string sheetName, int rowNumber, string kind) =>
        $"[员工导入:{sourceFileName}|{sheetName}|第{rowNumber}行|{kind}]";

    private static EmployeeWageCalculationMethod CalculationMethod(string unit) =>
        unit.Contains('日') ? EmployeeWageCalculationMethod.Daily :
        unit.Contains('时') ? EmployeeWageCalculationMethod.Hourly :
        unit.Contains('月') ? EmployeeWageCalculationMethod.Monthly :
        EmployeeWageCalculationMethod.FixedAmount;

    private static EmployeeReceiptType ReceiptType(string note) =>
        note.Contains("借款", StringComparison.OrdinalIgnoreCase) ? EmployeeReceiptType.Advance :
        note.Contains("报销", StringComparison.OrdinalIgnoreCase) || note.Contains("体检", StringComparison.OrdinalIgnoreCase) || note.Contains("保养", StringComparison.OrdinalIgnoreCase)
            ? EmployeeReceiptType.Expense
            : EmployeeReceiptType.Wage;

    private static Domain.Finance.PaymentMethod PaymentMethodFromNote(string note) =>
        note.Contains("微信", StringComparison.OrdinalIgnoreCase) ? Domain.Finance.PaymentMethod.WeChat :
        note.Contains("支付宝", StringComparison.OrdinalIgnoreCase) ? Domain.Finance.PaymentMethod.Alipay :
        note.Contains("现金", StringComparison.OrdinalIgnoreCase) ? Domain.Finance.PaymentMethod.Cash :
        Domain.Finance.PaymentMethod.BankTransfer;

    private static int? FindDetailHeaderIndex(IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            var headers = HeaderMap(rows[index]);
            if (FindColumn(headers, "月份").HasValue &&
                FindColumn(headers, "应付报销款").HasValue &&
                FindColumn(headers, "公司付款日期").HasValue &&
                FindColumn(headers, "公司已付款").HasValue)
            {
                return index;
            }
        }

        return null;
    }

    private static bool IsTotalRow(IReadOnlyList<object?> row) =>
        row.Any(value =>
        {
            var text = CellText(value);
            return text.Contains("合计", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("总计", StringComparison.OrdinalIgnoreCase);
        });

    private static bool IsParticipantSectionLabel(string value) => NormalizeHeader(value) switch
    {
        "应付款" or "已付款" or "公司付款日期" or "公司已付款" or "月份" or "备注" => true,
        _ => false
    };

    private static Dictionary<string, int> HeaderMap(IReadOnlyList<object?> row) =>
        row.Select((value, index) => new { Header = NormalizeHeader(TextAt(row, index)), index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Header))
            .GroupBy(item => item.Header, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.OrdinalIgnoreCase);

    private static int? FindColumn(IReadOnlyDictionary<string, int> headers, params string[] names)
    {
        foreach (var name in names)
        {
            if (headers.TryGetValue(NormalizeHeader(name), out var index))
            {
                return index;
            }
        }

        return null;
    }

    private static void AddOptionalColumn(IReadOnlyDictionary<string, int> headers, Dictionary<string, int> target, string key, params string[] names)
    {
        var index = FindColumn(headers, names);
        if (index.HasValue)
        {
            target[key] = index.Value;
        }
    }

    private static string NormalizeHeader(string value) =>
        value.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("　", string.Empty, StringComparison.Ordinal).Trim();

    private static string TextAt(IReadOnlyList<object?> row, int index) =>
        index < 0 || index >= row.Count ? string.Empty : CellText(row[index]);

    private static object? ValueAt(IReadOnlyList<object?> row, int index) =>
        index < 0 || index >= row.Count ? null : row[index];

    private static string CellText(object? value) => value switch
    {
        null => string.Empty,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString(CultureInfo.InvariantCulture),
        float number => number.ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
    };

    private static bool HasValue(object? value) => !string.IsNullOrWhiteSpace(CellText(value));

    private static decimal? ReadAmount(
        IReadOnlyList<object?> row,
        Dictionary<string, int> columns,
        string key,
        int rowNumber,
        string columnName,
        ICollection<ImportErrorDto> errors,
        bool allowText = false)
    {
        return columns.TryGetValue(key, out var index)
            ? ReadAmount(ValueAt(row, index), rowNumber, columnName, errors, allowText)
            : null;
    }

    private static decimal? ReadAmount(object? value, int rowNumber, string columnName, ICollection<ImportErrorDto> errors, bool allowText = false)
    {
        if (!HasValue(value))
        {
            return null;
        }

        var text = CellText(value).Replace(",", string.Empty, StringComparison.Ordinal);
        if (decimal.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var number))
        {
            return RoundMoney(number);
        }

        var match = Regex.Match(text, @"[-+]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant);
        if (match.Success && decimal.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return RoundMoney(number);
        }

        if (allowText)
        {
            return null;
        }

        errors.Add(new ImportErrorDto(rowNumber, columnName, "金额格式无法识别。", text));
        return null;
    }

    private static DateOnly? ParseDate(object? value)
    {
        if (value is DateOnly date)
        {
            return date;
        }

        if (value is DateTime dateTime)
        {
            return DateOnly.FromDateTime(dateTime);
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return DateOnly.FromDateTime(dateTimeOffset.DateTime);
        }

        var text = CellText(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Replace("年", "-", StringComparison.Ordinal)
            .Replace("月", "-", StringComparison.Ordinal)
            .Replace("日", string.Empty, StringComparison.Ordinal)
            .Replace("/", "-", StringComparison.Ordinal)
            .Replace(".", "-", StringComparison.Ordinal);
        text = Regex.Replace(text, "-+", "-", RegexOptions.CultureInvariant).Trim('-');
        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return date;
        }

        var match = Regex.Match(text, @"(?<year>20\d{2})(?:-(?<month>\d{1,2}))?(?:-(?<day>\d{1,2}))?", RegexOptions.CultureInvariant);
        if (match.Success && int.TryParse(match.Groups["year"].Value, out var year))
        {
            var month = match.Groups["month"].Success ? int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture) : 1;
            var day = match.Groups["day"].Success ? int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture) : 1;
            if (month is >= 1 and <= 12 && day is >= 1 and <= 31)
            {
                try
                {
                    return new DateOnly(year, month, day);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }
            }
        }

        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 20000m and < 80000m)
        {
            return DateOnly.FromDateTime(DateTime.FromOADate((double)serial));
        }

        return null;
    }

    private static DateOnly NormalizeDate(DateOnly? date, string? label, DateOnly fallback)
    {
        var parsed = date ?? ParseDate(label);
        if (!parsed.HasValue || parsed.Value < ImportYearStart || parsed.Value > ImportYearEnd)
        {
            return fallback;
        }

        return parsed.Value;
    }

    private static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Limit(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}

internal sealed record EmployeeWorkbookAnalysis(
    int TotalRows,
    IReadOnlyList<ImportErrorDto> Errors,
    IReadOnlyList<EmployeeWorkbookMasterRow> MasterRows,
    IReadOnlyList<EmployeeWorkbookDetailRow> Details,
    Guid? PaymentAccountId,
    Guid? PaymentLegalEntityId)
{
    public EmployeeWorkbookPaymentContext? PaymentContext =>
        PaymentAccountId.HasValue && PaymentLegalEntityId.HasValue
            ? new EmployeeWorkbookPaymentContext(PaymentAccountId.Value, PaymentLegalEntityId.Value)
            : null;
}

internal sealed record EmployeeWorkbookPaymentContext(Guid AccountId, Guid LegalEntityId);

internal sealed record EmployeeWorkbookMasterRow(
    int RowNumber,
    string Name,
    string IdentityNumber,
    string Phone,
    string PositionTitle,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? SalaryAmount,
    string SalaryUnit,
    decimal? AttendanceAmount,
    decimal? LeaveDeductionAmount,
    decimal? ReimbursementAmount,
    decimal? BonusAmount,
    decimal ActualAmount,
    decimal PaidAmount,
    decimal UnpaidAmount,
    string Notes,
    string BankAccountNumber)
{
    public bool HasWageData =>
        SalaryAmount.HasValue ||
        AttendanceAmount.HasValue ||
        LeaveDeductionAmount.HasValue ||
        ReimbursementAmount.HasValue ||
        BonusAmount.HasValue ||
        ActualAmount != 0m;
}

internal sealed record EmployeeWorkbookDetailRow(
    string EmployeeKey,
    string EmployeeName,
    string IdentityNumber,
    string SheetName,
    int RowNumber,
    string MonthLabel,
    decimal Payable,
    decimal Paid,
    DateOnly Date,
    string Note);

internal sealed record EmployeeWorkbookParticipant(
    string EmployeeKey,
    string Name,
    string IdentityNumber,
    string? Phone,
    string? PositionTitle,
    decimal PaidAmount);

internal sealed record EmployeeWorkbookRawDetail(
    int RowNumber,
    string MonthLabel,
    string Note,
    decimal Payable,
    decimal Paid,
    DateOnly Date);
