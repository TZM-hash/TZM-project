using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed class ImportService(ApplicationDbContext db) : IImportService
{
    private const string CompleteEmployeeWorkbookMarker = "__完整员工工作簿";
    private readonly CentralLedgerCommandService centralLedgerCommands = new(db);

    private sealed record FinanceImportRow(int RowNumber, Dictionary<string, string?> Values);
    private sealed record FinanceAllocationImport(
        Guid SettlementId,
        decimal Amount,
        int AllocationOrder,
        Guid? ProjectId,
        Guid? ContractId,
        int RowNumber);
    private sealed record CashImportPlan(
        Guid? SystemId,
        LedgerDirection Direction,
        LedgerCashType CashType,
        LegalEntity LegalEntity,
        BusinessPartner BusinessPartner,
        FinancialAccount Account,
        DateOnly BusinessDate,
        decimal Amount,
        string PaymentMethod,
        string? Notes,
        Guid? ProjectId,
        Guid? ContractId,
        IReadOnlyList<FinanceAllocationImport> Allocations,
        bool HasAllocationColumns,
        bool HasProjectColumn,
        bool HasContractColumn,
        bool HasNotes);
    private sealed record InvoiceImportPlan(
        Guid? SystemId,
        LedgerDirection Direction,
        LegalEntity LegalEntity,
        BusinessPartner BusinessPartner,
        string InvoiceNumber,
        DateOnly InvoiceDate,
        decimal Amount,
        decimal? NetAmount,
        decimal? TaxAmount,
        decimal? TaxRate,
        Guid? ProjectTaxConfigurationId,
        string? InvoiceType,
        LedgerRecordStatus Status,
        string? Notes,
        Guid? ProjectId,
        Guid? ContractId,
        IReadOnlyList<FinanceAllocationImport> Allocations,
        bool HasAllocationColumns,
        bool HasNetAmount,
        bool HasTaxAmount,
        bool HasTaxRate,
        bool HasTaxConfiguration,
        bool HasInvoiceType,
        bool HasProjectColumn,
        bool HasContractColumn,
        bool HasNotes);

    private static readonly Dictionary<ExportDataset, IReadOnlyList<ImportColumn>> Columns = new()
    {
        [ExportDataset.Employees] =
        [
            new("员工编号", "employee_number", true),
            new("姓名", "name", true),
            new("员工类型", "employee_type", true),
            new("岗位", "position", false),
            new("电话", "phone", false),
            new("身份证号", "identity_number", false),
            new("银行卡号", "bank_account_number", false),
            new("开户行", "bank_name", false),
            new("默认月工资", "default_monthly_salary", false),
            new("默认日工资", "default_daily_rate", false),
            new("默认时工资", "default_hourly_rate", false),
            new("默认计件单价", "default_piecework_rate", false),
            new("系统ID", "_system_id", false),
            new("并发版本", "_concurrency_stamp", false)
        ],
        [ExportDataset.Payroll] =
        [
            new("批次编号", "batch_number", true),
            new("批次名称", "batch_name", true),
            new("批次类型", "batch_type", true),
            new("开始日期", "start_date", true),
            new("结束日期", "end_date", true),
            new("发放日期", "payment_date", false),
            new("项目编号", "project_number", false),
            new("公司编码", "legal_entity_code", false),
            new("账户账号", "account_number", false),
            new("实际总额", "actual_amount", true),
            new("付款方式", "payment_method", false),
            new("员工编号", "employee_number", false),
            new("人员来源", "recipient_type", false),
            new("人员姓名", "recipient_name", false),
            new("个人金额", "amount", false),
            new("备注", "notes", false)
        ],
        [ExportDataset.Collections] =
        [
            new("系统ID", "_system_id", false),
            new("项目编号", "project_number", false),
            new("合同编号", "contract_number", false),
            new("收款日期", "collection_date", true),
            new("签约公司编码", "legal_entity_code", false),
            new("签约公司", "legal_entity", false),
            new("合作单位编号", "partner_number", false),
            new("合作单位", "partner", false),
            new("收款账户账号", "account_number", false),
            new("收款账户", "account", false),
            new("收款金额", "amount", true),
            new("源记录总额", "source_amount", false),
            new("分摊金额", "allocation_amount", false),
            new("结算系统ID", "settlement_id", false),
            new("收款方式", "payment_method", true),
            new("备注", "notes", false)
        ],
        [ExportDataset.Payments] =
        [
            new("系统ID", "_system_id", false),
            new("项目编号", "project_number", false),
            new("合同编号", "contract_number", false),
            new("付款日期", "payment_date", true),
            new("签约公司编码", "legal_entity_code", false),
            new("签约公司", "legal_entity", false),
            new("合作单位编号", "partner_number", false),
            new("合作单位", "partner", false),
            new("付款账户账号", "account_number", false),
            new("付款账户", "account", false),
            new("付款金额", "amount", true),
            new("源记录总额", "source_amount", false),
            new("分摊金额", "allocation_amount", false),
            new("结算系统ID", "settlement_id", false),
            new("付款方式", "payment_method", true),
            new("备注", "notes", false)
        ],
        [ExportDataset.Invoices] =
        [
            new("系统ID", "_system_id", false),
            new("项目编号", "project_number", false),
            new("合同编号", "contract_number", false),
            new("发票号码", "invoice_number", true),
            new("发票日期", "invoice_date", true),
            new("发票方向", "direction", true),
            new("签约公司编码", "legal_entity_code", false),
            new("签约公司", "legal_entity", false),
            new("合作单位编号", "partner_number", false),
            new("合作单位", "partner", false),
            new("发票类型", "invoice_type", false),
            new("项目税务配置ID", "project_tax_configuration_id", false),
            new("税率", "tax_rate", false),
            new("未税金额", "net_amount", false),
            new("税额", "tax_amount", false),
            new("含税金额", "gross_amount", true),
            new("源记录总额", "source_amount", false),
            new("分摊金额", "allocation_amount", false),
            new("结算系统ID", "settlement_id", false),
            new("状态", "status", true),
            new("备注", "notes", false)
        ],
        [ExportDataset.EmployeeWages] =
        [
            new("系统ID", "_system_id", false),
            new("员工编号", "employee_number", true),
            new("业务年度", "business_year", true),
            new("开始日期", "start_date", true),
            new("结束日期", "end_date", true),
            new("工资明细类型", "entry_type", true),
            new("工资类别", "wage_category", true),
            new("计薪方式", "calculation_method", true),
            new("收支性质", "nature", true),
            new("数量", "quantity", false),
            new("单位", "unit", false),
            new("单价", "unit_price", false),
            new("自动金额", "automatic_amount", false),
            new("调整金额", "adjustment_amount", false),
            new("最终金额", "final_amount", true),
            new("公司编码", "legal_entity_code", false),
            new("项目编号", "project_number", false),
            new("备注", "notes", false)
        ],
        [ExportDataset.EmployeeOtherPayments] =
        [
            new("系统ID", "_system_id", false),
            new("员工编号", "employee_number", true),
            new("项目编号", "project_number", false),
            new("公司编码", "legal_entity_code", false),
            new("公司名称", "legal_entity", false),
            new("往来类型", "entry_type", true),
            new("记录性质", "record_kind", true),
            new("关联应付ID", "related_payable_id", false),
            new("账户账号", "account_number", false),
            new("账户名称", "account", false),
            new("日期", "entry_date", true),
            new("金额", "amount", true),
            new("付款方式", "payment_method", false),
            new("说明", "description", false)
        ],
        [ExportDataset.EmployeeReceipts] =
        [
            new("系统ID", "_system_id", false),
            new("员工编号", "employee_number", true),
            new("业务年度", "business_year", true),
            new("收款日期", "receipt_date", true),
            new("收款类型", "receipt_type", true),
            new("金额", "amount", true),
            new("付款公司编码", "payment_legal_entity_code", false),
            new("付款公司", "payment_legal_entity", false),
            new("账户账号", "account_number", false),
            new("账户名称", "account", false),
            new("付款方式", "payment_method", true),
            new("实际收款人", "actual_recipient_name", true),
            new("项目编号", "project_number", false),
            new("备注", "notes", false)
        ],
        [ExportDataset.EmployeeFinancialAdjustments] =
        [
            new("系统ID", "_system_id", false),
            new("员工编号", "employee_number", true),
            new("业务年度", "business_year", true),
            new("调整日期", "adjustment_date", true),
            new("调整金额", "amount", true),
            new("调整类型", "adjustment_type", true),
            new("说明", "notes", true)
        ],
        [ExportDataset.EmployeeCertificates] =
        [
            new("员工编号", "employee_number", true),
            new("证书类型", "certificate_type", true),
            new("证书编号", "certificate_number", false),
            new("专业/等级/范围", "specialty_level_scope", false),
            new("发证机关", "issuing_authority", false),
            new("签发日期", "issued_on", false),
            new("到期日期", "expires_on", false),
            new("备注", "notes", false)
        ],
        [ExportDataset.Partners] =
        [
            new("单位编号", "partner_number", true),
            new("单位名称", "name", true),
            new("简称", "short_name", true)
        ],
        [ExportDataset.Projects] =
        [
            new("项目编号", "project_number", true),
            new("项目名称", "name", true),
            new("项目阶段", "stage", false),
            new("总包单位", "general_contractor", false)
        ],
        [ExportDataset.Contracts] =
        [
            new("项目编号", "project_number", true), new("合同编号", "contract_number", true), new("合同名称", "name", true), new("合同类型", "contract_type", true), new("对方单位", "counterparty_name", false), new("签订日期", "signed_date", false), new("合同金额", "total_amount", true), new("备注", "notes", false)
        ],
        [ExportDataset.StageResults] =
        [
            new("项目编号", "project_number", true), new("成果标题", "title", true), new("成果类型", "result_type", true), new("状态", "status", false), new("成果日期", "result_date", true), new("质量结果", "quality_result", false), new("说明", "description", false)
        ],
        [ExportDataset.Companies] =
        [
            new("公司编码", "company_code", true),
            new("公司全称", "name", true),
            new("公司简称", "short_name", true),
            new("组合分类编码", "category_code", true),
            new("法人/经营者", "legal_representative", false),
            new("统一社会信用代码/税号", "tax_code", false),
            new("注册地址", "registered_address", false),
            new("经营地址", "business_address", false),
            new("电话", "phone", false)
        ],
        [ExportDataset.CompanyAccounts] =
        [
            new("公司编码", "company_code", true),
            new("账户名称", "account_name", true),
            new("账户类型", "account_type", true),
            new("账号", "account_number", false),
            new("开户行", "bank_name", false),
            new("期初余额", "opening_balance", false),
            new("默认收款", "default_collection", false),
            new("默认付款", "default_payment", false),
            new("默认开票", "default_invoice", false)
        ],
        [ExportDataset.CompanyCertificates] =
        [
            new("公司编码", "company_code", true),
            new("资料类型", "certificate_type", true),
            new("资料编号", "certificate_number", false),
            new("专业/等级/范围", "specialty_level_scope", false),
            new("发证机关", "issuing_authority", false),
            new("签发日期", "issued_on", false),
            new("有效期", "expires_on", false),
            new("备注", "notes", false)
        ],
        [ExportDataset.Equipment] =
        [
            new("设备编号", "equipment_number", true), new("设备名称", "name", true), new("权属", "ownership", true), new("所属公司编码", "owner_company_code", false), new("出租方编号", "lessor_number", false), new("型号", "model", false), new("分类", "category", false), new("内部参考日价", "internal_daily_rate", false)
        ],
        [ExportDataset.EquipmentLeases] =
        [
            new("设备编号", "equipment_number", true), new("出租方编号", "lessor_number", true), new("租赁合同号", "contract_number", false), new("开始日期", "start_date", true), new("结束日期", "end_date", false), new("计租方式", "rent_mode", true), new("基础单价", "unit_rate", true)
        ],
        [ExportDataset.EquipmentUsages] =
        [
            new("设备编号", "equipment_number", true), new("项目编号", "project_number", true), new("公司编码", "company_code", true), new("进场日期", "entry_date", true), new("退场日期", "exit_date", false), new("计租方式", "rent_mode", true), new("基础单价", "unit_rate", false)
        ],
        [ExportDataset.EquipmentPeriods] =
        [
            new("设备编号", "equipment_number", true), new("项目编号", "project_number", true), new("进场日期", "usage_entry_date", true), new("开始日期", "start_date", true), new("结束日期", "end_date", true), new("日期段类型", "period_type", true), new("是否计租", "chargeable", false), new("备注", "notes", false)
        ],
        [ExportDataset.EquipmentSettlements] =
        [
            new("设备编号", "equipment_number", true), new("项目编号", "project_number", true), new("进场日期", "usage_entry_date", true), new("结算日期", "settlement_date", true), new("基础租金", "base_amount", true), new("结算总额", "total_amount", true), new("抵扣金额", "offset_amount", false), new("修改原因", "reason", true)
        ]
    };

    public IReadOnlyList<ExportDataset> ImportableDatasets { get; } =
        Columns.Keys.OrderBy(item => item).ToArray();

    public Task<ExportFileResult> GenerateTemplateAsync(ExportDataset dataset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var columns = GetColumns(dataset);
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(TemplateSheetName(dataset), columns.Select(item => item.Header).ToArray(), []);
        workbook.AddWorksheet("导入说明", ["项目", "说明"],
        [
            ["导入方式", "支持新增、更新和混合模式；更新时优先使用系统ID，其次使用稳定业务编号。"],
            ["校验规则", "整批校验，任意一行错误都不会写入。"],
            ["字段映射", "可使用标准表头，也可在上传时映射任意 Excel 表头。"],
            ["删除规则", "导入不会物理删除数据。"]
        ]);
        return Task.FromResult(new ExportFileResult($"{TemplateSheetName(dataset)}模板.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", workbook.ToArray()));
    }

    public async Task<ImportPreviewDto> PreviewAsync(ImportPreviewRequest request, CancellationToken cancellationToken)
    {
        var userId = NormalizeRequired(request.UserId, nameof(request.UserId));
        var fileName = NormalizeRequired(request.OriginalFileName, nameof(request.OriginalFileName));
        if (request.Content.Length == 0)
        {
            throw new ArgumentException("导入文件不能为空。", nameof(request));
        }

        var sheets = SimpleXlsxReader.Read(request.Content);
        if (sheets.Count == 0)
        {
            throw new InvalidDataException("导入文件没有工作表。");
        }

        IReadOnlyList<ImportErrorDto> errors;
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        int totalRows;
        if (IsCompleteEmployeeWorkbook(request.Dataset, sheets))
        {
            var analysis = await new EmployeeWorkbookImporter(db).AnalyzeAsync(sheets, fileName, cancellationToken);
            errors = analysis.Errors;
            totalRows = analysis.TotalRows;
            mapping[CompleteEmployeeWorkbookMarker] = "true";
        }
        else
        {
            var sheet = sheets[0];
            if (sheet.Rows.Count == 0)
            {
                throw new InvalidDataException("导入工作表没有表头。");
            }

            var headers = sheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
            mapping = ResolveMapping(request.Dataset, headers, request.SourceToTargetMapping);
            errors = await ValidateRowsAsync(request.Dataset, sheet.Rows.Skip(1).ToArray(), headers, mapping, request.Mode, cancellationToken);
            totalRows = Math.Max(sheet.Rows.Count - 1, 0);
        }

        var errorRows = errors.Select(item => item.RowNumber).Distinct().Count();
        var batch = new ImportBatch
        {
            CreatedByUserId = userId,
            Dataset = request.Dataset,
            OriginalFileName = fileName,
            OriginalContent = request.Content.ToArray(),
            MappingJson = JsonSerializer.Serialize(mapping),
            Mode = request.Mode,
            Status = DataExchangeTaskStatus.PreviewReady,
            TotalRows = totalRows,
            ValidRows = Math.Max(0, totalRows - errorRows),
            ErrorRows = errorRows
        };
        foreach (var error in errors)
        {
            batch.Errors.Add(new ImportError { Batch = batch, RowNumber = error.RowNumber, ColumnName = error.ColumnName, Message = error.Message, RawValue = error.RawValue });
        }

        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        return new ImportPreviewDto(batch.Id, batch.Dataset, batch.TotalRows, batch.ValidRows, batch.ErrorRows, errors);
    }

    public async Task ConfirmAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await db.ImportBatches.Include(item => item.Errors).SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken)
            ?? throw new InvalidOperationException("导入批次不存在。");
        if (batch.Status != DataExchangeTaskStatus.PreviewReady)
        {
            throw new InvalidOperationException("导入批次不处于可确认状态。");
        }

        if (batch.Errors.Count > 0)
        {
            throw new InvalidOperationException("导入预览仍有错误，不能确认导入。");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var sheets = SimpleXlsxReader.Read(batch.OriginalContent);
        if (sheets.Count == 0)
        {
            throw new InvalidDataException("导入文件没有工作表。");
        }

        if (IsCompleteEmployeeWorkbook(batch.Dataset, sheets))
        {
            var importer = new EmployeeWorkbookImporter(db);
            var analysis = await importer.AnalyzeAsync(sheets, batch.OriginalFileName, cancellationToken);
            if (analysis.Errors.Count > 0)
            {
                throw new InvalidOperationException("员工工作簿重新校验后存在错误，不能确认导入。");
            }

            await importer.ApplyAsync(analysis, batch.OriginalFileName, cancellationToken);
        }
        else
        {
            var sheet = sheets[0];
            if (sheet.Rows.Count == 0)
            {
                throw new InvalidDataException("导入工作表没有表头。");
            }

            var headers = sheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
            var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(batch.MappingJson) ?? [];
            var importRows = sheet.Rows.Skip(1)
                .Select((row, index) => new FinanceImportRow(index + 2, RowValues(headers, row, mapping)))
                .ToArray();
            if (IsCentralFinanceDataset(batch.Dataset))
            {
                await ApplyCentralFinanceGroupsAsync(batch.Dataset, importRows, batch.Mode, batch.CreatedByUserId, cancellationToken);
            }
            else
            {
                foreach (var row in importRows)
                {
                    AddOrUpdateEntity(batch.Dataset, row.Values, batch.Mode);
                }
            }
        }

        batch.Status = DataExchangeTaskStatus.Completed;
        batch.CompletedAt = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new AuditLog { UserId = batch.CreatedByUserId, Action = "DataImport", EntityType = nameof(ImportBatch), EntityId = batch.Id.ToString(), Reason = $"导入 {DataExchangeValueLabels.Dataset(batch.Dataset)}", AfterJson = JsonSerializer.Serialize(new { batch.Dataset, batch.Mode, batch.TotalRows, batch.ValidRows }) });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var entityTypes = string.Join(", ", exception.Entries.Select(item => $"{item.Metadata.ClrType.Name}:{item.State}"));
            throw new DbUpdateConcurrencyException($"通用导入写入发生并发冲突：{entityTypes}", exception);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static bool IsCentralFinanceDataset(ExportDataset dataset) =>
        dataset is ExportDataset.Collections or ExportDataset.Payments or ExportDataset.Invoices;

    private static IEnumerable<IGrouping<string, FinanceImportRow>> GroupCentralFinanceRows(IReadOnlyList<FinanceImportRow> rows) =>
        rows.GroupBy(
            row => string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("_system_id"))
                ? $"row:{row.RowNumber}"
                : $"id:{row.Values.GetValueOrDefault("_system_id")}",
            StringComparer.OrdinalIgnoreCase);

    private async Task ValidateCentralFinanceGroupsAsync(
        ExportDataset dataset,
        IReadOnlyList<FinanceImportRow> rows,
        ImportMode mode,
        List<ImportErrorDto> errors,
        CancellationToken cancellationToken)
    {
        foreach (var group in GroupCentralFinanceRows(rows))
        {
            try
            {
                if (dataset is ExportDataset.Collections or ExportDataset.Payments)
                {
                    var plan = BuildCashImportPlan(dataset, group.ToArray());
                    await ValidateCashImportPlanAsync(plan, mode, cancellationToken);
                }
                else
                {
                    var plan = BuildInvoiceImportPlan(group.ToArray());
                    await ValidateInvoiceImportPlanAsync(plan, mode, cancellationToken);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                errors.Add(new ImportErrorDto(group.First().RowNumber, "财务记录", exception.Message, group.First().Values.GetValueOrDefault("_system_id")));
            }
        }
    }

    private async Task ApplyCentralFinanceGroupsAsync(
        ExportDataset dataset,
        IReadOnlyList<FinanceImportRow> rows,
        ImportMode mode,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        foreach (var group in GroupCentralFinanceRows(rows))
        {
            if (dataset is ExportDataset.Collections or ExportDataset.Payments)
            {
                var plan = BuildCashImportPlan(dataset, group.ToArray());
                var existing = await ValidateCashImportPlanAsync(plan, mode, cancellationToken);
                await ApplyCashImportPlanAsync(plan, existing, actorUserId, cancellationToken);
            }
            else
            {
                var plan = BuildInvoiceImportPlan(group.ToArray());
                var existing = await ValidateInvoiceImportPlanAsync(plan, mode, cancellationToken);
                await ApplyInvoiceImportPlanAsync(plan, existing, actorUserId, cancellationToken);
            }
        }
    }

    private CashImportPlan BuildCashImportPlan(ExportDataset dataset, IReadOnlyList<FinanceImportRow> rows)
    {
        if (rows.Count == 0) throw new InvalidOperationException("财务导入分组不能为空。");
        var direction = dataset == ExportDataset.Collections ? LedgerDirection.Receivable : LedgerDirection.Payable;
        var cashType = dataset == ExportDataset.Collections ? LedgerCashType.Collection : LedgerCashType.Payment;
        var dateKey = dataset == ExportDataset.Collections ? "collection_date" : "payment_date";
        var systemId = ParseOptionalGuid(CommonValue(rows, "_system_id", "系统ID"), "系统ID");
        var legalEntity = ResolveLegalEntity(CommonValue(rows, "legal_entity_code", "签约公司编码"), CommonValue(rows, "legal_entity", "签约公司"));
        var partner = ResolvePartner(CommonValue(rows, "partner_number", "合作单位编号"), CommonValue(rows, "partner", "合作单位"))
            ?? throw new InvalidOperationException("中央现金记录必须匹配到合作单位。");
        var account = ResolveAccount(legalEntity.Id, CommonValue(rows, "account_number", "账户账号"), CommonValue(rows, "account", "账户"))
            ?? throw new InvalidOperationException("中央现金记录无法匹配到账户。");
        var businessDate = ParseDate(CommonValue(rows, dateKey, dataset == ExportDataset.Collections ? "收款日期" : "付款日期"))
            ?? throw new InvalidOperationException($"{(dataset == ExportDataset.Collections ? "收款日期" : "付款日期")}无法解析。");
        if (!TryParsePaymentMethod(CommonValue(rows, "payment_method", "付款方式"), out var paymentMethod))
            throw new InvalidOperationException("付款方式无法解析。");

        var contexts = rows.Select(ResolveFinanceRowContext).ToArray();
        var projectIds = contexts.Select(item => item.ProjectId).Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        var contractIds = contexts.Select(item => item.ContractId).Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        var hasAllocationColumns = HasAnyColumn(rows, "settlement_id", "allocation_amount", "source_amount");
        if (rows.Count > 1 && !hasAllocationColumns)
            throw new InvalidOperationException("同一资金记录包含多行时必须包含结算系统ID、源记录总额和分摊金额字段。");
        var amount = ResolveParentAmount(rows, "amount");
        var allocations = BuildFinanceAllocations(rows, contexts, "amount");

        return new CashImportPlan(
            systemId,
            direction,
            cashType,
            legalEntity,
            partner,
            account,
            businessDate,
            amount,
            paymentMethod.ToString(),
            CommonValue(rows, "notes", "备注"),
            projectIds.Length == 1 ? projectIds[0] : null,
            projectIds.Length <= 1 && contractIds.Length == 1 ? contractIds[0] : null,
            allocations,
            hasAllocationColumns,
            HasAnyColumn(rows, "project_number"),
            HasAnyColumn(rows, "contract_number"),
            HasAnyColumn(rows, "notes"));
    }

    private InvoiceImportPlan BuildInvoiceImportPlan(IReadOnlyList<FinanceImportRow> rows)
    {
        if (rows.Count == 0) throw new InvalidOperationException("财务导入分组不能为空。");
        var systemId = ParseOptionalGuid(CommonValue(rows, "_system_id", "系统ID"), "系统ID");
        var legalEntity = ResolveLegalEntity(CommonValue(rows, "legal_entity_code", "签约公司编码"), CommonValue(rows, "legal_entity", "签约公司"));
        var partner = ResolvePartner(CommonValue(rows, "partner_number", "合作单位编号"), CommonValue(rows, "partner", "合作单位"))
            ?? throw new InvalidOperationException("中央发票必须匹配到合作单位。");
        if (!TryParseLedgerDirection(CommonValue(rows, "direction", "发票方向"), out var direction))
            throw new InvalidOperationException("发票方向无法解析。");
        if (!TryParseLedgerStatus(CommonValue(rows, "status", "状态"), out var status))
            throw new InvalidOperationException("发票状态无法解析。");
        var invoiceDate = ParseDate(CommonValue(rows, "invoice_date", "发票日期"))
            ?? throw new InvalidOperationException("发票日期无法解析。");
        var invoiceNumber = CommonValue(rows, "invoice_number", "发票号码")
            ?? throw new InvalidOperationException("发票号码不能为空。");
        var contexts = rows.Select(ResolveFinanceRowContext).ToArray();
        var projectIds = contexts.Select(item => item.ProjectId).Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        var contractIds = contexts.Select(item => item.ContractId).Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        var hasAllocationColumns = HasAnyColumn(rows, "settlement_id", "allocation_amount", "source_amount");
        if (rows.Count > 1 && !hasAllocationColumns)
            throw new InvalidOperationException("同一发票包含多行时必须包含结算系统ID、源记录总额和分摊金额字段。");

        return new InvoiceImportPlan(
            systemId,
            direction,
            legalEntity,
            partner,
            invoiceNumber,
            invoiceDate,
            ResolveParentAmount(rows, "gross_amount"),
            SumOptionalDecimal(rows, "net_amount", "未税金额"),
            SumOptionalDecimal(rows, "tax_amount", "税额"),
            CommonDecimal(rows, "tax_rate", "税率"),
            ParseOptionalGuid(CommonValue(rows, "project_tax_configuration_id", "项目税务配置ID"), "项目税务配置ID"),
            CommonValue(rows, "invoice_type", "发票类型"),
            status,
            CommonValue(rows, "notes", "备注"),
            projectIds.Length == 1 ? projectIds[0] : null,
            projectIds.Length <= 1 && contractIds.Length == 1 ? contractIds[0] : null,
            BuildFinanceAllocations(rows, contexts, "gross_amount"),
            hasAllocationColumns,
            HasAnyColumn(rows, "net_amount"),
            HasAnyColumn(rows, "tax_amount"),
            HasAnyColumn(rows, "tax_rate"),
            HasAnyColumn(rows, "project_tax_configuration_id"),
            HasAnyColumn(rows, "invoice_type"),
            HasAnyColumn(rows, "project_number"),
            HasAnyColumn(rows, "contract_number"),
            HasAnyColumn(rows, "notes"));
    }

    private async Task<FinanceCashEntry?> ValidateCashImportPlanAsync(CashImportPlan plan, ImportMode mode, CancellationToken cancellationToken)
    {
        var existing = plan.SystemId.HasValue
            ? await db.FinanceCashEntries.Include(item => item.Allocations).SingleOrDefaultAsync(item => item.Id == plan.SystemId.Value, cancellationToken)
            : null;
        EnsureFinanceImportMode(mode, plan.SystemId, existing, "资金记录");
        var projectId = plan.HasProjectColumn ? plan.ProjectId : existing?.ProjectId;
        var contractId = plan.HasContractColumn ? plan.ContractId : existing?.ContractId;
        var allocations = EffectiveCashAllocations(plan, existing);
        foreach (var allocation in plan.Allocations)
        {
            await centralLedgerCommands.ValidateImportedAllocationAsync(
                plan.Direction,
                plan.LegalEntity.Id,
                plan.BusinessPartner.Id,
                allocation.ProjectId,
                allocation.ContractId,
                new FinanceAllocationRequest(allocation.SettlementId, allocation.Amount, allocation.AllocationOrder),
                cancellationToken);
        }
        await centralLedgerCommands.ValidateImportedCashAsync(
            new CreateFinanceCashRequest(
                LedgerScope.External,
                plan.Direction,
                plan.CashType,
                LedgerSourceType.CentralLedger,
                null,
                plan.LegalEntity.Id,
                plan.BusinessPartner.Id,
                null,
                plan.Account.Id,
                null,
                plan.BusinessDate,
                plan.Amount,
                plan.PaymentMethod,
                plan.HasNotes ? plan.Notes : existing?.Notes,
                allocations,
                ProjectId: projectId,
                ContractId: contractId,
                EntryId: plan.SystemId),
            existing,
            cancellationToken);
        return existing;
    }

    private async Task<FinanceInvoice?> ValidateInvoiceImportPlanAsync(InvoiceImportPlan plan, ImportMode mode, CancellationToken cancellationToken)
    {
        var existing = plan.SystemId.HasValue
            ? await db.FinanceInvoices.Include(item => item.Allocations).SingleOrDefaultAsync(item => item.Id == plan.SystemId.Value, cancellationToken)
            : null;
        EnsureFinanceImportMode(mode, plan.SystemId, existing, "发票记录");
        var projectId = plan.HasProjectColumn ? plan.ProjectId : existing?.ProjectId;
        var contractId = plan.HasContractColumn ? plan.ContractId : existing?.ContractId;
        var allocations = EffectiveInvoiceAllocations(plan, existing);
        foreach (var allocation in plan.Allocations)
        {
            await centralLedgerCommands.ValidateImportedAllocationAsync(
                plan.Direction,
                plan.LegalEntity.Id,
                plan.BusinessPartner.Id,
                allocation.ProjectId,
                allocation.ContractId,
                new FinanceAllocationRequest(allocation.SettlementId, allocation.Amount, allocation.AllocationOrder),
                cancellationToken);
        }
        await centralLedgerCommands.ValidateImportedInvoiceAsync(
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                plan.Direction,
                LedgerSourceType.CentralLedger,
                null,
                plan.LegalEntity.Id,
                plan.BusinessPartner.Id,
                null,
                plan.InvoiceNumber,
                plan.InvoiceDate,
                plan.Amount,
                plan.HasNetAmount ? plan.NetAmount : existing?.NetAmount,
                plan.HasTaxAmount ? plan.TaxAmount : existing?.TaxAmount,
                plan.HasTaxRate ? plan.TaxRate : existing?.TaxRate,
                plan.HasNotes ? plan.Notes : existing?.Notes,
                allocations,
                ProjectTaxConfigurationId: plan.HasTaxConfiguration ? plan.ProjectTaxConfigurationId : existing?.ProjectTaxConfigurationId,
                InvoiceType: plan.HasInvoiceType ? plan.InvoiceType : existing?.InvoiceType,
                Status: plan.Status,
                ProjectId: projectId,
                ContractId: contractId),
            existing,
            cancellationToken);
        return existing;
    }

    private async Task ApplyCashImportPlanAsync(CashImportPlan plan, FinanceCashEntry? existing, string actorUserId, CancellationToken cancellationToken)
    {
        var projectId = plan.HasProjectColumn ? plan.ProjectId : existing?.ProjectId;
        var contractId = plan.HasContractColumn ? plan.ContractId : existing?.ContractId;
        var cash = existing ?? new FinanceCashEntry { Id = plan.SystemId ?? Guid.NewGuid(), SourceType = LedgerSourceType.CentralLedger, CreatedByUserId = actorUserId };
        if (existing is null) db.FinanceCashEntries.Add(cash);
        cash.Scope = LedgerScope.External;
        cash.Direction = plan.Direction;
        cash.CashType = plan.CashType;
        cash.LegalEntityId = plan.LegalEntity.Id;
        cash.BusinessPartnerId = plan.BusinessPartner.Id;
        cash.ProjectId = projectId;
        cash.ContractId = contractId;
        cash.AccountId = plan.Account.Id;
        cash.BusinessDate = plan.BusinessDate;
        cash.Amount = plan.Amount;
        cash.PaymentMethod = plan.PaymentMethod;
        cash.Notes = plan.HasNotes ? plan.Notes : existing?.Notes;
        cash.UpdatedAt = DateTimeOffset.UtcNow;
        cash.ConcurrencyStamp = Guid.NewGuid();

        if (plan.HasAllocationColumns || existing is null)
        {
            if (existing is not null)
            {
                db.FinanceCashAllocations.RemoveRange(cash.Allocations);
                cash.Allocations.Clear();
            }
            await AddCashAllocationsAsync(cash, plan.Allocations, cancellationToken);
        }
        await centralLedgerCommands.SyncImportedCashAccountTransactionsAsync(cash, cancellationToken);
    }

    private async Task ApplyInvoiceImportPlanAsync(InvoiceImportPlan plan, FinanceInvoice? existing, string actorUserId, CancellationToken cancellationToken)
    {
        var projectId = plan.HasProjectColumn ? plan.ProjectId : existing?.ProjectId;
        var contractId = plan.HasContractColumn ? plan.ContractId : existing?.ContractId;
        var invoice = existing ?? new FinanceInvoice { Id = plan.SystemId ?? Guid.NewGuid(), SourceType = LedgerSourceType.CentralLedger, CreatedByUserId = actorUserId };
        if (existing is null) db.FinanceInvoices.Add(invoice);
        invoice.Scope = LedgerScope.External;
        invoice.Direction = plan.Direction;
        invoice.LegalEntityId = plan.LegalEntity.Id;
        invoice.BusinessPartnerId = plan.BusinessPartner.Id;
        invoice.ProjectId = projectId;
        invoice.ContractId = contractId;
        invoice.InvoiceNumber = plan.InvoiceNumber;
        invoice.InvoiceDate = plan.InvoiceDate;
        invoice.Amount = plan.Amount;
        invoice.NetAmount = plan.HasNetAmount ? plan.NetAmount : existing?.NetAmount;
        invoice.TaxAmount = plan.HasTaxAmount ? plan.TaxAmount : existing?.TaxAmount;
        invoice.TaxRate = plan.HasTaxRate ? plan.TaxRate : existing?.TaxRate;
        invoice.ProjectTaxConfigurationId = plan.HasTaxConfiguration ? plan.ProjectTaxConfigurationId : existing?.ProjectTaxConfigurationId;
        invoice.InvoiceType = plan.HasInvoiceType ? plan.InvoiceType : existing?.InvoiceType;
        invoice.Status = plan.Status;
        invoice.Notes = plan.HasNotes ? plan.Notes : existing?.Notes;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        invoice.ConcurrencyStamp = Guid.NewGuid();

        if (plan.HasAllocationColumns || existing is null)
        {
            if (existing is not null)
            {
                db.FinanceInvoiceAllocations.RemoveRange(invoice.Allocations);
                invoice.Allocations.Clear();
            }
            await AddInvoiceAllocationsAsync(invoice, plan.Allocations, cancellationToken);
        }
    }

    private async Task AddCashAllocationsAsync(FinanceCashEntry cash, IReadOnlyList<FinanceAllocationImport> allocations, CancellationToken cancellationToken)
    {
        foreach (var allocation in allocations)
        {
            var settlement = db.FinanceSettlements.Local.SingleOrDefault(item => item.Id == allocation.SettlementId)
                ?? await db.FinanceSettlements.SingleAsync(item => item.Id == allocation.SettlementId, cancellationToken);
            db.FinanceCashAllocations.Add(new FinanceCashAllocation
            {
                CashEntry = cash,
                Settlement = settlement,
                ProjectId = settlement.ProjectId,
                ContractId = settlement.ContractId,
                ContractLineItemId = settlement.ContractLineItemId,
                BusinessPartnerId = settlement.BusinessPartnerId,
                CounterLegalEntityId = settlement.CounterLegalEntityId,
                Amount = allocation.Amount,
                AllocationOrder = allocation.AllocationOrder
            });
        }
    }

    private async Task AddInvoiceAllocationsAsync(FinanceInvoice invoice, IReadOnlyList<FinanceAllocationImport> allocations, CancellationToken cancellationToken)
    {
        foreach (var allocation in allocations)
        {
            var settlement = db.FinanceSettlements.Local.SingleOrDefault(item => item.Id == allocation.SettlementId)
                ?? await db.FinanceSettlements.SingleAsync(item => item.Id == allocation.SettlementId, cancellationToken);
            db.FinanceInvoiceAllocations.Add(new FinanceInvoiceAllocation
            {
                Invoice = invoice,
                Settlement = settlement,
                ProjectId = settlement.ProjectId,
                ContractId = settlement.ContractId,
                ContractLineItemId = settlement.ContractLineItemId,
                BusinessPartnerId = settlement.BusinessPartnerId,
                CounterLegalEntityId = settlement.CounterLegalEntityId,
                Amount = allocation.Amount,
                AllocationOrder = allocation.AllocationOrder
            });
        }
    }

    private static FinanceAllocationRequest[] EffectiveCashAllocations(CashImportPlan plan, FinanceCashEntry? existing) =>
        plan.HasAllocationColumns
            ? plan.Allocations.Select(item => new FinanceAllocationRequest(item.SettlementId, item.Amount, item.AllocationOrder)).ToArray()
            : existing?.Allocations.OrderBy(item => item.AllocationOrder).Select(item => new FinanceAllocationRequest(item.SettlementId, item.Amount, item.AllocationOrder)).ToArray() ?? [];

    private static FinanceAllocationRequest[] EffectiveInvoiceAllocations(InvoiceImportPlan plan, FinanceInvoice? existing) =>
        plan.HasAllocationColumns
            ? plan.Allocations.Select(item => new FinanceAllocationRequest(item.SettlementId, item.Amount, item.AllocationOrder)).ToArray()
            : existing?.Allocations.OrderBy(item => item.AllocationOrder).Select(item => new FinanceAllocationRequest(item.SettlementId, item.Amount, item.AllocationOrder)).ToArray() ?? [];

    private static List<FinanceAllocationImport> BuildFinanceAllocations(
        IReadOnlyList<FinanceImportRow> rows,
        (Guid? ProjectId, Guid? ContractId)[] contexts,
        string amountKey)
    {
        var result = new List<FinanceAllocationImport>();
        for (var index = 0; index < rows.Count; index++)
        {
            var settlementText = rows[index].Values.GetValueOrDefault("settlement_id");
            if (string.IsNullOrWhiteSpace(settlementText)) continue;
            if (!Guid.TryParse(settlementText, out var settlementId))
                throw new InvalidOperationException($"第 {rows[index].RowNumber} 行结算系统ID无法解析。");
            var amount = ParseDecimal(rows[index].Values.GetValueOrDefault("allocation_amount"))
                ?? ParseDecimal(rows[index].Values.GetValueOrDefault(amountKey))
                ?? 0m;
            result.Add(new FinanceAllocationImport(settlementId, amount, result.Count + 1, contexts[index].ProjectId, contexts[index].ContractId, rows[index].RowNumber));
        }
        return result;
    }

    private (Guid? ProjectId, Guid? ContractId) ResolveFinanceRowContext(FinanceImportRow row)
    {
        var projectNumber = row.Values.GetValueOrDefault("project_number");
        var project = ResolveProject(projectNumber);
        if (!string.IsNullOrWhiteSpace(projectNumber) && project is null)
            throw new InvalidOperationException($"第 {row.RowNumber} 行项目编号不存在。");
        var contractNumber = row.Values.GetValueOrDefault("contract_number");
        if (string.IsNullOrWhiteSpace(contractNumber)) return (project?.Id, null);
        if (project is null) throw new InvalidOperationException($"第 {row.RowNumber} 行选择合同前必须填写项目编号。");
        var contract = db.Contracts.Local.FirstOrDefault(item => item.ProjectId == project.Id && item.ContractNumber == contractNumber)
            ?? db.Contracts.FirstOrDefault(item => item.ProjectId == project.Id && item.ContractNumber == contractNumber);
        if (contract is null) throw new InvalidOperationException($"第 {row.RowNumber} 行合同编号不存在或不属于当前项目。");
        return (project.Id, contract.Id);
    }

    private static decimal ResolveParentAmount(IReadOnlyList<FinanceImportRow> rows, string amountKey)
    {
        var sourceAmount = CommonDecimal(rows, "source_amount", "源记录总额");
        if (sourceAmount.HasValue) return sourceAmount.Value;
        if (rows.Count == 1) return ParseDecimal(rows[0].Values.GetValueOrDefault(amountKey)) ?? 0m;
        return rows.Sum(row => ParseDecimal(row.Values.GetValueOrDefault("allocation_amount")) ?? ParseDecimal(row.Values.GetValueOrDefault(amountKey)) ?? 0m);
    }

    private static decimal? CommonDecimal(IReadOnlyList<FinanceImportRow> rows, string key, string label)
    {
        if (!HasAnyColumn(rows, key)) return null;
        var values = rows.Select(row => ParseDecimal(row.Values.GetValueOrDefault(key))).Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        if (values.Length > 1) throw new InvalidOperationException($"同一财务记录的{label}不一致。");
        return values.Length == 0 ? null : values[0];
    }

    private static decimal? SumOptionalDecimal(IReadOnlyList<FinanceImportRow> rows, string key, string label)
    {
        if (!HasAnyColumn(rows, key)) return null;
        var values = rows.Select(row => ParseDecimal(row.Values.GetValueOrDefault(key))).ToArray();
        if (values.All(item => !item.HasValue)) return null;
        if (values.Any(item => !item.HasValue)) throw new InvalidOperationException($"同一财务记录的{label}必须全部填写或全部留空。");
        return values.Sum(item => item!.Value);
    }

    private static string? CommonValue(IReadOnlyList<FinanceImportRow> rows, string key, string label)
    {
        if (!HasAnyColumn(rows, key)) return null;
        var values = rows.Select(row => NormalizeOptional(row.Values.GetValueOrDefault(key))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (values.Length > 1) throw new InvalidOperationException($"同一财务记录的{label}不一致。");
        return values[0];
    }

    private static bool HasAnyColumn(IReadOnlyList<FinanceImportRow> rows, params string[] keys) =>
        rows.Any(row => keys.Any(row.Values.ContainsKey));

    private static Guid? ParseOptionalGuid(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Guid.TryParse(value, out var id) ? id : throw new InvalidOperationException($"{label}无法解析。");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureFinanceImportMode(ImportMode mode, Guid? systemId, object? existing, string label)
    {
        if (mode == ImportMode.New && existing is not null) throw new InvalidOperationException($"仅新增模式不能覆盖已有{label}。");
        if (mode == ImportMode.Update && !systemId.HasValue) throw new InvalidOperationException($"仅更新模式必须提供{label}系统ID。");
        if (mode == ImportMode.Update && existing is null) throw new InvalidOperationException($"仅更新模式找不到已有{label}。");
    }

    private async Task<List<ImportErrorDto>> ValidateRowsAsync(
        ExportDataset dataset,
        IReadOnlyList<object?>[] rows,
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, string> mapping,
        ImportMode requestMode,
        CancellationToken cancellationToken)
    {
        var errors = new List<ImportErrorDto>();
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var numberKey = dataset switch
        {
            ExportDataset.Employees => "employee_number",
            ExportDataset.Payroll => "batch_number",
            ExportDataset.Partners => "partner_number",
            ExportDataset.Projects => "project_number",
            ExportDataset.Companies => "company_code",
            ExportDataset.Equipment => "equipment_number",
            _ => string.Empty
        };
        for (var index = 0; index < rows.Length; index++)
        {
            var excelRow = index + 2;
            var values = RowValues(headers, rows[index], mapping);
            foreach (var column in GetColumns(dataset).Where(item => item.Required))
            {
                if (!values.TryGetValue(column.Key, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    errors.Add(new ImportErrorDto(excelRow, column.Header, "必填字段不能为空。", value));
                }
            }

            if (dataset != ExportDataset.Payroll && values.TryGetValue(numberKey, out var number) && !string.IsNullOrWhiteSpace(number) && !seenNumbers.Add(number))
            {
                errors.Add(new ImportErrorDto(excelRow, HeaderFor(dataset, numberKey), "文件内编号重复。", number));
            }

            if (dataset == ExportDataset.Employees && values.TryGetValue("employee_type", out var type) && !string.IsNullOrWhiteSpace(type) && !TryParseEmployeeType(type, out _))
            {
                errors.Add(new ImportErrorDto(excelRow, "员工类型", "员工类型必须是正式员工、劳务员工或特殊临时人员。", type));
            }
            if (dataset == ExportDataset.Employees && Guid.TryParse(values.GetValueOrDefault("_concurrency_stamp"), out var expectedStamp))
            {
                var employeeNumber = values.GetValueOrDefault("employee_number");
                var currentStamp = await db.Employees.Where(item => item.EmployeeNumber == employeeNumber).Select(item => (Guid?)item.ConcurrencyStamp).SingleOrDefaultAsync(cancellationToken);
                if (currentStamp.HasValue && currentStamp.Value != expectedStamp)
                {
                    errors.Add(new ImportErrorDto(excelRow, "并发版本", "员工已被其他用户修改，请重新导出后再导入。", values.GetValueOrDefault("_concurrency_stamp")));
                }
            }

            if (dataset is ExportDataset.CompanyAccounts or ExportDataset.CompanyCertificates)
            {
                var companyCode = values.GetValueOrDefault("company_code");
                if (!string.IsNullOrWhiteSpace(companyCode) && !await db.LegalEntities.AnyAsync(item => item.Code == companyCode, cancellationToken))
                {
                    errors.Add(new ImportErrorDto(excelRow, "公司编码", "公司编码不存在。", companyCode));
                }
            }
            if (dataset == ExportDataset.EmployeeCertificates)
            {
                var employeeNumber = values.GetValueOrDefault("employee_number");
                if (!string.IsNullOrWhiteSpace(employeeNumber) && !await db.Employees.AnyAsync(item => item.EmployeeNumber == employeeNumber, cancellationToken))
                {
                    errors.Add(new ImportErrorDto(excelRow, "员工编号", "员工编号不存在。", employeeNumber));
                }
            }
            if (dataset == ExportDataset.Companies)
            {
                var categoryCode = values.GetValueOrDefault("category_code");
                if (!string.IsNullOrWhiteSpace(categoryCode) && !await db.CompanyCategories.AnyAsync(item => item.Code == categoryCode && item.IsActive, cancellationToken))
                {
                    errors.Add(new ImportErrorDto(excelRow, "组合分类编码", "组合分类不存在或已停用。", categoryCode));
                }
            }
            if (dataset == ExportDataset.CompanyAccounts)
            {
                if (!TryParseAccountType(values.GetValueOrDefault("account_type"), out _))
                {
                    errors.Add(new ImportErrorDto(excelRow, "账户类型", "账户类型必须是银行、现金或其他。", values.GetValueOrDefault("account_type")));
                }
                if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("opening_balance")) && !decimal.TryParse(values.GetValueOrDefault("opening_balance"), out _))
                {
                    errors.Add(new ImportErrorDto(excelRow, "期初余额", "期初余额必须是数字。", values.GetValueOrDefault("opening_balance")));
                }
            }
            if (dataset is ExportDataset.CompanyCertificates or ExportDataset.EmployeeCertificates)
            {
                ValidateDate(values.GetValueOrDefault("issued_on"), excelRow, "签发日期", errors);
                ValidateDate(values.GetValueOrDefault("expires_on"), excelRow, "到期日期", errors);
                var issuedOn = ParseDate(values.GetValueOrDefault("issued_on"));
                var expiresOn = ParseDate(values.GetValueOrDefault("expires_on"));
                if (issuedOn.HasValue && expiresOn.HasValue && expiresOn < issuedOn)
                {
                    errors.Add(new ImportErrorDto(excelRow, "到期日期", "到期日期不能早于签发日期。", values.GetValueOrDefault("expires_on")));
                }
            }
            if (dataset == ExportDataset.Equipment)
            {
                var ownership = values.GetValueOrDefault("ownership");
                if (!TryParseOwnership(ownership, out var ownershipType)) errors.Add(new ImportErrorDto(excelRow, "权属", "权属必须是自有、租赁或其他。", ownership));
                var companyCode = values.GetValueOrDefault("owner_company_code");
                var lessorNumber = values.GetValueOrDefault("lessor_number");
                if (ownershipType == EquipmentOwnershipType.SelfOwned && (string.IsNullOrWhiteSpace(companyCode) || !await db.LegalEntities.AnyAsync(item => item.Code == companyCode, cancellationToken))) errors.Add(new ImportErrorDto(excelRow, "所属公司编码", "自有设备必须填写存在的所属公司编码。", companyCode));
                if (ownershipType == EquipmentOwnershipType.Rented && (string.IsNullOrWhiteSpace(lessorNumber) || !await db.BusinessPartners.AnyAsync(item => item.PartnerNumber == lessorNumber, cancellationToken))) errors.Add(new ImportErrorDto(excelRow, "出租方编号", "租赁设备必须填写存在的出租方编号。", lessorNumber));
                ValidateDecimal(values.GetValueOrDefault("internal_daily_rate"), excelRow, "内部参考日价", errors);
            }
            if (dataset is ExportDataset.EquipmentLeases or ExportDataset.EquipmentUsages or ExportDataset.EquipmentPeriods or ExportDataset.EquipmentSettlements)
            {
                var equipmentNumber = values.GetValueOrDefault("equipment_number");
                if (!await db.Equipment.AnyAsync(item => item.EquipmentNumber == equipmentNumber, cancellationToken)) errors.Add(new ImportErrorDto(excelRow, "设备编号", "设备编号不存在。", equipmentNumber));
            }
            if (dataset == ExportDataset.EquipmentLeases)
            {
                if (!await db.BusinessPartners.AnyAsync(item => item.PartnerNumber == values.GetValueOrDefault("lessor_number"), cancellationToken)) errors.Add(new ImportErrorDto(excelRow, "出租方编号", "出租方编号不存在。", values.GetValueOrDefault("lessor_number")));
                ValidateDate(values.GetValueOrDefault("start_date"), excelRow, "开始日期", errors); ValidateDate(values.GetValueOrDefault("end_date"), excelRow, "结束日期", errors); ValidateDecimal(values.GetValueOrDefault("unit_rate"), excelRow, "基础单价", errors);
                if (!TryParseRentMode(values.GetValueOrDefault("rent_mode"), out _)) errors.Add(new ImportErrorDto(excelRow, "计租方式", "计租方式必须是日租、月租或阶段包干。", values.GetValueOrDefault("rent_mode")));
            }
            if (dataset is ExportDataset.EquipmentUsages or ExportDataset.EquipmentPeriods or ExportDataset.EquipmentSettlements)
            {
                if (!await db.Projects.AnyAsync(item => item.ProjectNumber == values.GetValueOrDefault("project_number"), cancellationToken)) errors.Add(new ImportErrorDto(excelRow, "项目编号", "项目编号不存在。", values.GetValueOrDefault("project_number")));
            }
            if (dataset == ExportDataset.EquipmentUsages)
            {
                if (!await db.LegalEntities.AnyAsync(item => item.Code == values.GetValueOrDefault("company_code"), cancellationToken)) errors.Add(new ImportErrorDto(excelRow, "公司编码", "公司编码不存在。", values.GetValueOrDefault("company_code")));
                ValidateDate(values.GetValueOrDefault("entry_date"), excelRow, "进场日期", errors); ValidateDate(values.GetValueOrDefault("exit_date"), excelRow, "退场日期", errors); ValidateDecimal(values.GetValueOrDefault("unit_rate"), excelRow, "基础单价", errors);
                if (!TryParseRentMode(values.GetValueOrDefault("rent_mode"), out _)) errors.Add(new ImportErrorDto(excelRow, "计租方式", "计租方式必须是日租、月租或阶段包干。", values.GetValueOrDefault("rent_mode")));
            }
            if (dataset == ExportDataset.EquipmentPeriods)
            {
                ValidateDate(values.GetValueOrDefault("usage_entry_date"), excelRow, "进场日期", errors); ValidateDate(values.GetValueOrDefault("start_date"), excelRow, "开始日期", errors); ValidateDate(values.GetValueOrDefault("end_date"), excelRow, "结束日期", errors);
                if (!TryParsePeriodType(values.GetValueOrDefault("period_type"), out _)) errors.Add(new ImportErrorDto(excelRow, "日期段类型", "日期段类型必须是施工或停工。", values.GetValueOrDefault("period_type")));
            }
            if (dataset == ExportDataset.EquipmentSettlements)
            {
                ValidateDate(values.GetValueOrDefault("usage_entry_date"), excelRow, "进场日期", errors); ValidateDate(values.GetValueOrDefault("settlement_date"), excelRow, "结算日期", errors); ValidateDecimal(values.GetValueOrDefault("base_amount"), excelRow, "基础租金", errors); ValidateDecimal(values.GetValueOrDefault("total_amount"), excelRow, "结算总额", errors); ValidateDecimal(values.GetValueOrDefault("offset_amount"), excelRow, "抵扣金额", errors);
            }
            if ((dataset is ExportDataset.Contracts or ExportDataset.StageResults) && !await db.Projects.AnyAsync(item => item.ProjectNumber == values.GetValueOrDefault("project_number"), cancellationToken))
            {
                errors.Add(new ImportErrorDto(excelRow, "项目编号", "项目编号不存在。", values.GetValueOrDefault("project_number")));
            }
            if (dataset == ExportDataset.Contracts)
            {
                ValidateDate(values.GetValueOrDefault("signed_date"), excelRow, "签订日期", errors);
                ValidateDecimal(values.GetValueOrDefault("total_amount"), excelRow, "合同金额", errors);
                if (!Enum.TryParse<ContractType>(values.GetValueOrDefault("contract_type"), true, out _)) errors.Add(new ImportErrorDto(excelRow, "合同类型", "合同类型无法识别。", values.GetValueOrDefault("contract_type")));
            }
            if (dataset == ExportDataset.StageResults)
            {
                ValidateDate(values.GetValueOrDefault("result_date"), excelRow, "成果日期", errors);
                if (!Enum.TryParse<EngineeringManager.Domain.StageResults.StageResultType>(values.GetValueOrDefault("result_type"), true, out _)) errors.Add(new ImportErrorDto(excelRow, "成果类型", "成果类型无法识别。", values.GetValueOrDefault("result_type")));
            }

            await ValidateExtendedRowAsync(dataset, values, excelRow, errors, requestMode, cancellationToken);
        }

        if (IsCentralFinanceDataset(dataset))
        {
            var financeRows = rows
                .Select((item, index) => new FinanceImportRow(index + 2, RowValues(headers, item, mapping)))
                .ToArray();
            await ValidateCentralFinanceGroupsAsync(dataset, financeRows, requestMode, errors, cancellationToken);
        }

        foreach (var number in seenNumbers)
        {
            var exists = dataset switch
            {
                ExportDataset.Employees => await db.Employees.AnyAsync(item => item.EmployeeNumber == number, cancellationToken),
                ExportDataset.Payroll => await db.PayrollBatches.AnyAsync(item => item.BatchNumber == number, cancellationToken),
                ExportDataset.Partners => await db.BusinessPartners.AnyAsync(item => item.PartnerNumber == number, cancellationToken),
                ExportDataset.Projects => await db.Projects.AnyAsync(item => item.ProjectNumber == number, cancellationToken),
                ExportDataset.Companies => await db.LegalEntities.AnyAsync(item => item.Code == number, cancellationToken),
                ExportDataset.Equipment => await db.Equipment.AnyAsync(item => item.EquipmentNumber == number, cancellationToken),
                _ => false
            };
            if (exists && requestMode == ImportMode.New)
            {
                var rowIndex = rows.Select((row, index) => new { row, index }).First(item => RowValues(headers, item.row, mapping).GetValueOrDefault(numberKey) == number).index + 2;
                errors.Add(new ImportErrorDto(rowIndex, HeaderFor(dataset, numberKey), "编号已存在。", number));
            }
            if (!exists && requestMode == ImportMode.Update)
            {
                var rowIndex = rows.Select((row, index) => new { row, index }).First(item => RowValues(headers, item.row, mapping).GetValueOrDefault(numberKey) == number).index + 2;
                errors.Add(new ImportErrorDto(rowIndex, HeaderFor(dataset, numberKey), "更新模式下找不到对应记录。", number));
            }
        }

        return errors;
    }

    private async Task ValidateExtendedRowAsync(
        ExportDataset dataset,
        IReadOnlyDictionary<string, string?> values,
        int row,
        List<ImportErrorDto> errors,
        ImportMode requestMode,
        CancellationToken cancellationToken)
    {
        if (dataset == ExportDataset.Payroll)
        {
            if (requestMode == ImportMode.New && await db.PayrollBatches.AnyAsync(item => item.BatchNumber == values.GetValueOrDefault("batch_number"), cancellationToken))
            {
                errors.Add(new ImportErrorDto(row, "批次编号", "批次编号已存在。", values.GetValueOrDefault("batch_number")));
            }
            ValidateDate(values.GetValueOrDefault("start_date"), row, "开始日期", errors);
            ValidateDate(values.GetValueOrDefault("end_date"), row, "结束日期", errors);
            ValidateDate(values.GetValueOrDefault("payment_date"), row, "发放日期", errors);
            ValidateDecimal(values.GetValueOrDefault("actual_amount"), row, "实际总额", errors);
            if (!TryParsePayrollBatchType(values.GetValueOrDefault("batch_type"), out _)) errors.Add(new ImportErrorDto(row, "批次类型", "批次类型无法识别。", values.GetValueOrDefault("batch_type")));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("payment_method")) && !TryParsePaymentMethod(values.GetValueOrDefault("payment_method"), out _)) errors.Add(new ImportErrorDto(row, "付款方式", "付款方式无法识别。", values.GetValueOrDefault("payment_method")));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("project_number")) && !await db.Projects.AnyAsync(item => item.ProjectNumber == values.GetValueOrDefault("project_number"), cancellationToken)) errors.Add(new ImportErrorDto(row, "项目编号", "项目编号不存在。", values.GetValueOrDefault("project_number")));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("legal_entity_code")) && !await db.LegalEntities.AnyAsync(item => item.Code == values.GetValueOrDefault("legal_entity_code"), cancellationToken)) errors.Add(new ImportErrorDto(row, "公司编码", "公司编码不存在。", values.GetValueOrDefault("legal_entity_code")));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("employee_number")) && !await db.Employees.AnyAsync(item => item.EmployeeNumber == values.GetValueOrDefault("employee_number"), cancellationToken)) errors.Add(new ImportErrorDto(row, "员工编号", "员工编号不存在。", values.GetValueOrDefault("employee_number")));
            ValidateDecimal(values.GetValueOrDefault("amount"), row, "个人金额", errors);
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("employee_number")) && !TryParsePayrollRecipientType(values.GetValueOrDefault("recipient_type"), out _)) errors.Add(new ImportErrorDto(row, "人员来源", "人员来源必须是员工或班组工人。", values.GetValueOrDefault("recipient_type")));
        }

        if (dataset is ExportDataset.Collections or ExportDataset.Payments)
        {
            var company = values.GetValueOrDefault("legal_entity_code");
            var companyName = values.GetValueOrDefault("legal_entity");
            var partner = values.GetValueOrDefault("partner_number");
            var partnerName = values.GetValueOrDefault("partner");
            var account = values.GetValueOrDefault("account_number");
            var accountName = values.GetValueOrDefault("account");
            if (string.IsNullOrWhiteSpace(company) && string.IsNullOrWhiteSpace(companyName)) errors.Add(new ImportErrorDto(row, "签约公司", "必须填写签约公司编码或名称。", null));
            if (string.IsNullOrWhiteSpace(partner) && string.IsNullOrWhiteSpace(partnerName)) errors.Add(new ImportErrorDto(row, "合作单位", "必须填写合作单位编号或名称。", null));
            if (string.IsNullOrWhiteSpace(account) && string.IsNullOrWhiteSpace(accountName)) errors.Add(new ImportErrorDto(row, "账户", "必须填写账户账号或名称。", null));
            if (!string.IsNullOrWhiteSpace(company) && !await db.LegalEntities.AnyAsync(item => item.Code == company, cancellationToken) && string.IsNullOrWhiteSpace(companyName)) errors.Add(new ImportErrorDto(row, "签约公司编码", "签约公司编码不存在。", company));
            if (!string.IsNullOrWhiteSpace(partner) && !await db.BusinessPartners.AnyAsync(item => item.PartnerNumber == partner, cancellationToken) && string.IsNullOrWhiteSpace(partnerName)) errors.Add(new ImportErrorDto(row, "合作单位编号", "合作单位编号不存在。", partner));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("project_number")) && !await db.Projects.AnyAsync(item => item.ProjectNumber == values.GetValueOrDefault("project_number"), cancellationToken)) errors.Add(new ImportErrorDto(row, "项目编号", "项目编号不存在。", values.GetValueOrDefault("project_number")));
            if (!string.IsNullOrWhiteSpace(account) && !await db.FinancialAccounts.AnyAsync(item => item.AccountNumber == account, cancellationToken)) errors.Add(new ImportErrorDto(row, "账户账号", "账户账号不存在。", account));
            ValidateDate(values.GetValueOrDefault(dataset == ExportDataset.Collections ? "collection_date" : "payment_date"), row, dataset == ExportDataset.Collections ? "收款日期" : "付款日期", errors);
            ValidateDecimal(values.GetValueOrDefault("amount"), row, dataset == ExportDataset.Collections ? "收款金额" : "付款金额", errors);
            if (!TryParsePaymentMethod(values.GetValueOrDefault("payment_method"), out _)) errors.Add(new ImportErrorDto(row, "付款方式", "付款方式无法识别。", values.GetValueOrDefault("payment_method")));
        }

        if (dataset == ExportDataset.Invoices)
        {
            var company = values.GetValueOrDefault("legal_entity_code");
            var companyName = values.GetValueOrDefault("legal_entity");
            var partner = values.GetValueOrDefault("partner_number");
            var partnerName = values.GetValueOrDefault("partner");
            if (string.IsNullOrWhiteSpace(company) && string.IsNullOrWhiteSpace(companyName)) errors.Add(new ImportErrorDto(row, "签约公司", "必须填写签约公司编码或名称。", null));
            if (!string.IsNullOrWhiteSpace(company) && !await db.LegalEntities.AnyAsync(item => item.Code == company, cancellationToken) && string.IsNullOrWhiteSpace(companyName)) errors.Add(new ImportErrorDto(row, "签约公司编码", "签约公司编码不存在。", company));
            if (string.IsNullOrWhiteSpace(partner) && string.IsNullOrWhiteSpace(partnerName)) errors.Add(new ImportErrorDto(row, "合作单位", "必须填写合作单位编号或名称。", null));
            if (!string.IsNullOrWhiteSpace(partner) && !await db.BusinessPartners.AnyAsync(item => item.PartnerNumber == partner, cancellationToken) && string.IsNullOrWhiteSpace(partnerName)) errors.Add(new ImportErrorDto(row, "合作单位编号", "合作单位编号不存在。", partner));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("project_number")) && !await db.Projects.AnyAsync(item => item.ProjectNumber == values.GetValueOrDefault("project_number"), cancellationToken)) errors.Add(new ImportErrorDto(row, "项目编号", "项目编号不存在。", values.GetValueOrDefault("project_number")));
            ValidateDate(values.GetValueOrDefault("invoice_date"), row, "发票日期", errors);
            ValidateDecimal(values.GetValueOrDefault("gross_amount"), row, "含税金额", errors);
            if (!TryParseLedgerDirection(values.GetValueOrDefault("direction"), out _)) errors.Add(new ImportErrorDto(row, "发票方向", "发票方向必须是应收或应付。", values.GetValueOrDefault("direction")));
            if (!TryParseLedgerStatus(values.GetValueOrDefault("status"), out _)) errors.Add(new ImportErrorDto(row, "状态", "发票状态必须是有效或已作废。", values.GetValueOrDefault("status")));
        }

        if (dataset == ExportDataset.EmployeeWages)
        {
            await ValidateEmployeeReferenceAsync(values, row, errors, cancellationToken);
            ValidateDate(values.GetValueOrDefault("start_date"), row, "开始日期", errors);
            ValidateDate(values.GetValueOrDefault("end_date"), row, "结束日期", errors);
            ValidateDecimal(values.GetValueOrDefault("quantity"), row, "数量", errors);
            ValidateDecimal(values.GetValueOrDefault("unit_price"), row, "单价", errors);
            ValidateDecimal(values.GetValueOrDefault("automatic_amount"), row, "自动金额", errors);
            ValidateDecimal(values.GetValueOrDefault("adjustment_amount"), row, "调整金额", errors);
            ValidateDecimal(values.GetValueOrDefault("final_amount"), row, "最终金额", errors);
            if (!TryParseEmployeeWageEntryType(values.GetValueOrDefault("entry_type"), out _)) errors.Add(new ImportErrorDto(row, "工资明细类型", "工资明细类型无法识别。", values.GetValueOrDefault("entry_type")));
            if (!TryParseEmployeeWageCategory(values.GetValueOrDefault("wage_category"), out _)) errors.Add(new ImportErrorDto(row, "工资类别", "工资类别无法识别。", values.GetValueOrDefault("wage_category")));
            if (!TryParseEmployeeWageCalculationMethod(values.GetValueOrDefault("calculation_method"), out _)) errors.Add(new ImportErrorDto(row, "计薪方式", "计薪方式无法识别。", values.GetValueOrDefault("calculation_method")));
            if (!TryParsePayrollItemNature(values.GetValueOrDefault("nature"), out _)) errors.Add(new ImportErrorDto(row, "收支性质", "收支性质必须是收入或扣款。", values.GetValueOrDefault("nature")));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("legal_entity_code")) && !await db.LegalEntities.AnyAsync(item => item.Code == values.GetValueOrDefault("legal_entity_code"), cancellationToken)) errors.Add(new ImportErrorDto(row, "公司编码", "公司编码不存在。", values.GetValueOrDefault("legal_entity_code")));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("project_number")) && !await db.Projects.AnyAsync(item => item.ProjectNumber == values.GetValueOrDefault("project_number"), cancellationToken)) errors.Add(new ImportErrorDto(row, "项目编号", "项目编号不存在。", values.GetValueOrDefault("project_number")));
        }

        if (dataset == ExportDataset.EmployeeOtherPayments)
        {
            await ValidateEmployeeReferenceAsync(values, row, errors, cancellationToken);
            var company = values.GetValueOrDefault("legal_entity_code");
            var companyName = values.GetValueOrDefault("legal_entity");
            if (string.IsNullOrWhiteSpace(company) && string.IsNullOrWhiteSpace(companyName)) errors.Add(new ImportErrorDto(row, "公司", "必须填写公司编码或名称。", null));
            if (!string.IsNullOrWhiteSpace(company) && !await db.LegalEntities.AnyAsync(item => item.Code == company, cancellationToken) && string.IsNullOrWhiteSpace(companyName)) errors.Add(new ImportErrorDto(row, "公司编码", "公司编码不存在。", company));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("project_number")) && !await db.Projects.AnyAsync(item => item.ProjectNumber == values.GetValueOrDefault("project_number"), cancellationToken)) errors.Add(new ImportErrorDto(row, "项目编号", "项目编号不存在。", values.GetValueOrDefault("project_number")));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("account_number")) && !await db.FinancialAccounts.AnyAsync(item => item.AccountNumber == values.GetValueOrDefault("account_number"), cancellationToken)) errors.Add(new ImportErrorDto(row, "账户账号", "账户账号不存在。", values.GetValueOrDefault("account_number")));
            ValidateDate(values.GetValueOrDefault("entry_date"), row, "日期", errors);
            ValidateDecimal(values.GetValueOrDefault("amount"), row, "金额", errors);
            if (!TryParseEmployeeLedgerEntryType(values.GetValueOrDefault("entry_type"), out _)) errors.Add(new ImportErrorDto(row, "往来类型", "往来类型无法识别。", values.GetValueOrDefault("entry_type")));
            if (!TryParseEmployeeLedgerRecordKind(values.GetValueOrDefault("record_kind"), out _)) errors.Add(new ImportErrorDto(row, "记录性质", "记录性质无法识别。", values.GetValueOrDefault("record_kind")));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("payment_method")) && !TryParsePaymentMethod(values.GetValueOrDefault("payment_method"), out _)) errors.Add(new ImportErrorDto(row, "付款方式", "付款方式无法识别。", values.GetValueOrDefault("payment_method")));
        }

        if (dataset == ExportDataset.EmployeeReceipts)
        {
            await ValidateEmployeeReferenceAsync(values, row, errors, cancellationToken);
            var company = values.GetValueOrDefault("payment_legal_entity_code");
            var companyName = values.GetValueOrDefault("payment_legal_entity");
            if (string.IsNullOrWhiteSpace(company) && string.IsNullOrWhiteSpace(companyName)) errors.Add(new ImportErrorDto(row, "付款公司", "必须填写付款公司编码或名称。", null));
            if (!string.IsNullOrWhiteSpace(company) && !await db.LegalEntities.AnyAsync(item => item.Code == company, cancellationToken) && string.IsNullOrWhiteSpace(companyName)) errors.Add(new ImportErrorDto(row, "付款公司编码", "付款公司编码不存在。", company));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("account_number")) && !await db.FinancialAccounts.AnyAsync(item => item.AccountNumber == values.GetValueOrDefault("account_number"), cancellationToken)) errors.Add(new ImportErrorDto(row, "账户账号", "账户账号不存在。", values.GetValueOrDefault("account_number")));
            if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("project_number")) && !await db.Projects.AnyAsync(item => item.ProjectNumber == values.GetValueOrDefault("project_number"), cancellationToken)) errors.Add(new ImportErrorDto(row, "项目编号", "项目编号不存在。", values.GetValueOrDefault("project_number")));
            ValidateDate(values.GetValueOrDefault("receipt_date"), row, "收款日期", errors);
            ValidateDecimal(values.GetValueOrDefault("amount"), row, "金额", errors);
            if (!TryParseEmployeeReceiptType(values.GetValueOrDefault("receipt_type"), out _)) errors.Add(new ImportErrorDto(row, "收款类型", "收款类型无法识别。", values.GetValueOrDefault("receipt_type")));
            if (!TryParsePaymentMethod(values.GetValueOrDefault("payment_method"), out _)) errors.Add(new ImportErrorDto(row, "付款方式", "付款方式无法识别。", values.GetValueOrDefault("payment_method")));
        }

        if (dataset == ExportDataset.EmployeeFinancialAdjustments)
        {
            await ValidateEmployeeReferenceAsync(values, row, errors, cancellationToken);
            ValidateDate(values.GetValueOrDefault("adjustment_date"), row, "调整日期", errors);
            ValidateDecimal(values.GetValueOrDefault("amount"), row, "调整金额", errors);
            if (!TryParseEmployeeFinancialAdjustmentType(values.GetValueOrDefault("adjustment_type"), out _)) errors.Add(new ImportErrorDto(row, "调整类型", "调整类型无法识别。", values.GetValueOrDefault("adjustment_type")));
        }
    }

    private async Task ValidateEmployeeReferenceAsync(IReadOnlyDictionary<string, string?> values, int row, List<ImportErrorDto> errors, CancellationToken cancellationToken)
    {
        var employeeNumber = values.GetValueOrDefault("employee_number");
        if (!await db.Employees.AnyAsync(item => item.EmployeeNumber == employeeNumber, cancellationToken)) errors.Add(new ImportErrorDto(row, "员工编号", "员工编号不存在。", employeeNumber));
    }

    private static Dictionary<string, string> ResolveMapping(ExportDataset dataset, IReadOnlyList<string> headers, IReadOnlyDictionary<string, string>? provided)
    {
        var mapping = provided is null
            ? GetColumns(dataset).Where(column => headers.Contains(column.Header, StringComparer.Ordinal)).ToDictionary(column => column.Header, column => column.Key, StringComparer.Ordinal)
            : new Dictionary<string, string>(provided, StringComparer.Ordinal);
        var validKeys = GetColumns(dataset).Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        if (mapping.Any(item => !headers.Contains(item.Key, StringComparer.Ordinal) || !validKeys.Contains(item.Value)))
        {
            throw new ArgumentException("字段映射包含不存在的源列或目标字段。", nameof(provided));
        }

        return mapping;
    }

    private static Dictionary<string, string?> RowValues(IReadOnlyList<string> headers, IReadOnlyList<object?> row, IReadOnlyDictionary<string, string> mapping)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var index = 0; index < headers.Count; index++)
        {
            if (mapping.TryGetValue(headers[index], out var target))
            {
                result[target] = index < row.Count ? Convert.ToString(row[index], System.Globalization.CultureInfo.InvariantCulture)?.Trim() : null;
            }
        }

        return result;
    }

    private void AddOrUpdateEntity(ExportDataset dataset, Dictionary<string, string?> values, ImportMode mode)
    {
        if (mode != ImportMode.New && TryUpdateEntity(dataset, values)) return;
        switch (dataset)
        {
            case ExportDataset.Employees:
                if (!TryParseEmployeeType(values["employee_type"]!, out var employeeType))
                {
                    throw new InvalidOperationException("已通过预览的员工类型无法解析。");
                }
                db.Employees.Add(new Employee
                {
                    EmployeeNumber = values["employee_number"]!,
                    Name = values["name"]!,
                    EmployeeType = employeeType,
                    PositionTitle = values.GetValueOrDefault("position"),
                    Phone = values.GetValueOrDefault("phone"),
                    IdentityNumber = values.GetValueOrDefault("identity_number"),
                    BankAccountNumber = values.GetValueOrDefault("bank_account_number"),
                    BankName = values.GetValueOrDefault("bank_name"),
                    DefaultMonthlySalary = ParseDecimal(values.GetValueOrDefault("default_monthly_salary")),
                    DefaultDailyRate = ParseDecimal(values.GetValueOrDefault("default_daily_rate")),
                    DefaultHourlyRate = ParseDecimal(values.GetValueOrDefault("default_hourly_rate")),
                    DefaultPieceworkRate = ParseDecimal(values.GetValueOrDefault("default_piecework_rate"))
                });
                break;
            case ExportDataset.Payroll:
                AddPayrollBatch(values);
                break;
            case ExportDataset.EmployeeWages:
                AddEmployeeWage(values);
                break;
            case ExportDataset.EmployeeOtherPayments:
                AddEmployeeOtherPayment(values);
                break;
            case ExportDataset.EmployeeReceipts:
                AddEmployeeReceipt(values);
                break;
            case ExportDataset.EmployeeFinancialAdjustments:
                AddEmployeeFinancialAdjustment(values);
                break;
            case ExportDataset.EmployeeCertificates:
                var certificateEmployee = db.Employees.Single(item => item.EmployeeNumber == values["employee_number"]);
                db.EmployeeCertificates.Add(new EmployeeCertificate
                {
                    Employee = certificateEmployee,
                    CertificateType = values["certificate_type"]!,
                    CertificateNumber = values.GetValueOrDefault("certificate_number"),
                    SpecialtyLevelScope = values.GetValueOrDefault("specialty_level_scope"),
                    IssuingAuthority = values.GetValueOrDefault("issuing_authority"),
                    IssuedOn = ParseDate(values.GetValueOrDefault("issued_on")),
                    ExpiresOn = ParseDate(values.GetValueOrDefault("expires_on")),
                    Notes = values.GetValueOrDefault("notes")
                });
                break;
            case ExportDataset.Partners:
                db.BusinessPartners.Add(new BusinessPartner
                {
                    PartnerNumber = values["partner_number"]!,
                    Name = values["name"]!,
                    ShortName = values["short_name"]!
                });
                break;
            case ExportDataset.Projects:
                var stage = Enum.TryParse<ProjectStage>(values.GetValueOrDefault("stage"), ignoreCase: true, out var parsedStage) ? parsedStage : ProjectStage.AwaitingMobilization;
                db.Projects.Add(new Project
                {
                    ProjectNumber = values["project_number"]!,
                    Name = values["name"]!,
                    Stage = stage,
                    GeneralContractorName = values.GetValueOrDefault("general_contractor")
                });
                break;
            case ExportDataset.Contracts:
                var contractProject = db.Projects.Single(item => item.ProjectNumber == values["project_number"]);
                db.Contracts.Add(new Contract { Project = contractProject, ContractNumber = values["contract_number"]!, Name = values["name"]!, ContractType = Enum.Parse<ContractType>(values["contract_type"]!, true), CounterpartyName = values.GetValueOrDefault("counterparty_name"), SignedDate = ParseDate(values.GetValueOrDefault("signed_date")), TotalAmount = ParseDecimal(values.GetValueOrDefault("total_amount")) ?? 0m, Notes = values.GetValueOrDefault("notes") });
                break;
            case ExportDataset.StageResults:
                var resultProject = db.Projects.Single(item => item.ProjectNumber == values["project_number"]);
                db.StageResults.Add(new StageResult { Project = resultProject, Title = values["title"]!, ResultType = Enum.Parse<EngineeringManager.Domain.StageResults.StageResultType>(values["result_type"]!, true), Status = Enum.TryParse<EngineeringManager.Domain.StageResults.StageResultStatus>(values.GetValueOrDefault("status"), true, out var resultStatus) ? resultStatus : EngineeringManager.Domain.StageResults.StageResultStatus.Draft, ResultDate = ParseDate(values.GetValueOrDefault("result_date")) ?? DateOnly.FromDateTime(DateTime.Today), QualityResult = Enum.TryParse<EngineeringManager.Domain.StageResults.QualityResult>(values.GetValueOrDefault("quality_result"), true, out var quality) ? quality : EngineeringManager.Domain.StageResults.QualityResult.NotChecked, Description = values.GetValueOrDefault("description") });
                break;
            case ExportDataset.Companies:
                var category = db.CompanyCategories.Single(item => item.Code == values["category_code"]);
                db.LegalEntities.Add(new EngineeringManager.Domain.Organization.LegalEntity
                {
                    Code = values["company_code"]!,
                    Name = values["name"]!,
                    ShortName = values["short_name"]!,
                    CompanyCategory = category,
                    LegalRepresentative = values.GetValueOrDefault("legal_representative"),
                    UnifiedSocialCreditCode = values.GetValueOrDefault("tax_code"),
                    RegisteredAddress = values.GetValueOrDefault("registered_address"),
                    BusinessAddress = values.GetValueOrDefault("business_address"),
                    Phone = values.GetValueOrDefault("phone"),
                    InvoiceTitle = values["name"]
                });
                break;
            case ExportDataset.CompanyAccounts:
                if (!TryParseAccountType(values["account_type"], out var accountType)) throw new InvalidOperationException("已通过预览的账户类型无法解析。");
                var accountCompany = db.LegalEntities.Single(item => item.Code == values["company_code"]);
                db.FinancialAccounts.Add(new FinancialAccount
                {
                    LegalEntity = accountCompany,
                    AccountName = values["account_name"]!,
                    AccountType = accountType,
                    AccountNumber = values.GetValueOrDefault("account_number"),
                    BankName = values.GetValueOrDefault("bank_name"),
                    OpeningBalance = decimal.TryParse(values.GetValueOrDefault("opening_balance"), out var opening) ? opening : 0m,
                    IsDefaultCollection = ParseBoolean(values.GetValueOrDefault("default_collection")),
                    IsDefaultPayment = ParseBoolean(values.GetValueOrDefault("default_payment")),
                    IsDefaultInvoice = ParseBoolean(values.GetValueOrDefault("default_invoice"))
                });
                break;
            case ExportDataset.CompanyCertificates:
                var certificateCompany = db.LegalEntities.Single(item => item.Code == values["company_code"]);
                db.CompanyCertificates.Add(new CompanyCertificate
                {
                    LegalEntity = certificateCompany,
                    CertificateType = values["certificate_type"]!,
                    CertificateNumber = values.GetValueOrDefault("certificate_number"),
                    SpecialtyLevelScope = values.GetValueOrDefault("specialty_level_scope"),
                    IssuingAuthority = values.GetValueOrDefault("issuing_authority"),
                    IssuedOn = ParseDate(values.GetValueOrDefault("issued_on")),
                    ExpiresOn = ParseDate(values.GetValueOrDefault("expires_on")),
                    Notes = values.GetValueOrDefault("notes")
                });
                break;
            case ExportDataset.Equipment:
                if (!TryParseOwnership(values["ownership"], out var ownership)) throw new InvalidOperationException("已通过预览的设备权属无法解析。");
                db.Equipment.Add(new EngineeringManager.Infrastructure.Data.Equipment
                {
                    EquipmentNumber = values["equipment_number"]!, Name = values["name"]!, Model = values.GetValueOrDefault("model"), Category = values.GetValueOrDefault("category"), OwnershipType = ownership,
                    OwnerLegalEntity = ownership == EquipmentOwnershipType.SelfOwned ? db.LegalEntities.Single(item => item.Code == values["owner_company_code"]) : null,
                    LessorBusinessPartner = ownership == EquipmentOwnershipType.Rented ? db.BusinessPartners.Single(item => item.PartnerNumber == values["lessor_number"]) : null,
                    InternalDailyRate = ParseDecimal(values.GetValueOrDefault("internal_daily_rate"))
                });
                break;
            case ExportDataset.EquipmentLeases:
                if (!TryParseRentMode(values["rent_mode"], out var leaseRentMode)) throw new InvalidOperationException("已通过预览的计租方式无法解析。");
                db.EquipmentLeaseAgreements.Add(new EquipmentLeaseAgreement { Equipment = db.Equipment.Single(item => item.EquipmentNumber == values["equipment_number"]), LessorBusinessPartner = db.BusinessPartners.Single(item => item.PartnerNumber == values["lessor_number"]), ContractNumber = values.GetValueOrDefault("contract_number"), StartDate = ParseDate(values["start_date"])!.Value, EndDate = ParseDate(values.GetValueOrDefault("end_date")), RentMode = leaseRentMode, UnitRate = ParseDecimal(values["unit_rate"]) ?? 0m });
                break;
            case ExportDataset.EquipmentUsages:
                if (!TryParseRentMode(values["rent_mode"], out var usageRentMode)) throw new InvalidOperationException("已通过预览的计租方式无法解析。");
                db.EquipmentProjectUsages.Add(new EquipmentProjectUsage { Equipment = db.Equipment.Single(item => item.EquipmentNumber == values["equipment_number"]), Project = db.Projects.Single(item => item.ProjectNumber == values["project_number"]), LegalEntity = db.LegalEntities.Single(item => item.Code == values["company_code"]), EntryDate = ParseDate(values["entry_date"])!.Value, ExitDate = ParseDate(values.GetValueOrDefault("exit_date")), RentMode = usageRentMode, UnitRate = ParseDecimal(values.GetValueOrDefault("unit_rate")) ?? 0m });
                break;
            case ExportDataset.EquipmentPeriods:
                if (!TryParsePeriodType(values["period_type"], out var periodType)) throw new InvalidOperationException("已通过预览的日期段类型无法解析。");
                var periodUsage = FindUsage(values);
                db.EquipmentWorkPeriods.Add(new EquipmentWorkPeriod { Usage = periodUsage, StartDate = ParseDate(values["start_date"])!.Value, EndDate = ParseDate(values["end_date"])!.Value, PeriodType = periodType, IsChargeable = ParseBoolean(values.GetValueOrDefault("chargeable")), Notes = values.GetValueOrDefault("notes") });
                break;
            case ExportDataset.EquipmentSettlements:
                var settlementUsage = FindUsage(values);
                db.EquipmentSettlements.Add(new EquipmentSettlement { Usage = settlementUsage, SettlementDate = ParseDate(values["settlement_date"])!.Value, BaseAmount = ParseDecimal(values["base_amount"]) ?? 0m, TotalAmount = ParseDecimal(values["total_amount"]) ?? 0m, OffsetAmount = ParseDecimal(values.GetValueOrDefault("offset_amount")) ?? 0m, ModificationReason = values["reason"]! });
                break;
            default:
                throw new NotSupportedException($"暂不支持导入数据集：{dataset}");
        }
    }

    private void AddPayrollBatch(Dictionary<string, string?> values)
    {
        if (!TryParsePayrollBatchType(values.GetValueOrDefault("batch_type"), out var batchType)) throw new InvalidOperationException("已通过预览的工资批次类型无法解析。");
        var legalEntity = ResolveLegalEntityOptional(values.GetValueOrDefault("legal_entity_code"), null);
        var project = ResolveProject(values.GetValueOrDefault("project_number"));
        var account = legalEntity is null ? null : ResolveAccount(legalEntity.Id, values.GetValueOrDefault("account_number"), null);
        var startDate = ParseDate(values.GetValueOrDefault("start_date"))!.Value;
        var endDate = ParseDate(values.GetValueOrDefault("end_date"))!.Value;
        var paymentDate = ParseDate(values.GetValueOrDefault("payment_date"));
        var paymentMethod = TryParsePaymentMethod(values.GetValueOrDefault("payment_method"), out var parsedPaymentMethod) ? parsedPaymentMethod : PaymentMethod.BankTransfer;
        var batchNumber = values["batch_number"]!;
        var batch = db.PayrollBatches.Local.FirstOrDefault(item => item.BatchNumber == batchNumber)
            ?? db.PayrollBatches.SingleOrDefault(item => item.BatchNumber == batchNumber);
        if (batch is null)
        {
            batch = new PayrollBatch
            {
                BatchNumber = batchNumber,
                Name = values["batch_name"]!,
                BatchType = batchType,
                StartDate = startDate,
                EndDate = endDate,
                PaymentDate = paymentDate,
                Project = project,
                LegalEntity = legalEntity,
                Account = account,
                ActualAmount = ParseDecimal(values.GetValueOrDefault("actual_amount")) ?? 0m,
                PaymentMethod = paymentMethod,
                Status = PayrollBatchStatus.Draft,
                Notes = values.GetValueOrDefault("notes")
            };
            db.PayrollBatches.Add(batch);
        }

        var employeeNumber = values.GetValueOrDefault("employee_number");
        if (string.IsNullOrWhiteSpace(employeeNumber)) return;
        if (!TryParsePayrollRecipientType(values.GetValueOrDefault("recipient_type"), out var recipientType) || recipientType != PayrollRecipientType.Employee) throw new InvalidOperationException("工资导入目前只支持员工收款行。");
        var employee = FindEmployee(employeeNumber);
        var recipientKey = $"employee:{employee.Id:N}";
        var payment = batch.Payments.FirstOrDefault(item => item.RecipientKey == recipientKey)
            ?? db.PayrollPayments.Local.FirstOrDefault(item => item.PayrollBatchId == batch.Id && item.RecipientKey == recipientKey)
            ?? (batch.Id == Guid.Empty ? null : db.PayrollPayments.SingleOrDefault(item => item.PayrollBatchId == batch.Id && item.RecipientKey == recipientKey));
        if (payment is null)
        {
            payment = new PayrollPayment
            {
                Batch = batch,
                RecipientType = PayrollRecipientType.Employee,
                PaymentCategory = PayrollPaymentCategory.Wage,
                RecipientKey = recipientKey,
                Employee = employee,
                Account = account,
                PaymentDate = paymentDate,
                Amount = ParseDecimal(values.GetValueOrDefault("amount")) ?? 0m,
                PaymentMethod = paymentMethod,
                PayeeType = PayrollPayeeType.Employee,
                PayeeName = values.GetValueOrDefault("recipient_name") ?? employee.Name,
                RecipientNameSnapshot = values.GetValueOrDefault("recipient_name") ?? employee.Name
            };
            batch.Payments.Add(payment);
        }
        else
        {
            payment.Batch = batch;
            payment.RecipientType = PayrollRecipientType.Employee;
            payment.PaymentCategory = PayrollPaymentCategory.Wage;
            payment.Employee = employee;
            payment.Account = account;
            payment.PaymentDate = paymentDate;
            payment.Amount = ParseDecimal(values.GetValueOrDefault("amount")) ?? payment.Amount;
            payment.PaymentMethod = paymentMethod;
            payment.PayeeType = PayrollPayeeType.Employee;
            payment.PayeeName = values.GetValueOrDefault("recipient_name") ?? employee.Name;
            payment.RecipientNameSnapshot = values.GetValueOrDefault("recipient_name") ?? employee.Name;
        }
    }

    private void AddEmployeeWage(Dictionary<string, string?> values)
    {
        var employee = FindEmployee(values["employee_number"]!);
        var startDate = ParseDate(values.GetValueOrDefault("start_date"))!.Value;
        var endDate = ParseDate(values.GetValueOrDefault("end_date"))!.Value;
        if (!TryParseEmployeeWageEntryType(values.GetValueOrDefault("entry_type"), out var entryType)) throw new InvalidOperationException("已通过预览的工资明细类型无法解析。");
        if (!TryParseEmployeeWageCategory(values.GetValueOrDefault("wage_category"), out var wageCategory)) throw new InvalidOperationException("已通过预览的工资类别无法解析。");
        if (!TryParseEmployeeWageCalculationMethod(values.GetValueOrDefault("calculation_method"), out var calculationMethod)) throw new InvalidOperationException("已通过预览的计薪方式无法解析。");
        if (!TryParsePayrollItemNature(values.GetValueOrDefault("nature"), out var nature)) throw new InvalidOperationException("已通过预览的收支性质无法解析。");
        var legalEntity = ResolveLegalEntityOptional(values.GetValueOrDefault("legal_entity_code"), null);
        var project = ResolveProject(values.GetValueOrDefault("project_number"));
        var automaticAmount = ParseDecimal(values.GetValueOrDefault("automatic_amount")) ?? 0m;
        var adjustmentAmount = ParseDecimal(values.GetValueOrDefault("adjustment_amount")) ?? 0m;
        var finalAmount = ParseDecimal(values.GetValueOrDefault("final_amount")) ?? automaticAmount + adjustmentAmount;
        db.EmployeeWageEntries.Add(new EmployeeWageEntry
        {
            Employee = employee,
            BusinessYear = ResolveBusinessYear(values.GetValueOrDefault("business_year"), startDate),
            StartDate = startDate,
            EndDate = endDate,
            EntryType = entryType,
            WageCategory = wageCategory,
            CalculationMethod = calculationMethod,
            Nature = nature,
            Quantity = ParseDecimal(values.GetValueOrDefault("quantity")),
            Unit = values.GetValueOrDefault("unit"),
            UnitPrice = ParseDecimal(values.GetValueOrDefault("unit_price")),
            AutomaticAmount = automaticAmount,
            AdjustmentAmount = adjustmentAmount,
            FinalAmount = finalAmount,
            LegalEntity = legalEntity,
            Project = project,
            Notes = values.GetValueOrDefault("notes")
        });
    }

    private void AddEmployeeOtherPayment(Dictionary<string, string?> values)
    {
        var employee = FindEmployee(values["employee_number"]!);
        var legalEntity = ResolveLegalEntity(values.GetValueOrDefault("legal_entity_code"), values.GetValueOrDefault("legal_entity"));
        if (!TryParseEmployeeLedgerEntryType(values.GetValueOrDefault("entry_type"), out var entryType)) throw new InvalidOperationException("已通过预览的往来类型无法解析。");
        if (!TryParseEmployeeLedgerRecordKind(values.GetValueOrDefault("record_kind"), out var recordKind)) throw new InvalidOperationException("已通过预览的记录性质无法解析。");
        var paymentMethod = string.IsNullOrWhiteSpace(values.GetValueOrDefault("payment_method")) ? (PaymentMethod?)null : TryParsePaymentMethod(values.GetValueOrDefault("payment_method"), out var parsed) ? parsed : null;
        db.EmployeeOtherPayments.Add(new EmployeeOtherPayment
        {
            Employee = employee,
            Project = ResolveProject(values.GetValueOrDefault("project_number")),
            LegalEntity = legalEntity,
            EntryType = entryType,
            RecordKind = recordKind,
            RelatedPayableId = Guid.TryParse(values.GetValueOrDefault("related_payable_id"), out var relatedId) ? relatedId : null,
            Account = ResolveAccount(legalEntity.Id, values.GetValueOrDefault("account_number"), values.GetValueOrDefault("account")),
            EntryDate = ParseDate(values.GetValueOrDefault("entry_date"))!.Value,
            Amount = ParseDecimal(values.GetValueOrDefault("amount")) ?? 0m,
            PaymentMethod = paymentMethod,
            Description = values.GetValueOrDefault("description")
        });
    }

    private void AddEmployeeReceipt(Dictionary<string, string?> values)
    {
        var employee = FindEmployee(values["employee_number"]!);
        var legalEntity = ResolveLegalEntity(values.GetValueOrDefault("payment_legal_entity_code"), values.GetValueOrDefault("payment_legal_entity"));
        var account = ResolveAccount(legalEntity.Id, values.GetValueOrDefault("account_number"), values.GetValueOrDefault("account"))
            ?? throw new InvalidOperationException("员工收款必须能匹配到账户。");
        if (!TryParseEmployeeReceiptType(values.GetValueOrDefault("receipt_type"), out var receiptType)) throw new InvalidOperationException("已通过预览的收款类型无法解析。");
        if (!TryParsePaymentMethod(values.GetValueOrDefault("payment_method"), out var paymentMethod)) throw new InvalidOperationException("已通过预览的付款方式无法解析。");
        var receiptDate = ParseDate(values.GetValueOrDefault("receipt_date"))!.Value;
        db.EmployeeReceipts.Add(new EmployeeReceipt
        {
            Employee = employee,
            BusinessYear = ResolveBusinessYear(values.GetValueOrDefault("business_year"), receiptDate),
            ReceiptDate = receiptDate,
            ReceiptType = receiptType,
            Amount = ParseDecimal(values.GetValueOrDefault("amount")) ?? 0m,
            PaymentLegalEntity = legalEntity,
            Account = account,
            PaymentMethod = paymentMethod,
            ActualRecipientName = values.GetValueOrDefault("actual_recipient_name") ?? employee.Name,
            Project = ResolveProject(values.GetValueOrDefault("project_number")),
            Notes = values.GetValueOrDefault("notes")
        });
    }

    private void AddEmployeeFinancialAdjustment(Dictionary<string, string?> values)
    {
        var employee = FindEmployee(values["employee_number"]!);
        if (!TryParseEmployeeFinancialAdjustmentType(values.GetValueOrDefault("adjustment_type"), out var adjustmentType)) throw new InvalidOperationException("已通过预览的调整类型无法解析。");
        var adjustmentDate = ParseDate(values.GetValueOrDefault("adjustment_date"))!.Value;
        db.EmployeeFinancialAdjustments.Add(new EmployeeFinancialAdjustment
        {
            Employee = employee,
            BusinessYear = ResolveBusinessYear(values.GetValueOrDefault("business_year"), adjustmentDate),
            AdjustmentDate = adjustmentDate,
            Amount = ParseDecimal(values.GetValueOrDefault("amount")) ?? 0m,
            AdjustmentType = adjustmentType,
            Notes = values.GetValueOrDefault("notes") ?? string.Empty
        });
    }

    private Employee FindEmployee(string employeeNumber) => db.Employees.Local.FirstOrDefault(item => item.EmployeeNumber == employeeNumber)
        ?? db.Employees.Single(item => item.EmployeeNumber == employeeNumber);

    private LegalEntity ResolveLegalEntity(string? code, string? name) => ResolveLegalEntityOptional(code, name)
        ?? throw new InvalidOperationException("无法匹配导入数据中的公司。");

    private LegalEntity? ResolveLegalEntityOptional(string? code, string? name)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        var normalizedName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        return db.LegalEntities.Local.FirstOrDefault(item =>
                   (normalizedCode != null && item.Code == normalizedCode) ||
                   (normalizedName != null && (item.Name == normalizedName || item.ShortName == normalizedName)))
               ?? db.LegalEntities.FirstOrDefault(item =>
                   (normalizedCode != null && item.Code == normalizedCode) ||
                   (normalizedName != null && (item.Name == normalizedName || item.ShortName == normalizedName)));
    }

    private BusinessPartner? ResolvePartner(string? number, string? name)
    {
        var normalizedNumber = string.IsNullOrWhiteSpace(number) ? null : number.Trim();
        var normalizedName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        return db.BusinessPartners.Local.FirstOrDefault(item =>
                   (normalizedNumber != null && item.PartnerNumber == normalizedNumber) ||
                   (normalizedName != null && (item.Name == normalizedName || item.ShortName == normalizedName)))
               ?? db.BusinessPartners.FirstOrDefault(item =>
                   (normalizedNumber != null && item.PartnerNumber == normalizedNumber) ||
                   (normalizedName != null && (item.Name == normalizedName || item.ShortName == normalizedName)));
    }

    private Project? ResolveProject(string? number)
    {
        var normalized = string.IsNullOrWhiteSpace(number) ? null : number.Trim();
        return normalized is null ? null : db.Projects.Local.FirstOrDefault(item => item.ProjectNumber == normalized)
            ?? db.Projects.FirstOrDefault(item => item.ProjectNumber == normalized);
    }

    private FinancialAccount? ResolveAccount(Guid legalEntityId, string? accountNumber, string? accountName)
    {
        var normalizedNumber = string.IsNullOrWhiteSpace(accountNumber) ? null : accountNumber.Trim();
        var normalizedName = string.IsNullOrWhiteSpace(accountName) ? null : accountName.Trim();
        return db.FinancialAccounts.Local.FirstOrDefault(item => item.LegalEntityId == legalEntityId &&
                   ((normalizedNumber != null && item.AccountNumber == normalizedNumber) || (normalizedName != null && item.AccountName == normalizedName)))
               ?? db.FinancialAccounts.FirstOrDefault(item => item.LegalEntityId == legalEntityId &&
                   ((normalizedNumber != null && item.AccountNumber == normalizedNumber) || (normalizedName != null && item.AccountName == normalizedName)));
    }

    private BusinessYear ResolveBusinessYear(string? name, DateOnly referenceDate)
    {
        var digits = new string((name ?? string.Empty).Where(char.IsDigit).ToArray());
        var yearNumber = int.TryParse(digits, out var parsedYear) && parsedYear >= 1900 && parsedYear <= 2200 ? parsedYear : referenceDate.Year;
        var normalizedName = string.IsNullOrWhiteSpace(name) ? $"{yearNumber}年度" : name.Trim();
        return db.BusinessYears.Local.FirstOrDefault(item => item.Name == normalizedName)
            ?? db.BusinessYears.SingleOrDefault(item => item.Name == normalizedName)
            ?? AddBusinessYear(normalizedName, yearNumber);
    }

    private BusinessYear AddBusinessYear(string name, int year)
    {
        var businessYear = new BusinessYear { Name = name, StartDate = new DateOnly(year, 1, 1), EndDate = new DateOnly(year, 12, 31) };
        db.BusinessYears.Add(businessYear);
        return businessYear;
    }

    public async Task<ImportMappingTemplateDto> SaveMappingTemplateAsync(SaveImportMappingTemplateRequest request, CancellationToken cancellationToken)
    {
        var owner = NormalizeRequired(request.OwnerUserId, nameof(request.OwnerUserId));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        if (request.Scope == ExportTemplateScope.Shared && !request.CanPublishShared) throw new UnauthorizedAccessException("当前用户无权发布共享导入映射。");
        if (await db.ImportMappingTemplates.AnyAsync(item => item.OwnerUserId == owner && item.Dataset == request.Dataset && item.Name == name, cancellationToken)) throw new InvalidOperationException($"导入映射模板名称已存在：{name}");
        var template = new ImportMappingTemplate { OwnerUserId = owner, Name = name, Dataset = request.Dataset, Scope = request.Scope, DatasetVersion = NormalizeRequired(request.DatasetVersion, nameof(request.DatasetVersion)), MappingJson = JsonSerializer.Serialize(request.Mapping) };
        db.ImportMappingTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return ToMappingDto(template);
    }

    public async Task<IReadOnlyList<ImportMappingTemplateDto>> ListMappingTemplatesAsync(string userId, ExportDataset dataset, CancellationToken cancellationToken)
    {
        var owner = NormalizeRequired(userId, nameof(userId));
        var templates = await db.ImportMappingTemplates.AsNoTracking().Where(item => item.Dataset == dataset && (item.OwnerUserId == owner || item.Scope == ExportTemplateScope.Shared)).ToListAsync(cancellationToken);
        return templates.OrderBy(item => item.Name).Select(ToMappingDto).ToArray();
    }

    private static ImportMappingTemplateDto ToMappingDto(ImportMappingTemplate template) => new(template.Id, template.OwnerUserId, template.Name, template.Dataset, template.Scope, template.DatasetVersion, JsonSerializer.Deserialize<Dictionary<string, string>>(template.MappingJson) ?? []);

    private bool TryUpdateEntity(ExportDataset dataset, Dictionary<string, string?> values)
    {
        var systemId = Guid.TryParse(values.GetValueOrDefault("_system_id"), out var parsedId) ? parsedId : (Guid?)null;
        switch (dataset)
        {
            case ExportDataset.Employees:
                var employee = systemId.HasValue ? db.Employees.SingleOrDefault(item => item.Id == systemId.Value) : db.Employees.SingleOrDefault(item => item.EmployeeNumber == values.GetValueOrDefault("employee_number"));
                if (employee is null) return false;
                EnsureConcurrency(employee.ConcurrencyStamp, values.GetValueOrDefault("_concurrency_stamp"), "员工");
                employee.Name = values.GetValueOrDefault("name") ?? employee.Name;
                if (TryParseEmployeeType(values.GetValueOrDefault("employee_type") ?? string.Empty, out var type)) employee.EmployeeType = type;
                employee.PositionTitle = values.GetValueOrDefault("position") ?? employee.PositionTitle;
                employee.Phone = values.GetValueOrDefault("phone") ?? employee.Phone;
                employee.IdentityNumber = values.GetValueOrDefault("identity_number") ?? employee.IdentityNumber;
                employee.BankAccountNumber = values.GetValueOrDefault("bank_account_number") ?? employee.BankAccountNumber;
                employee.BankName = values.GetValueOrDefault("bank_name") ?? employee.BankName;
                employee.DefaultMonthlySalary = ParseDecimal(values.GetValueOrDefault("default_monthly_salary"), employee.DefaultMonthlySalary ?? 0m);
                employee.DefaultDailyRate = ParseDecimal(values.GetValueOrDefault("default_daily_rate"), employee.DefaultDailyRate ?? 0m);
                employee.DefaultHourlyRate = ParseDecimal(values.GetValueOrDefault("default_hourly_rate"), employee.DefaultHourlyRate ?? 0m);
                employee.DefaultPieceworkRate = ParseDecimal(values.GetValueOrDefault("default_piecework_rate"), employee.DefaultPieceworkRate ?? 0m);
                employee.ConcurrencyStamp = Guid.NewGuid();
                return true;
            case ExportDataset.Partners:
                var partner = systemId.HasValue ? db.BusinessPartners.SingleOrDefault(item => item.Id == systemId.Value) : db.BusinessPartners.SingleOrDefault(item => item.PartnerNumber == values.GetValueOrDefault("partner_number"));
                if (partner is null) return false;
                partner.Name = values.GetValueOrDefault("name") ?? partner.Name;
                partner.ShortName = values.GetValueOrDefault("short_name") ?? partner.ShortName;
                partner.ConcurrencyStamp = Guid.NewGuid();
                return true;
            case ExportDataset.Projects:
                var project = systemId.HasValue ? db.Projects.SingleOrDefault(item => item.Id == systemId.Value) : db.Projects.SingleOrDefault(item => item.ProjectNumber == values.GetValueOrDefault("project_number"));
                if (project is null) return false;
                project.Name = values.GetValueOrDefault("name") ?? project.Name;
                if (Enum.TryParse<ProjectStage>(values.GetValueOrDefault("stage"), true, out var stage)) project.Stage = stage;
                project.GeneralContractorName = values.GetValueOrDefault("general_contractor") ?? project.GeneralContractorName;
                project.ConcurrencyStamp = Guid.NewGuid();
                return true;
            case ExportDataset.Companies:
                var company = systemId.HasValue ? db.LegalEntities.SingleOrDefault(item => item.Id == systemId.Value) : db.LegalEntities.SingleOrDefault(item => item.Code == values.GetValueOrDefault("company_code"));
                if (company is null) return false;
                company.Name = values.GetValueOrDefault("name") ?? company.Name;
                company.ShortName = values.GetValueOrDefault("short_name") ?? company.ShortName;
                company.Phone = values.GetValueOrDefault("phone") ?? company.Phone;
                company.UnifiedSocialCreditCode = values.GetValueOrDefault("tax_code") ?? company.UnifiedSocialCreditCode;
                company.ConcurrencyStamp = Guid.NewGuid();
                return true;
            case ExportDataset.Equipment:
                var equipment = systemId.HasValue ? db.Equipment.SingleOrDefault(item => item.Id == systemId.Value) : db.Equipment.SingleOrDefault(item => item.EquipmentNumber == values.GetValueOrDefault("equipment_number"));
                if (equipment is null) return false;
                equipment.Name = values.GetValueOrDefault("name") ?? equipment.Name;
                equipment.Model = values.GetValueOrDefault("model") ?? equipment.Model;
                equipment.Category = values.GetValueOrDefault("category") ?? equipment.Category;
                equipment.InternalDailyRate = ParseDecimal(values.GetValueOrDefault("internal_daily_rate"), equipment.InternalDailyRate ?? 0m);
                equipment.ConcurrencyStamp = Guid.NewGuid();
                return true;
            case ExportDataset.Payroll:
                var batch = db.PayrollBatches.SingleOrDefault(item => item.BatchNumber == values.GetValueOrDefault("batch_number"));
                if (batch is null) return false;
                batch.Name = values.GetValueOrDefault("batch_name") ?? batch.Name;
                if (TryParsePayrollBatchType(values.GetValueOrDefault("batch_type"), out var batchType)) batch.BatchType = batchType;
                batch.StartDate = ParseDate(values.GetValueOrDefault("start_date")) ?? batch.StartDate;
                batch.EndDate = ParseDate(values.GetValueOrDefault("end_date")) ?? batch.EndDate;
                batch.PaymentDate = ParseDate(values.GetValueOrDefault("payment_date"));
                batch.Project = ResolveProject(values.GetValueOrDefault("project_number"));
                batch.LegalEntity = ResolveLegalEntityOptional(values.GetValueOrDefault("legal_entity_code"), null);
                batch.ActualAmount = ParseDecimal(values.GetValueOrDefault("actual_amount"), batch.ActualAmount);
                if (TryParsePaymentMethod(values.GetValueOrDefault("payment_method"), out var batchPaymentMethod)) batch.PaymentMethod = batchPaymentMethod;
                batch.Notes = values.GetValueOrDefault("notes") ?? batch.Notes;
                AddPayrollBatch(values);
                return true;
            case ExportDataset.EmployeeWages:
                if (!systemId.HasValue) return false;
                var wage = db.EmployeeWageEntries.SingleOrDefault(item => item.Id == systemId.Value);
                if (wage is null) return false;
                UpdateEmployeeWage(wage, values);
                return true;
            case ExportDataset.EmployeeOtherPayments:
                if (!systemId.HasValue) return false;
                var otherPayment = db.EmployeeOtherPayments.SingleOrDefault(item => item.Id == systemId.Value);
                if (otherPayment is null) return false;
                UpdateEmployeeOtherPayment(otherPayment, values);
                return true;
            case ExportDataset.EmployeeReceipts:
                if (!systemId.HasValue) return false;
                var receipt = db.EmployeeReceipts.SingleOrDefault(item => item.Id == systemId.Value);
                if (receipt is null) return false;
                UpdateEmployeeReceipt(receipt, values);
                return true;
            case ExportDataset.EmployeeFinancialAdjustments:
                if (!systemId.HasValue) return false;
                var adjustment = db.EmployeeFinancialAdjustments.SingleOrDefault(item => item.Id == systemId.Value);
                if (adjustment is null) return false;
                UpdateEmployeeFinancialAdjustment(adjustment, values);
                return true;
            default:
                return false;
        }
    }

    private void UpdateEmployeeWage(EmployeeWageEntry entry, Dictionary<string, string?> values)
    {
        var employee = FindEmployee(values["employee_number"]!);
        var startDate = ParseDate(values.GetValueOrDefault("start_date"))!.Value;
        if (!TryParseEmployeeWageEntryType(values.GetValueOrDefault("entry_type"), out var entryType)) throw new InvalidOperationException("工资明细类型无法解析。");
        if (!TryParseEmployeeWageCategory(values.GetValueOrDefault("wage_category"), out var wageCategory)) throw new InvalidOperationException("工资类别无法解析。");
        if (!TryParseEmployeeWageCalculationMethod(values.GetValueOrDefault("calculation_method"), out var calculationMethod)) throw new InvalidOperationException("计薪方式无法解析。");
        if (!TryParsePayrollItemNature(values.GetValueOrDefault("nature"), out var nature)) throw new InvalidOperationException("收支性质无法解析。");
        entry.Employee = employee;
        entry.BusinessYear = ResolveBusinessYear(values.GetValueOrDefault("business_year"), startDate);
        entry.StartDate = startDate;
        entry.EndDate = ParseDate(values.GetValueOrDefault("end_date"))!.Value;
        entry.EntryType = entryType;
        entry.WageCategory = wageCategory;
        entry.CalculationMethod = calculationMethod;
        entry.Nature = nature;
        entry.Quantity = ParseDecimal(values.GetValueOrDefault("quantity"));
        entry.Unit = values.GetValueOrDefault("unit");
        entry.UnitPrice = ParseDecimal(values.GetValueOrDefault("unit_price"));
        entry.AutomaticAmount = ParseDecimal(values.GetValueOrDefault("automatic_amount")) ?? 0m;
        entry.AdjustmentAmount = ParseDecimal(values.GetValueOrDefault("adjustment_amount")) ?? 0m;
        entry.FinalAmount = ParseDecimal(values.GetValueOrDefault("final_amount")) ?? entry.AutomaticAmount + entry.AdjustmentAmount;
        entry.LegalEntity = ResolveLegalEntityOptional(values.GetValueOrDefault("legal_entity_code"), null);
        entry.Project = ResolveProject(values.GetValueOrDefault("project_number"));
        entry.Notes = values.GetValueOrDefault("notes");
    }

    private void UpdateEmployeeOtherPayment(EmployeeOtherPayment entry, Dictionary<string, string?> values)
    {
        var employee = FindEmployee(values["employee_number"]!);
        var legalEntity = ResolveLegalEntity(values.GetValueOrDefault("legal_entity_code"), values.GetValueOrDefault("legal_entity"));
        if (!TryParseEmployeeLedgerEntryType(values.GetValueOrDefault("entry_type"), out var entryType)) throw new InvalidOperationException("往来类型无法解析。");
        if (!TryParseEmployeeLedgerRecordKind(values.GetValueOrDefault("record_kind"), out var recordKind)) throw new InvalidOperationException("记录性质无法解析。");
        entry.Employee = employee;
        entry.Project = ResolveProject(values.GetValueOrDefault("project_number"));
        entry.LegalEntity = legalEntity;
        entry.EntryType = entryType;
        entry.RecordKind = recordKind;
        entry.RelatedPayableId = Guid.TryParse(values.GetValueOrDefault("related_payable_id"), out var relatedId) ? relatedId : null;
        entry.Account = ResolveAccount(legalEntity.Id, values.GetValueOrDefault("account_number"), values.GetValueOrDefault("account"));
        entry.EntryDate = ParseDate(values.GetValueOrDefault("entry_date"))!.Value;
        entry.Amount = ParseDecimal(values.GetValueOrDefault("amount")) ?? entry.Amount;
        entry.PaymentMethod = string.IsNullOrWhiteSpace(values.GetValueOrDefault("payment_method")) ? null : TryParsePaymentMethod(values.GetValueOrDefault("payment_method"), out var paymentMethod) ? paymentMethod : null;
        entry.Description = values.GetValueOrDefault("description");
    }

    private void UpdateEmployeeReceipt(EmployeeReceipt entry, Dictionary<string, string?> values)
    {
        var employee = FindEmployee(values["employee_number"]!);
        var legalEntity = ResolveLegalEntity(values.GetValueOrDefault("payment_legal_entity_code"), values.GetValueOrDefault("payment_legal_entity"));
        var account = ResolveAccount(legalEntity.Id, values.GetValueOrDefault("account_number"), values.GetValueOrDefault("account"))
            ?? throw new InvalidOperationException("员工收款无法匹配到账户。");
        if (!TryParseEmployeeReceiptType(values.GetValueOrDefault("receipt_type"), out var receiptType)) throw new InvalidOperationException("收款类型无法解析。");
        if (!TryParsePaymentMethod(values.GetValueOrDefault("payment_method"), out var paymentMethod)) throw new InvalidOperationException("付款方式无法解析。");
        var receiptDate = ParseDate(values.GetValueOrDefault("receipt_date"))!.Value;
        entry.Employee = employee;
        entry.BusinessYear = ResolveBusinessYear(values.GetValueOrDefault("business_year"), receiptDate);
        entry.ReceiptDate = receiptDate;
        entry.ReceiptType = receiptType;
        entry.Amount = ParseDecimal(values.GetValueOrDefault("amount")) ?? entry.Amount;
        entry.PaymentLegalEntity = legalEntity;
        entry.Account = account;
        entry.PaymentMethod = paymentMethod;
        entry.ActualRecipientName = values.GetValueOrDefault("actual_recipient_name") ?? employee.Name;
        entry.Project = ResolveProject(values.GetValueOrDefault("project_number"));
        entry.Notes = values.GetValueOrDefault("notes");
    }

    private void UpdateEmployeeFinancialAdjustment(EmployeeFinancialAdjustment entry, Dictionary<string, string?> values)
    {
        var employee = FindEmployee(values["employee_number"]!);
        if (!TryParseEmployeeFinancialAdjustmentType(values.GetValueOrDefault("adjustment_type"), out var adjustmentType)) throw new InvalidOperationException("调整类型无法解析。");
        var adjustmentDate = ParseDate(values.GetValueOrDefault("adjustment_date"))!.Value;
        entry.Employee = employee;
        entry.BusinessYear = ResolveBusinessYear(values.GetValueOrDefault("business_year"), adjustmentDate);
        entry.AdjustmentDate = adjustmentDate;
        entry.Amount = ParseDecimal(values.GetValueOrDefault("amount")) ?? entry.Amount;
        entry.AdjustmentType = adjustmentType;
        entry.Notes = values.GetValueOrDefault("notes") ?? string.Empty;
    }

    private static decimal ParseDecimal(string? value, decimal fallback = 0m) => decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : fallback;

    private static void EnsureConcurrency(Guid current, string? expected, string label)
    {
        if (!string.IsNullOrWhiteSpace(expected) && Guid.TryParse(expected, out var parsed) && parsed != current)
        {
            throw new InvalidOperationException($"{label}已被其他用户修改，导入已停止，请重新导出后再导入。");
        }
    }

    private static bool TryParsePaymentMethod(string? value, out PaymentMethod paymentMethod)
    {
        switch (value?.Trim())
        {
            case "银行转账":
            case "银行":
            case "BankTransfer":
                paymentMethod = PaymentMethod.BankTransfer;
                return true;
            case "现金":
            case "Cash":
                paymentMethod = PaymentMethod.Cash;
                return true;
            case "微信":
            case "WeChat":
                paymentMethod = PaymentMethod.WeChat;
                return true;
            case "支付宝":
            case "Alipay":
                paymentMethod = PaymentMethod.Alipay;
                return true;
            case "其他":
            case "Other":
                paymentMethod = PaymentMethod.Other;
                return true;
            default:
                paymentMethod = default;
                return false;
        }
    }

    private static bool TryParseLedgerDirection(string? value, out LedgerDirection direction)
    {
        switch (value?.Trim())
        {
            case "应收":
            case "销项":
            case "收款":
            case "Receivable":
            case "Output":
                direction = LedgerDirection.Receivable;
                return true;
            case "应付":
            case "进项":
            case "付款":
            case "Payable":
            case "Input":
                direction = LedgerDirection.Payable;
                return true;
            default:
                direction = default;
                return false;
        }
    }

    private static bool TryParseLedgerStatus(string? value, out LedgerRecordStatus status)
    {
        switch (value?.Trim())
        {
            case "有效":
            case "Active":
                status = LedgerRecordStatus.Active;
                return true;
            case "已作废":
            case "作废":
            case "Voided":
                status = LedgerRecordStatus.Voided;
                return true;
            default:
                status = default;
                return false;
        }
    }

    private static bool TryParsePayrollBatchType(string? value, out PayrollBatchType batchType)
    {
        switch (value?.Trim())
        {
            case "按月":
            case "Monthly": batchType = PayrollBatchType.Monthly; return true;
            case "按日期范围":
            case "DateRange": batchType = PayrollBatchType.DateRange; return true;
            case "项目阶段":
            case "ProjectStage": batchType = PayrollBatchType.ProjectStage; return true;
            case "里程碑":
            case "Milestone": batchType = PayrollBatchType.Milestone; return true;
            case "临时":
            case "Temporary": batchType = PayrollBatchType.Temporary; return true;
            default: batchType = default; return false;
        }
    }

    private static bool TryParsePayrollRecipientType(string? value, out PayrollRecipientType recipientType)
    {
        switch (value?.Trim())
        {
            case "员工":
            case "Employee": recipientType = PayrollRecipientType.Employee; return true;
            case "班组工人":
            case "CrewWorker": recipientType = PayrollRecipientType.CrewWorker; return true;
            default: recipientType = default; return false;
        }
    }

    private static bool TryParseEmployeeWageCategory(string? value, out EmployeeWageCategory category)
    {
        switch (value?.Trim())
        {
            case "社保工资":
            case "SocialSecurityWage": category = EmployeeWageCategory.SocialSecurityWage; return true;
            case "农民工工资":
            case "MigrantWorkerWage": category = EmployeeWageCategory.MigrantWorkerWage; return true;
            case "其他工资":
            case "OtherWage": category = EmployeeWageCategory.OtherWage; return true;
            default: category = default; return false;
        }
    }

    private static bool TryParseEmployeeWageCalculationMethod(string? value, out EmployeeWageCalculationMethod method)
    {
        switch (value?.Trim())
        {
            case "按月":
            case "Monthly": method = EmployeeWageCalculationMethod.Monthly; return true;
            case "按日":
            case "Daily": method = EmployeeWageCalculationMethod.Daily; return true;
            case "按小时":
            case "Hourly": method = EmployeeWageCalculationMethod.Hourly; return true;
            case "按计件":
            case "Piecework": method = EmployeeWageCalculationMethod.Piecework; return true;
            case "固定金额":
            case "FixedAmount": method = EmployeeWageCalculationMethod.FixedAmount; return true;
            case "自定义单位":
            case "CustomUnit": method = EmployeeWageCalculationMethod.CustomUnit; return true;
            default: method = default; return false;
        }
    }

    private static bool TryParseEmployeeWageEntryType(string? value, out EmployeeWageEntryType entryType)
    {
        switch (value?.Trim())
        {
            case "出勤":
            case "Attendance": entryType = EmployeeWageEntryType.Attendance; return true;
            case "加班":
            case "Overtime": entryType = EmployeeWageEntryType.Overtime; return true;
            case "奖金":
            case "Bonus": entryType = EmployeeWageEntryType.Bonus; return true;
            case "罚款":
            case "Penalty": entryType = EmployeeWageEntryType.Penalty; return true;
            case "其他":
            case "Other": entryType = EmployeeWageEntryType.Other; return true;
            default: entryType = default; return false;
        }
    }

    private static bool TryParsePayrollItemNature(string? value, out PayrollItemNature nature)
    {
        switch (value?.Trim())
        {
            case "收入":
            case "Earning": nature = PayrollItemNature.Earning; return true;
            case "扣款":
            case "Deduction": nature = PayrollItemNature.Deduction; return true;
            default: nature = default; return false;
        }
    }

    private static bool TryParseEmployeeLedgerEntryType(string? value, out EmployeeLedgerEntryType entryType)
    {
        switch (value?.Trim())
        {
            case "费用":
            case "Expense": entryType = EmployeeLedgerEntryType.Expense; return true;
            case "借支发放":
            case "AdvanceDisbursement": entryType = EmployeeLedgerEntryType.AdvanceDisbursement; return true;
            case "借支归还":
            case "AdvanceRepayment": entryType = EmployeeLedgerEntryType.AdvanceRepayment; return true;
            case "分红":
            case "Dividend": entryType = EmployeeLedgerEntryType.Dividend; return true;
            case "利息":
            case "Interest": entryType = EmployeeLedgerEntryType.Interest; return true;
            case "其他":
            case "Other": entryType = EmployeeLedgerEntryType.Other; return true;
            default: entryType = default; return false;
        }
    }

    private static bool TryParseEmployeeLedgerRecordKind(string? value, out EmployeeLedgerRecordKind recordKind)
    {
        switch (value?.Trim())
        {
            case "应付":
            case "Payable": recordKind = EmployeeLedgerRecordKind.Payable; return true;
            case "已付款":
            case "Payment": recordKind = EmployeeLedgerRecordKind.Payment; return true;
            case "退款/冲销":
            case "RefundOrReversal": recordKind = EmployeeLedgerRecordKind.RefundOrReversal; return true;
            default: recordKind = default; return false;
        }
    }

    private static bool TryParseEmployeeReceiptType(string? value, out EmployeeReceiptType receiptType)
    {
        switch (value?.Trim())
        {
            case "工资":
            case "Wage": receiptType = EmployeeReceiptType.Wage; return true;
            case "报销":
            case "Expense": receiptType = EmployeeReceiptType.Expense; return true;
            case "分红/其他":
            case "DividendOrOther": receiptType = EmployeeReceiptType.DividendOrOther; return true;
            case "借支":
            case "Advance": receiptType = EmployeeReceiptType.Advance; return true;
            case "通用":
            case "General": receiptType = EmployeeReceiptType.General; return true;
            default: receiptType = default; return false;
        }
    }

    private static bool TryParseEmployeeFinancialAdjustmentType(string? value, out EmployeeFinancialAdjustmentType adjustmentType)
    {
        switch (value?.Trim())
        {
            case "管理员调整":
            case "AdministratorAdjustment": adjustmentType = EmployeeFinancialAdjustmentType.AdministratorAdjustment; return true;
            case "历史期初余额":
            case "HistoricalOpeningBalance": adjustmentType = EmployeeFinancialAdjustmentType.HistoricalOpeningBalance; return true;
            case "冲销":
            case "Reversal": adjustmentType = EmployeeFinancialAdjustmentType.Reversal; return true;
            default: adjustmentType = default; return false;
        }
    }

    private static bool TryParseEmployeeType(string value, out EmployeeType employeeType)
    {
        if (value is "正式员工" or "Formal") { employeeType = EmployeeType.Formal; return true; }
        if (value is "劳务员工" or "Labor") { employeeType = EmployeeType.Labor; return true; }
        if (value is "特殊临时人员" or "Temporary") { employeeType = EmployeeType.Temporary; return true; }
        employeeType = default;
        return false;
    }

    private static bool TryParseAccountType(string? value, out EngineeringManager.Domain.Finance.FinancialAccountType accountType)
    {
        if (value is "银行" or "Bank") { accountType = EngineeringManager.Domain.Finance.FinancialAccountType.Bank; return true; }
        if (value is "现金" or "Cash") { accountType = EngineeringManager.Domain.Finance.FinancialAccountType.Cash; return true; }
        if (value is "其他" or "Other") { accountType = EngineeringManager.Domain.Finance.FinancialAccountType.Other; return true; }
        accountType = default;
        return false;
    }

    private static bool ParseBoolean(string? value) => value is "是" or "true" or "True" or "1";

    private EquipmentProjectUsage FindUsage(Dictionary<string, string?> values)
    {
        var equipmentNumber = values["equipment_number"]!; var projectNumber = values["project_number"]!; var entryDate = ParseDate(values["usage_entry_date"])!.Value;
        return db.EquipmentProjectUsages.Include(item => item.Equipment).Include(item => item.Project).Single(item => item.Equipment.EquipmentNumber == equipmentNumber && item.Project.ProjectNumber == projectNumber && item.EntryDate == entryDate);
    }

    private static bool TryParseOwnership(string? value, out EquipmentOwnershipType type) { if (value is "自有" or "SelfOwned") { type = EquipmentOwnershipType.SelfOwned; return true; } if (value is "租赁" or "Rented") { type = EquipmentOwnershipType.Rented; return true; } if (value is "其他" or "Other") { type = EquipmentOwnershipType.Other; return true; } type = default; return false; }
    private static bool TryParseRentMode(string? value, out RentMode mode) { if (value is "日租" or "Daily") { mode = RentMode.Daily; return true; } if (value is "月租" or "Monthly") { mode = RentMode.Monthly; return true; } if (value is "阶段包干" or "StagePackage") { mode = RentMode.StagePackage; return true; } mode = default; return false; }
    private static bool TryParsePeriodType(string? value, out EquipmentPeriodType type) { if (value is "施工" or "Work") { type = EquipmentPeriodType.Work; return true; } if (value is "停工" or "Stop") { type = EquipmentPeriodType.Stop; return true; } type = default; return false; }
    private static decimal? ParseDecimal(string? value) => decimal.TryParse(value, out var number) ? number : null;

    private static DateOnly? ParseDate(string? value) => DateOnly.TryParse(value, out var date) ? date : null;

    private static void ValidateDate(string? value, int row, string column, List<ImportErrorDto> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) && !DateOnly.TryParse(value, out _))
        {
            errors.Add(new ImportErrorDto(row, column, "日期格式无效，应为 yyyy-MM-dd。", value));
        }
    }
    private static void ValidateDecimal(string? value, int row, string column, List<ImportErrorDto> errors) { if (!string.IsNullOrWhiteSpace(value) && !decimal.TryParse(value, out _)) errors.Add(new ImportErrorDto(row, column, "金额或单价格式无效。", value)); }

    private static IReadOnlyList<ImportColumn> GetColumns(ExportDataset dataset) =>
        Columns.TryGetValue(dataset, out var columns) ? columns : throw new NotSupportedException($"暂不支持导入数据集：{dataset}");

    private static bool IsCompleteEmployeeWorkbook(ExportDataset dataset, IReadOnlyList<SimpleXlsxSheet> sheets) =>
        dataset == ExportDataset.Employees && sheets.Any(item => string.Equals(item.Name.Trim(), "员工总表", StringComparison.OrdinalIgnoreCase));

    private static string HeaderFor(ExportDataset dataset, string key) => GetColumns(dataset).Single(item => item.Key == key).Header;

    private static string TemplateSheetName(ExportDataset dataset) => dataset switch
    {
        ExportDataset.Employees => "员工导入",
        ExportDataset.Payroll => "工资导入",
        ExportDataset.Collections => "收款导入",
        ExportDataset.Payments => "付款导入",
        ExportDataset.Invoices => "发票导入",
        ExportDataset.EmployeeWages => "员工工资明细导入",
        ExportDataset.EmployeeOtherPayments => "员工往来导入",
        ExportDataset.EmployeeReceipts => "员工收款导入",
        ExportDataset.EmployeeFinancialAdjustments => "员工财务调整导入",
        ExportDataset.EmployeeCertificates => "员工证书导入",
        ExportDataset.Partners => "合作单位导入",
        ExportDataset.Projects => "项目导入",
        ExportDataset.Contracts => "合同导入",
        ExportDataset.StageResults => "阶段成果导入",
        ExportDataset.Companies => "公司导入",
        ExportDataset.CompanyAccounts => "公司账户导入",
        ExportDataset.CompanyCertificates => "公司证书导入",
        ExportDataset.Equipment => "设备导入",
        ExportDataset.EquipmentLeases => "设备租赁导入",
        ExportDataset.EquipmentUsages => "设备使用导入",
        ExportDataset.EquipmentPeriods => "设备日期段导入",
        ExportDataset.EquipmentSettlements => "设备结算导入",
        _ => throw new NotSupportedException($"暂不支持导入数据集：{dataset}")
    };

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("值不能为空。", parameterName);
        return value.Trim();
    }

    private sealed record ImportColumn(string Header, string Key, bool Required);
}
