using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.DataExchange;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class StandardImportTests
{
    [Theory]
    [InlineData(ExportDataset.Employees, "员工编号")]
    [InlineData(ExportDataset.Partners, "单位编号")]
    [InlineData(ExportDataset.Projects, "项目编号")]
    [InlineData(ExportDataset.Companies, "公司编码")]
    [InlineData(ExportDataset.CompanyAccounts, "公司编码")]
    [InlineData(ExportDataset.CompanyCertificates, "公司编码")]
    [InlineData(ExportDataset.EmployeeCertificates, "员工编号")]
    [InlineData(ExportDataset.Equipment, "设备编号")]
    [InlineData(ExportDataset.EquipmentLeases, "设备编号")]
    [InlineData(ExportDataset.EquipmentUsages, "设备编号")]
    [InlineData(ExportDataset.EquipmentPeriods, "设备编号")]
    [InlineData(ExportDataset.EquipmentSettlements, "设备编号")]
    [InlineData(ExportDataset.Contracts, "项目编号")]
    [InlineData(ExportDataset.StageResults, "项目编号")]
    [InlineData((ExportDataset)21, "员工编号")]
    [InlineData((ExportDataset)22, "员工编号")]
    [InlineData((ExportDataset)23, "员工编号")]
    [InlineData((ExportDataset)24, "员工编号")]
    public async Task StandardTemplatesContainExpectedHeaders(ExportDataset dataset, string expectedHeader)
    {
        await using var fixture = await ImportFixture.CreateAsync();

        var file = await fixture.Service.GenerateTemplateAsync(dataset, CancellationToken.None);
        var sheet = SimpleXlsxReader.Read(file.Content)[0];

        sheet.Rows[0].Should().Contain(expectedHeader);
    }

    [Fact]
    public async Task ImportableDatasetListExcludesReadOnlyExports()
    {
        await using var fixture = await ImportFixture.CreateAsync();

        var property = fixture.Service.GetType().GetProperty("ImportableDatasets");
        property.Should().NotBeNull();
        var datasets = (IReadOnlyList<ExportDataset>)property!.GetValue(fixture.Service)!;

        datasets.Should().Contain(ExportDataset.Contracts)
            .And.Contain(ExportDataset.StageResults)
            .And.NotContain(ExportDataset.ProjectOverview)
            .And.NotContain(ExportDataset.Accounts);
    }

    [Fact]
    public async Task CentralLedgerTemplatesCanBePreviewedAndImported()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new EngineeringManager.Domain.Organization.LegalEntity { Code = "IO-LE", Name = "导入公司", ShortName = "导入公司" };
        var partner = new BusinessPartner { PartnerNumber = "IO-BP", Name = "导入单位", ShortName = "导入单位" };
        var project = new EngineeringManager.Infrastructure.Data.Project { ProjectNumber = "IO-P", Name = "导入项目", Stage = EngineeringManager.Domain.Projects.ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var account = new FinancialAccount { LegalEntity = company, AccountName = "导入账户", AccountNumber = "IO-ACCT", AccountType = EngineeringManager.Domain.Finance.FinancialAccountType.Bank };
        fixture.Db.AddRange(company, partner, project, account);
        await fixture.Db.SaveChangesAsync();

        var collection = new SimpleXlsxWorkbook();
        collection.AddWorksheet("收款导入", ["项目编号", "收款日期", "签约公司编码", "合作单位编号", "收款账户账号", "收款金额", "收款方式", "备注"], [["IO-P", "2026-08-01", "IO-LE", "IO-BP", "IO-ACCT", 120m, "银行转账", "导入收款"]]);
        var collectionPreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("io-user", ExportDataset.Collections, "收款.xlsx", collection.ToArray(), null), CancellationToken.None);
        collectionPreview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(collectionPreview.BatchId, CancellationToken.None);

        var payment = new SimpleXlsxWorkbook();
        payment.AddWorksheet("付款导入", ["项目编号", "付款日期", "签约公司编码", "合作单位编号", "付款账户账号", "付款金额", "付款方式", "备注"], [["IO-P", "2026-08-02", "IO-LE", "IO-BP", "IO-ACCT", 80m, "现金", "导入付款"]]);
        var paymentPreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("io-user", ExportDataset.Payments, "付款.xlsx", payment.ToArray(), null), CancellationToken.None);
        paymentPreview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(paymentPreview.BatchId, CancellationToken.None);

        var invoice = new SimpleXlsxWorkbook();
        invoice.AddWorksheet("发票导入", ["项目编号", "发票号码", "发票日期", "发票方向", "签约公司编码", "合作单位编号", "含税金额", "状态", "备注"], [["IO-P", "IO-INV-001", "2026-08-03", "应收", "IO-LE", "IO-BP", 120m, "有效", "导入发票"]]);
        var invoicePreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("io-user", ExportDataset.Invoices, "发票.xlsx", invoice.ToArray(), null), CancellationToken.None);
        invoicePreview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(invoicePreview.BatchId, CancellationToken.None);

        (await fixture.Db.FinanceCashEntries.CountAsync()).Should().Be(2);
        (await fixture.Db.FinanceInvoices.CountAsync()).Should().Be(1);
        (await fixture.Db.AccountTransactions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task InvoiceImportRejectsDuplicateNumbersWithinOneUnsavedBatch()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new EngineeringManager.Domain.Organization.LegalEntity { Code = "DUP-LE", Name = "重复发票公司", ShortName = "重复发票" };
        var partner = new BusinessPartner { PartnerNumber = "DUP-BP", Name = "重复发票单位", ShortName = "重复发票单位" };
        var project = new EngineeringManager.Infrastructure.Data.Project { ProjectNumber = "DUP-P", Name = "重复发票项目", Stage = EngineeringManager.Domain.Projects.ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        fixture.Db.AddRange(company, partner, project);
        await fixture.Db.SaveChangesAsync();

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "发票导入",
            ["项目编号", "发票号码", "发票日期", "发票方向", "签约公司编码", "合作单位编号", "含税金额", "状态", "备注"],
            [
                ["DUP-P", "DUP-INV-001", "2026-08-03", "应收", "DUP-LE", "DUP-BP", 100m, "有效", "第一张"],
                ["DUP-P", "DUP-INV-001", "2026-08-04", "应收", "DUP-LE", "DUP-BP", 200m, "有效", "第二张"]
            ]);

        var preview = await fixture.Service.PreviewAsync(
            new ImportPreviewRequest("duplicate-invoice", ExportDataset.Invoices, "重复发票.xlsx", workbook.ToArray(), null),
            CancellationToken.None);
        var confirm = () => fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        await confirm.Should().ThrowAsync<InvalidOperationException>().WithMessage("*发票号码已存在：DUP-INV-001*");
        (await fixture.Db.FinanceInvoices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task EmployeeLedgerTemplatesCanImportAllFinancialDetails()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new EngineeringManager.Domain.Organization.LegalEntity { Code = "EMP-IO-LE", Name = "员工导入公司", ShortName = "员工导入公司" };
        var account = new FinancialAccount { LegalEntity = company, AccountName = "员工账户", AccountNumber = "EMP-IO-ACCT", AccountType = EngineeringManager.Domain.Finance.FinancialAccountType.Bank };
        fixture.Db.AddRange(company, account);
        await fixture.Db.SaveChangesAsync();

        var employeeBook = new SimpleXlsxWorkbook();
        employeeBook.AddWorksheet("员工导入", ["员工编号", "姓名", "员工类型"], [["EMP-IO-001", "员工导入甲", "正式员工"]]);
        var employeePreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("employee-io", ExportDataset.Employees, "员工.xlsx", employeeBook.ToArray(), null), CancellationToken.None);
        await fixture.Service.ConfirmAsync(employeePreview.BatchId, CancellationToken.None);

        var wageBook = new SimpleXlsxWorkbook();
        wageBook.AddWorksheet("员工工资明细导入", ["员工编号", "业务年度", "开始日期", "结束日期", "工资明细类型", "工资类别", "计薪方式", "收支性质", "最终金额", "备注"], [["EMP-IO-001", "2026年度", "2026-08-01", "2026-08-31", "出勤", "社保工资", "按月", "收入", 5000m, "工资导入"]]);
        var wagePreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("employee-io", (ExportDataset)21, "员工工资.xlsx", wageBook.ToArray(), null), CancellationToken.None);
        wagePreview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(wagePreview.BatchId, CancellationToken.None);

        var otherBook = new SimpleXlsxWorkbook();
        otherBook.AddWorksheet("员工往来导入", ["员工编号", "公司编码", "往来类型", "记录性质", "日期", "金额", "付款方式", "说明"], [["EMP-IO-001", "EMP-IO-LE", "费用", "应付", "2026-08-04", 300m, "银行转账", "报销"]]);
        var otherPreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("employee-io", (ExportDataset)22, "员工往来.xlsx", otherBook.ToArray(), null), CancellationToken.None);
        otherPreview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(otherPreview.BatchId, CancellationToken.None);

        var receiptBook = new SimpleXlsxWorkbook();
        receiptBook.AddWorksheet("员工收款导入", ["员工编号", "业务年度", "收款日期", "收款类型", "金额", "付款公司编码", "账户账号", "付款方式", "实际收款人"], [["EMP-IO-001", "2026年度", "2026-08-05", "工资", 400m, "EMP-IO-LE", "EMP-IO-ACCT", "银行转账", "员工导入甲"]]);
        var receiptPreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("employee-io", (ExportDataset)23, "员工收款.xlsx", receiptBook.ToArray(), null), CancellationToken.None);
        receiptPreview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(receiptPreview.BatchId, CancellationToken.None);

        (await fixture.Db.EmployeeWageEntries.CountAsync()).Should().Be(1);
        (await fixture.Db.EmployeeOtherPayments.CountAsync()).Should().Be(1);
        (await fixture.Db.EmployeeReceipts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PayrollTemplateCanImportBatchAndEmployeePayment()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new EngineeringManager.Domain.Organization.LegalEntity { Code = "PAY-IO-LE", Name = "工资导入公司", ShortName = "工资导入公司" };
        var account = new FinancialAccount { LegalEntity = company, AccountName = "工资账户", AccountNumber = "PAY-IO-ACCT", AccountType = EngineeringManager.Domain.Finance.FinancialAccountType.Bank };
        fixture.Db.AddRange(company, account, new Employee { EmployeeNumber = "PAY-IO-001", Name = "工资导入员工", EmployeeType = EmployeeType.Formal });
        await fixture.Db.SaveChangesAsync();

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("工资导入", ["批次编号", "批次名称", "批次类型", "开始日期", "结束日期", "发放日期", "公司编码", "账户账号", "实际总额", "付款方式", "员工编号", "人员来源", "人员姓名", "个人金额"], [["PAY-IO-001", "八月工资", "按月", "2026-08-01", "2026-08-31", "2026-09-05", "PAY-IO-LE", "PAY-IO-ACCT", 5000m, "银行转账", "PAY-IO-001", "员工", "工资导入员工", 5000m]]);
        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("payroll-io", ExportDataset.Payroll, "工资.xlsx", workbook.ToArray(), null), CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);

        (await fixture.Db.PayrollBatches.CountAsync()).Should().Be(1);
        (await fixture.Db.PayrollPayments.CountAsync()).Should().Be(1);
        (await fixture.Db.PayrollPayments.SumAsync(item => item.Amount)).Should().Be(5000m);
    }

    [Fact]
    public async Task MixedPayrollImportUpdatesExistingPaymentWithoutDuplicates()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new EngineeringManager.Domain.Organization.LegalEntity { Code = "PAY-UPD-LE", Name = "工资更新公司", ShortName = "工资更新公司" };
        var account = new FinancialAccount { LegalEntity = company, AccountName = "工资更新账户", AccountNumber = "PAY-UPD-ACCT", AccountType = FinancialAccountType.Bank };
        var employee = new Employee { EmployeeNumber = "PAY-UPD-001", Name = "工资更新员工", EmployeeType = EmployeeType.Formal };
        var batch = new PayrollBatch
        {
            BatchNumber = "PAY-UPD-BATCH",
            Name = "原工资批次",
            BatchType = PayrollBatchType.Monthly,
            StartDate = new DateOnly(2026, 8, 1),
            EndDate = new DateOnly(2026, 8, 31),
            PaymentDate = new DateOnly(2026, 9, 5),
            LegalEntity = company,
            Account = account,
            ActualAmount = 5000m,
            PaymentMethod = PaymentMethod.BankTransfer,
            Status = PayrollBatchStatus.Draft
        };
        batch.Payments.Add(new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.Employee,
            PaymentCategory = PayrollPaymentCategory.Wage,
            RecipientKey = $"employee:{employee.Id:N}",
            Employee = employee,
            Account = account,
            PaymentDate = new DateOnly(2026, 9, 5),
            Amount = 5000m,
            PaymentMethod = PaymentMethod.BankTransfer,
            PayeeType = PayrollPayeeType.Employee,
            PayeeName = employee.Name,
            RecipientNameSnapshot = employee.Name
        });
        fixture.Db.AddRange(company, account, employee, batch);
        await fixture.Db.SaveChangesAsync();

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "工资导入",
            ["批次编号", "批次名称", "批次类型", "开始日期", "结束日期", "发放日期", "公司编码", "账户账号", "实际总额", "付款方式", "员工编号", "人员来源", "人员姓名", "个人金额"],
            [["PAY-UPD-BATCH", "更新后的工资批次", "按月", "2026-08-01", "2026-08-31", "2026-09-05", "PAY-UPD-LE", "PAY-UPD-ACCT", 6200m, "银行转账", "PAY-UPD-001", "员工", "工资更新员工", 6200m]]);

        var preview = await fixture.Service.PreviewAsync(
            new ImportPreviewRequest("payroll-update", ExportDataset.Payroll, "工资更新.xlsx", workbook.ToArray(), null, ImportMode.Mixed),
            CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);

        (await fixture.Db.PayrollBatches.CountAsync()).Should().Be(1);
        (await fixture.Db.PayrollPayments.CountAsync()).Should().Be(1);
        (await fixture.Db.PayrollPayments.SingleAsync()).Amount.Should().Be(6200m);
        (await fixture.Db.PayrollBatches.SingleAsync()).ActualAmount.Should().Be(6200m);
    }

    [Fact]
    public async Task PreviewReturnsRowErrorsAndDoesNotPartiallyImport()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("员工导入", ["员工编号", "姓名", "员工类型"], [["E-001", "张三", "正式员工"], ["E-002", "", "劳务员工"]]);

        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user-1", ExportDataset.Employees, "员工.xlsx", workbook.ToArray(), null), CancellationToken.None);
        var confirmAction = () => fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);

        preview.TotalRows.Should().Be(2);
        preview.ValidRows.Should().Be(1);
        preview.Errors.Should().ContainSingle(item => item.RowNumber == 3 && item.ColumnName == "姓名");
        await confirmAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*错误*");
        (await fixture.Db.Employees.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BatchSpecificOldSystemMappingCanBeConfirmed()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("旧员工", ["工号", "人员姓名", "人员类别"], [["OLD-001", "旧系统员工", "正式员工"]]);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["工号"] = "employee_number",
            ["人员姓名"] = "name",
            ["人员类别"] = "employee_type"
        };

        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user-2", ExportDataset.Employees, "旧系统员工.xlsx", workbook.ToArray(), mapping), CancellationToken.None);
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        var employee = await fixture.Db.Employees.SingleAsync();
        employee.EmployeeNumber.Should().Be("OLD-001");
        employee.Name.Should().Be("旧系统员工");
        var batch = await fixture.Db.ImportBatches.SingleAsync(item => item.Id == preview.BatchId);
        batch.OriginalContent.Should().NotBeEmpty();
        batch.Status.Should().Be(DataExchangeTaskStatus.Completed);
    }

    [Fact]
    public async Task EmployeeImportAcceptsStableChineseAndEnglishTypeLabels()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工导入",
            ["员工编号", "姓名", "员工类型"],
            [
                ["TYPE-FORMAL-CN", "正式中文", "正式员工"],
                ["TYPE-FORMAL-EN", "正式英文", "Formal"],
                ["TYPE-LABOR-CN", "劳务中文", "劳务员工"],
                ["TYPE-LABOR-EN", "劳务英文", "Labor"],
                ["TYPE-TEMP-CN", "临时中文", "特殊临时人员"],
                ["TYPE-TEMP-EN", "临时英文", "Temporary"]
            ]);

        var preview = await fixture.Service.PreviewAsync(
            new ImportPreviewRequest("type-labels", ExportDataset.Employees, "员工类型.xlsx", workbook.ToArray(), null),
            CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);
        var imported = await fixture.Db.Employees.ToDictionaryAsync(item => item.EmployeeNumber, item => item.EmployeeType);
        imported["TYPE-FORMAL-CN"].Should().Be(EmployeeType.Formal);
        imported["TYPE-FORMAL-EN"].Should().Be(EmployeeType.Formal);
        imported["TYPE-LABOR-CN"].Should().Be(EmployeeType.Labor);
        imported["TYPE-LABOR-EN"].Should().Be(EmployeeType.Labor);
        imported["TYPE-TEMP-CN"].Should().Be(EmployeeType.Temporary);
        imported["TYPE-TEMP-EN"].Should().Be(EmployeeType.Temporary);
    }

    [Fact]
    public async Task MixedImportUpdatesExistingEmployeeAndPreservesAllOrNothingOnConcurrencyConflict()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var employee = new Employee { EmployeeNumber = "UPDATE-001", Name = "旧姓名", EmployeeType = EmployeeType.Formal };
        fixture.Db.Employees.Add(employee);
        await fixture.Db.SaveChangesAsync();

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("员工", ["员工编号", "姓名", "员工类型", "并发版本"], [[employee.EmployeeNumber, "新姓名", "正式员工", employee.ConcurrencyStamp.ToString()]]);
        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("update-user", ExportDataset.Employees, "update.xlsx", workbook.ToArray(), null, ImportMode.Mixed), default);
        await fixture.Service.ConfirmAsync(preview.BatchId, default);
        (await fixture.Db.Employees.SingleAsync(item => item.Id == employee.Id)).Name.Should().Be("新姓名");

        var stale = new SimpleXlsxWorkbook();
        stale.AddWorksheet("员工", ["员工编号", "姓名", "员工类型", "并发版本"], [[employee.EmployeeNumber, "不应写入", "正式员工", Guid.NewGuid().ToString()]]);
        var stalePreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("update-user", ExportDataset.Employees, "stale.xlsx", stale.ToArray(), null, ImportMode.Update), default);
        var before = (await fixture.Db.Employees.SingleAsync(item => item.Id == employee.Id)).Name;
        var confirmStale = () => fixture.Service.ConfirmAsync(stalePreview.BatchId, default);
        stalePreview.Errors.Should().ContainSingle(item => item.ColumnName == "并发版本");
        await confirmStale.Should().ThrowAsync<InvalidOperationException>().WithMessage("*错误*");
        (await fixture.Db.Employees.SingleAsync(item => item.Id == employee.Id)).Name.Should().Be(before);
    }

    [Fact]
    public async Task RoundTripEmployeeImportTreatsBlankOptionalCellsAsNoChange()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var employee = new Employee { EmployeeNumber = "RT-001", Name = "原姓名", Phone = "13800000000", EmployeeType = EmployeeType.Formal };
        fixture.Db.Employees.Add(employee);
        await fixture.Db.SaveChangesAsync();
        var export = await new ExportService(fixture.Db, new EngineeringManager.Infrastructure.Finance.FinanceLedgerService(fixture.Db))
            .ExportAsync(new ExportRequest(ExportDataset.Employees, "round-trip", ["employee_number", "name", "phone", "employee_type"], null, UseRoundTripWorkbook: true), CancellationToken.None);

        var source = SimpleXlsxReader.Read(export.Content);
        var edited = new SimpleXlsxWorkbook();
        foreach (var sheet in source)
        {
            var headers = sheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
            var rows = sheet.Rows.Skip(1).Select(row => row.ToArray()).ToList();
            if (sheet.Name == "员工")
            {
                var nameIndex = Array.IndexOf(headers, "姓名");
                var phoneIndex = Array.IndexOf(headers, "电话");
                rows[0][nameIndex] = "修改后的姓名";
                rows[0][phoneIndex] = null;
            }
            edited.AddWorksheet(sheet.Name, headers, rows);
        }

        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("round-trip", ExportDataset.Employees, "员工往返.xlsx", edited.ToArray(), null), CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);

        var updated = await fixture.Db.Employees.SingleAsync(item => item.Id == employee.Id);
        updated.Name.Should().Be("修改后的姓名");
        updated.Phone.Should().Be("13800000000");
    }

    [Fact]
    public async Task RoundTripEmployeeImportRejectsTamperedBusinessKeyForSystemId()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var employee = new Employee { EmployeeNumber = "RT-TAMPER", Name = "原姓名", EmployeeType = EmployeeType.Formal };
        fixture.Db.Employees.Add(employee);
        await fixture.Db.SaveChangesAsync();
        var export = await new ExportService(fixture.Db, new EngineeringManager.Infrastructure.Finance.FinanceLedgerService(fixture.Db))
            .ExportAsync(new ExportRequest(ExportDataset.Employees, "round-trip", ["employee_number", "name", "employee_type"], null, UseRoundTripWorkbook: true), CancellationToken.None);

        var source = SimpleXlsxReader.Read(export.Content);
        var edited = new SimpleXlsxWorkbook();
        foreach (var sheet in source)
        {
            var headers = sheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
            var rows = sheet.Rows.Skip(1).Select(row => row.ToArray()).ToList();
            if (sheet.Name == "员工") rows[0][Array.IndexOf(headers, "_business_key")] = "RT-OTHER";
            edited.AddWorksheet(sheet.Name, headers, rows);
        }

        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("round-trip", ExportDataset.Employees, "员工篡改.xlsx", edited.ToArray(), null), CancellationToken.None);

        preview.Errors.Should().Contain(item => item.ColumnName == "_record_id" && item.Message.Contains("篡改", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RoundTripWorkbookCannotBeImportedIntoAnotherDataset()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var employee = new Employee { EmployeeNumber = "RT-DATASET", Name = "员工数据集", EmployeeType = EmployeeType.Formal };
        fixture.Db.Employees.Add(employee);
        await fixture.Db.SaveChangesAsync();
        var export = await new ExportService(fixture.Db, new EngineeringManager.Infrastructure.Finance.FinanceLedgerService(fixture.Db))
            .ExportAsync(new ExportRequest(ExportDataset.Employees, "round-trip", ["employee_number", "name", "employee_type"], null, UseRoundTripWorkbook: true), CancellationToken.None);

        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("round-trip", ExportDataset.Projects, "错误数据集.xlsx", export.Content, null), CancellationToken.None);

        preview.Errors.Should().Contain(item => item.ColumnName == "_dataset_key" && item.Message.Contains("不一致", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RoundTripEmployeeImportUsesExplicitClearMarkerForNullableFields()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var employee = new Employee { EmployeeNumber = "RT-CLEAR", Name = "待清空", Phone = "13800000000", Notes = "旧备注", EmployeeType = EmployeeType.Formal };
        fixture.Db.Employees.Add(employee);
        await fixture.Db.SaveChangesAsync();
        var export = await new ExportService(fixture.Db, new EngineeringManager.Infrastructure.Finance.FinanceLedgerService(fixture.Db))
            .ExportAsync(new ExportRequest(ExportDataset.Employees, "round-trip", ["employee_number", "name", "employee_type", "phone", "notes"], null, UseRoundTripWorkbook: true), CancellationToken.None);
        var source = SimpleXlsxReader.Read(export.Content);
        var edited = new SimpleXlsxWorkbook();
        foreach (var sheet in source)
        {
            var headers = sheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
            var rows = sheet.Rows.Skip(1).Select(row => row.ToArray()).ToList();
            if (sheet.Name == "员工")
            {
                rows[0][Array.IndexOf(headers, "电话")] = "【清空】";
                rows[0][Array.IndexOf(headers, "备注")] = "【清空】";
            }
            edited.AddWorksheet(sheet.Name, headers, rows);
        }

        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("round-trip", ExportDataset.Employees, "员工清空.xlsx", edited.ToArray(), null), CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);
        var updated = await fixture.Db.Employees.SingleAsync(item => item.Id == employee.Id);
        updated.Phone.Should().BeNull();
        updated.Notes.Should().BeNull();
    }

    [Fact]
    public async Task RoundTripEmployeeImportBlocksStaleConcurrencyAsOneBatch()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var employee = new Employee { EmployeeNumber = "RT-002", Name = "导出姓名", EmployeeType = EmployeeType.Formal };
        fixture.Db.Employees.Add(employee);
        await fixture.Db.SaveChangesAsync();
        var exportService = new ExportService(fixture.Db, new EngineeringManager.Infrastructure.Finance.FinanceLedgerService(fixture.Db));
        var export = await exportService.ExportAsync(new ExportRequest(ExportDataset.Employees, "round-trip", ["employee_number", "name", "employee_type"], null, UseRoundTripWorkbook: true), CancellationToken.None);

        employee.Name = "系统内修改";
        employee.ConcurrencyStamp = Guid.NewGuid();
        await fixture.Db.SaveChangesAsync();
        var source = SimpleXlsxReader.Read(export.Content);
        var edited = new SimpleXlsxWorkbook();
        foreach (var sheet in source)
        {
            var headers = sheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
            var rows = sheet.Rows.Skip(1).Select(row => row.ToArray()).ToList();
            if (sheet.Name == "员工") rows[0][Array.IndexOf(headers, "姓名")] = "不应覆盖";
            edited.AddWorksheet(sheet.Name, headers, rows);
        }

        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("round-trip", ExportDataset.Employees, "员工过期.xlsx", edited.ToArray(), null), CancellationToken.None);
        preview.Errors.Should().Contain(error => error.ColumnName == "并发版本");
        var confirm = () => fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);
        await confirm.Should().ThrowAsync<InvalidOperationException>();
        (await fixture.Db.Employees.SingleAsync(item => item.Id == employee.Id)).Name.Should().Be("系统内修改");
    }

    [Fact]
    public async Task RoundTripEmployeeImportAllowsAppendingRowsWithBlankControlColumns()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var exportService = new ExportService(fixture.Db, new EngineeringManager.Infrastructure.Finance.FinanceLedgerService(fixture.Db));
        var export = await exportService.ExportAsync(new ExportRequest(ExportDataset.Employees, "round-trip", ["employee_number", "name", "employee_type"], null, UseRoundTripWorkbook: true), CancellationToken.None);
        var source = SimpleXlsxReader.Read(export.Content);
        var edited = new SimpleXlsxWorkbook();
        foreach (var sheet in source)
        {
            var headers = sheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
            var rows = sheet.Rows.Skip(1).Select(row => row.ToArray()).ToList();
            if (sheet.Name == "员工")
            {
                rows.Add(["RT-NEW", "新增员工", "正式员工", null, null, null, null, null]);
            }
            edited.AddWorksheet(sheet.Name, headers, rows);
        }

        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("round-trip", ExportDataset.Employees, "员工新增.xlsx", edited.ToArray(), null), CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);
        (await fixture.Db.Employees.SingleAsync(item => item.EmployeeNumber == "RT-NEW")).Name.Should().Be("新增员工");
    }

    [Fact]
    public async Task CompanyAccountAndCertificateImportsResolveCompanyAndCategory()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        fixture.Db.CompanyCategories.Add(new EngineeringManager.Domain.Organization.CompanyCategory { Code = "GENERAL", Name = "一般纳税人有限公司" });
        await fixture.Db.SaveChangesAsync();
        var companyBook = new SimpleXlsxWorkbook();
        companyBook.AddWorksheet("公司导入", ["公司编码", "公司全称", "公司简称", "组合分类编码", "法人/经营者", "统一社会信用代码/税号"], [["IMP-C", "导入测试公司", "导入公司", "GENERAL", "测试法人", "913IMP"]]);

        var companyPreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user", ExportDataset.Companies, "公司.xlsx", companyBook.ToArray(), null), default);
        await fixture.Service.ConfirmAsync(companyPreview.BatchId, default);
        var company = await fixture.Db.LegalEntities.SingleAsync(item => item.Code == "IMP-C");

        var accountBook = new SimpleXlsxWorkbook();
        accountBook.AddWorksheet("公司账户导入", ["公司编码", "账户名称", "账户类型", "期初余额", "默认收款", "默认付款", "默认开票"], [["IMP-C", "基本户", "银行", "100", "是", "是", "是"]]);
        var accountPreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user", ExportDataset.CompanyAccounts, "账户.xlsx", accountBook.ToArray(), null), default);
        await fixture.Service.ConfirmAsync(accountPreview.BatchId, default);

        var certificateBook = new SimpleXlsxWorkbook();
        certificateBook.AddWorksheet("公司证照导入", ["公司编码", "资料类型", "资料编号", "有效期"], [["IMP-C", "营业执照", "LIC-01", "2030-12-31"]]);
        var certificatePreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user", ExportDataset.CompanyCertificates, "证照.xlsx", certificateBook.ToArray(), null), default);
        await fixture.Service.ConfirmAsync(certificatePreview.BatchId, default);

        company.CompanyCategoryId.Should().NotBeNull();
        (await fixture.Db.FinancialAccounts.SingleAsync()).IsDefaultInvoice.Should().BeTrue();
        (await fixture.Db.CompanyCertificates.SingleAsync()).ExpiresOn.Should().Be(new DateOnly(2030, 12, 31));
    }

    [Fact]
    public async Task EquipmentImportResolvesOwnerCompany()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        fixture.Db.LegalEntities.Add(new EngineeringManager.Domain.Organization.LegalEntity { Code = "EQ-OWNER", Name = "设备所属公司", ShortName = "设备公司" });
        await fixture.Db.SaveChangesAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("设备导入", ["设备编号", "设备名称", "权属", "所属公司编码", "型号"], [["IMP-EQ", "导入挖掘机", "自有", "EQ-OWNER", "X100"], ["IMP-EQ-OTHER", "其他来源设备", "其他", "", "O100"]]);
        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user", ExportDataset.Equipment, "设备.xlsx", workbook.ToArray(), null), default);
        await fixture.Service.ConfirmAsync(preview.BatchId, default);
        var equipment = await fixture.Db.Equipment.SingleAsync(item => item.EquipmentNumber == "IMP-EQ");
        equipment.EquipmentNumber.Should().Be("IMP-EQ");
        equipment.OwnerLegalEntityId.Should().NotBeNull();
        var other = await fixture.Db.Equipment.SingleAsync(item => item.EquipmentNumber == "IMP-EQ-OTHER");
        other.OwnershipType.Should().Be(EquipmentOwnershipType.Other);
        other.OwnerLegalEntityId.Should().BeNull();
    }

    [Fact]
    public async Task EmployeeCertificateImportResolvesEmployeeAndExtendedFields()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        fixture.Db.Employees.Add(new Employee { EmployeeNumber = "CERT-EMP", Name = "导入持证员工", EmployeeType = EngineeringManager.Domain.Employees.EmployeeType.Formal });
        await fixture.Db.SaveChangesAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("员工证书导入", ["员工编号", "证书类型", "证书编号", "专业/等级/范围", "发证机关", "签发日期", "到期日期"], [["CERT-EMP", "安全员证", "AQ-100", "C证", "住建部门", "2024-01-01", "2027-01-01"]]);

        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user", ExportDataset.EmployeeCertificates, "员工证书.xlsx", workbook.ToArray(), null), default);
        await fixture.Service.ConfirmAsync(preview.BatchId, default);

        var certificate = await fixture.Db.EmployeeCertificates.SingleAsync();
        certificate.SpecialtyLevelScope.Should().Be("C证");
        certificate.IssuingAuthority.Should().Be("住建部门");
    }

    [Fact]
    public async Task CompleteEmployeeWorkbookImportsLedgerDataAndIsIdempotent()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new EngineeringManager.Domain.Organization.LegalEntity
        {
            Code = "EMP-LE",
            Name = "员工工资付款公司",
            ShortName = "工资公司"
        };
        var employee = new Employee
        {
            EmployeeNumber = "EMP-001",
            Name = "叶青",
            IdentityNumber = "ID-001",
            EmployeeType = EmployeeType.Formal
        };
        var account = new FinancialAccount
        {
            LegalEntity = company,
            AccountName = "工资付款账户",
            AccountType = EngineeringManager.Domain.Finance.FinancialAccountType.Bank,
            IsDefaultPayment = true
        };
        fixture.Db.AddRange(company, employee, account);
        await fixture.Db.SaveChangesAsync();

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工总表",
            ["姓名", "身份证号", "工种", "开工时间", "最后一天上班时间", "工资", "工资（单位）", "上班时间", "全勤工资", "请假扣除", "应付报销款及加班费", "年终分红", "实际应付工资", "已付", "未付", "备注", "银行卡号"],
            [["叶青", "ID-001", "会计", "2026-03-04", null, 8000m, "元/月", "2026年3月至今", 10000m, 0m, 300m, 0m, 10300m, 600m, 9700m, "公式导入测试", "6222000000000001"]]);
        workbook.AddWorksheet(
            "叶青",
            ["机器", "工种", "姓名", "身份证号", "联系电话", "开工时间", "最后一天上班时间", "工资", "工资（单位）", "上班时间", "全勤工资", "请假天数", "请假扣除", "应付报销款及加班费", "年终分红", "实际应付工资", "已付", "未付", "备注", "银行卡号"],
            [
                [null, "会计", "叶青", "ID-001", "13800000000", "2026-03-04", null, 8000m, "元/月", "2026年3月至今", 10000m, null, 0m, 300m, 0m, 10300m, 600m, 9700m, "公式导入测试", "6222000000000001"],
                [null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null],
                ["应付款", null, "已付款", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null],
                ["月份", "应付报销款", "公司付款日期", "公司已付款", "备注", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null],
                ["2026.04.01", 300m, "2026.04.14", 600m, "工资与报销已发", null, null, null, null, null, null, null, null, null, null, null, null, null],
                [null, 300m, "合计已付：", 600m, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null]
            ]);

        var request = new ImportPreviewRequest("employee-workbook", ExportDataset.Employees, "员工相关信息2026年.xlsx", workbook.ToArray(), null);
        var preview = await fixture.Service.PreviewAsync(request, CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);

        var repeatPreview = await fixture.Service.PreviewAsync(request, CancellationToken.None);
        repeatPreview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(repeatPreview.BatchId, CancellationToken.None);

        (await fixture.Db.BusinessYears.CountAsync()).Should().Be(1);
        (await fixture.Db.EmployeeWageEntries.CountAsync()).Should().Be(1);
        (await fixture.Db.EmployeeWageEntries.SumAsync(item => item.FinalAmount)).Should().Be(10000m);
        (await fixture.Db.EmployeeOtherPayments.CountAsync()).Should().Be(1);
        (await fixture.Db.EmployeeOtherPayments.SumAsync(item => item.Amount)).Should().Be(300m);
        (await fixture.Db.EmployeeReceipts.CountAsync()).Should().Be(1);
        (await fixture.Db.EmployeeReceipts.SumAsync(item => item.Amount)).Should().Be(600m);
        (await fixture.Db.AccountTransactions.CountAsync(item => item.SourceType == EngineeringManager.Domain.Finance.AccountTransactionSourceType.EmployeeReceipt)).Should().Be(1);

        var wageTotal = await fixture.Db.EmployeeWageEntries.SumAsync(item => item.FinalAmount);
        var otherTotal = await fixture.Db.EmployeeOtherPayments.SumAsync(item => item.Amount);
        var receiptTotal = await fixture.Db.EmployeeReceipts.SumAsync(item => item.Amount);
        (wageTotal + otherTotal - receiptTotal).Should().Be(9700m);
    }

    [Fact]
    public async Task CompleteEmployeeWorkbookAllowsTextualPieceworkSalary()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工总表",
            ["姓名", "身份证号", "工资", "工资（单位）", "实际应付工资", "已付", "未付"],
            [["计件员工", "PIECE-001", "按米算", "元/米", 0m, 0m, 0m]]);

        var preview = await fixture.Service.PreviewAsync(
            new ImportPreviewRequest("piecework", ExportDataset.Employees, "计件员工.xlsx", workbook.ToArray(), null),
            CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);
        (await fixture.Db.Employees.SingleAsync()).Name.Should().Be("计件员工");
    }

    [Fact]
    public async Task CompleteEmployeeWorkbookStoresMissingIdentityNumbersAsNull()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工总表",
            ["姓名", "身份证号", "实际应付工资", "已付", "未付"],
            [["无证件甲", null, 0m, 0m, 0m], ["无证件乙", null, 0m, 0m, 0m]]);

        var preview = await fixture.Service.PreviewAsync(
            new ImportPreviewRequest("missing-identity", ExportDataset.Employees, "无身份证员工.xlsx", workbook.ToArray(), null),
            CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);

        (await fixture.Db.Employees.CountAsync()).Should().Be(2);
        var identities = await fixture.Db.Employees.Select(item => item.IdentityNumber).ToListAsync();
        identities.Should().OnlyContain(item => item == null);
    }

    [Fact]
    public async Task CompleteEmployeeWorkbookUsesCompactNumbersForNewEmployees()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工总表",
            ["姓名", "身份证号", "实际应付工资", "已付", "未付"],
            [["短编号甲", "SHORT-001", 0m, 0m, 0m], ["短编号乙", "SHORT-002", 0m, 0m, 0m]]);

        var preview = await fixture.Service.PreviewAsync(
            new ImportPreviewRequest("compact-number", ExportDataset.Employees, "短编号.xlsx", workbook.ToArray(), null),
            CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);

        (await fixture.Db.Employees.OrderBy(item => item.EmployeeNumber).Select(item => item.EmployeeNumber).ToListAsync())
            .Should().Equal("YG0001", "YG0002");
    }

    [Fact]
    public async Task CompleteEmployeeWorkbookSkipsAllExistingCompactNumbers()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        fixture.Db.Employees.AddRange(
            new Employee { EmployeeNumber = "YG0001", Name = "已存在甲", EmployeeType = EmployeeType.Formal },
            new Employee { EmployeeNumber = "YG0002", Name = "已存在乙", EmployeeType = EmployeeType.Formal });
        await fixture.Db.SaveChangesAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工总表",
            ["姓名", "身份证号", "实际应付工资", "已付", "未付"],
            [["新增员工甲", "SHORT-003", 0m, 0m, 0m], ["新增员工乙", "SHORT-004", 0m, 0m, 0m]]);

        var preview = await fixture.Service.PreviewAsync(
            new ImportPreviewRequest("compact-number-collision", ExportDataset.Employees, "短编号碰撞.xlsx", workbook.ToArray(), null),
            CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);

        (await fixture.Db.Employees.OrderBy(item => item.EmployeeNumber).Select(item => item.EmployeeNumber).ToListAsync())
            .Should().Equal("YG0001", "YG0002", "YG0003", "YG0004");
    }

    [Fact]
    public async Task CompleteEmployeeWorkbookIgnoresBlankDetailSheetPlaceholderRows()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工总表",
            ["姓名", "身份证号", "实际应付工资", "已付", "未付"],
            [["有效员工", "VALID-001", 0m, 0m, 0m]]);
        workbook.AddWorksheet(
            "空白模板",
            ["机器", "工种", "姓名", "身份证号", "联系电话"],
            [
                [null, null, null, null, null],
                [null, null, null, null, null],
                ["应付款", null, "已付款", null, null],
                ["月份", "应付报销款", "公司付款日期", "公司已付款", "备注"],
                [null, 0m, null, null, null]
            ]);

        var preview = await fixture.Service.PreviewAsync(
            new ImportPreviewRequest("blank-detail", ExportDataset.Employees, "员工空白模板.xlsx", workbook.ToArray(), null),
            CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);
        (await fixture.Db.Employees.Select(item => item.Name).ToListAsync()).Should().Equal("有效员工");
    }

    [Fact]
    public async Task CompleteEmployeeWorkbookDoesNotTreatPaymentSectionLabelAsEmployee()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new EngineeringManager.Domain.Organization.LegalEntity
        {
            Code = "LABEL-LE",
            Name = "标签测试公司",
            ShortName = "标签公司"
        };
        fixture.Db.FinancialAccounts.Add(new FinancialAccount
        {
            LegalEntity = company,
            AccountName = "标签测试账户",
            AccountType = EngineeringManager.Domain.Finance.FinancialAccountType.Bank,
            IsDefaultPayment = true
        });
        await fixture.Db.SaveChangesAsync();

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工总表",
            ["姓名", "身份证号", "实际应付工资", "已付", "未付"],
            [["有效员工", "VALID-001", 0m, 0m, 0m]]);
        workbook.AddWorksheet(
            "无人员资料",
            ["机器", "工种", "姓名", "身份证号", "联系电话"],
            [
                [null, null, null, null, null],
                [null, null, null, null, null],
                ["应付款", null, "已付款", null, null],
                ["月份", "应付报销款", "公司付款日期", "公司已付款", "备注"],
                ["2026.04", 100m, null, 0m, "缺少员工归属"]
            ]);

        var preview = await fixture.Service.PreviewAsync(
            new ImportPreviewRequest("section-label", ExportDataset.Employees, "员工标签.xlsx", workbook.ToArray(), null),
            CancellationToken.None);

        preview.Errors.Should().ContainSingle(error => error.Message.Contains("无法从员工明细工作表确定归属员工", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompleteEmployeeWorkbookKeepsWageSeparateFromHistoricalDetailPayables()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new EngineeringManager.Domain.Organization.LegalEntity
        {
            Code = "WAGE-LE",
            Name = "工资拆分测试公司",
            ShortName = "拆分公司"
        };
        fixture.Db.FinancialAccounts.Add(new FinancialAccount
        {
            LegalEntity = company,
            AccountName = "工资拆分账户",
            AccountType = EngineeringManager.Domain.Finance.FinancialAccountType.Bank,
            IsDefaultPayment = true
        });
        await fixture.Db.SaveChangesAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工总表",
            ["姓名", "身份证号", "应付报销款及加班费", "实际应付工资", "已付", "未付"],
            [["历史往来员工", "HISTORY-001", 0m, 100m, 0m, 100m]]);
        workbook.AddWorksheet(
            "历史往来员工",
            ["姓名", "身份证号", "工种", "工资", "预留1", "预留2", "预留3", "预留4"],
            [
                ["历史往来员工", "HISTORY-001", "劳务", null, null, null, null, null],
                [null, null, null, "应付款", null, null, null, null],
                [null, null, null, "月份", "应付报销款", "公司付款日期", "公司已付款", "备注"],
                [null, null, null, "历史余款", 40m, null, 0m, null]
            ]);

        var preview = await fixture.Service.PreviewAsync(
            new ImportPreviewRequest("wage-split", ExportDataset.Employees, "工资拆分.xlsx", workbook.ToArray(), null),
            CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);
        (await fixture.Db.EmployeeWageEntries.SumAsync(item => item.FinalAmount)).Should().Be(100m);
        (await fixture.Db.EmployeeOtherPayments.SumAsync(item => item.Amount)).Should().Be(40m);
    }

    private sealed class ImportFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private ImportFixture(SqliteConnection connection, ApplicationDbContext db, IImportService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IImportService Service { get; }

        public static async Task<ImportFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new ImportFixture(connection, db, new ImportService(db));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
