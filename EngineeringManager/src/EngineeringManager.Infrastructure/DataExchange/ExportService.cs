using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Projects;
using EngineeringManager.Infrastructure.Files;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed class ExportService : IExportService
{
    private readonly ApplicationDbContext db;
    private readonly IFinanceLedgerService financeService;
    private readonly IFileStore? fileStore;

    public ExportService(ApplicationDbContext db, IFinanceLedgerService financeService)
        : this(db, financeService, null) { }

    public ExportService(ApplicationDbContext db, IFinanceLedgerService financeService, IFileStore? fileStore)
    {
        this.db = db;
        this.financeService = financeService;
        this.fileStore = fileStore;
    }

    private const string LastSelectionName = "__last_selection__";
    private static readonly Dictionary<ExportDataset, IReadOnlyList<ExportFieldDefinition>> Catalogs =
        new Dictionary<ExportDataset, IReadOnlyList<ExportFieldDefinition>>
        {
            [ExportDataset.ProjectOverview] =
            [
                new("serial_number", "序号", ExportFieldDataType.Number, true, CanImport: false),
                new("project_number", "项目编号", ExportFieldDataType.Text, true),
                new("project_name", "项目名称", ExportFieldDataType.Text, true),
                new("stage", "项目阶段", ExportFieldDataType.Text, true),
                new("contract_signing_status", "合同签订", ExportFieldDataType.Text, true),
                new("affiliation_type", "项目合作方式", ExportFieldDataType.Text, true),
                new("parent_project", "上级项目", ExportFieldDataType.Text, false),
                new("general_contractor", "总包单位", ExportFieldDataType.Text, true),
                new("general_contractor_contact", "总包联系人", ExportFieldDataType.Text, false),
                new("general_contractor_phone", "总包电话", ExportFieldDataType.Text, false),
                new("responsible_user", "项目负责人", ExportFieldDataType.Text, false),
                new("department", "部门", ExportFieldDataType.Text, false),
                new("branch", "分支机构", ExportFieldDataType.Text, false),
                new("legal_entities", "签约公司", ExportFieldDataType.Text, false),
                new("actual_start_date", "实际开始日期", ExportFieldDataType.Date, false),
                new("actual_completion_date", "实际完工日期", ExportFieldDataType.Date, false),
                new("contract_amount", "合同金额", ExportFieldDataType.Number, true),
                new("estimated_amount", "预计金额", ExportFieldDataType.Number, true),
                new("settled_amount", "已结算金额", ExportFieldDataType.Number, true),
                new("current_project_amount", "当前工程金额", ExportFieldDataType.Number, true),
                new("settlement_status", "结算状态", ExportFieldDataType.Text, true),
                new("contract_count", "合同数量", ExportFieldDataType.Number, true, CanImport: false),
                new("line_item_count", "清单项数量", ExportFieldDataType.Number, true, CanImport: false),
                new("receivable_amount", "应收款", ExportFieldDataType.Number, true),
                new("collected_amount", "已收款", ExportFieldDataType.Number, true),
                new("uncollected_amount", "未收款", ExportFieldDataType.Number, true),
                new("payable_amount", "应付款", ExportFieldDataType.Number, true),
                new("paid_amount", "已付款", ExportFieldDataType.Number, true),
                new("unpaid_amount", "未付款", ExportFieldDataType.Number, true),
                new("expected_invoice_amount", "应开票", ExportFieldDataType.Number, true),
                new("output_invoice_amount", "已开票", ExportFieldDataType.Number, true),
                new("uninvoiced_amount", "未开票", ExportFieldDataType.Number, true),
                new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.Employees] =
            [
                new("employee_number", "员工编号", ExportFieldDataType.Text, true),
                new("name", "姓名", ExportFieldDataType.Text, true),
                new("employee_type", "员工类型", ExportFieldDataType.Text, true),
                new("position", "岗位", ExportFieldDataType.Text, true),
                new("phone", "电话", ExportFieldDataType.Text, false),
                new("identity_number", "身份证号", ExportFieldDataType.Text, false, true, true, true),
                new("bank_account_number", "银行卡号", ExportFieldDataType.Text, false, true, true, true),
                new("bank_name", "开户行", ExportFieldDataType.Text, false, true, true, true),
                new("default_monthly_salary", "默认月工资", ExportFieldDataType.Number, false, true, true, true),
                new("default_daily_rate", "默认日工资", ExportFieldDataType.Number, false, true, true, true),
                new("default_hourly_rate", "默认时工资", ExportFieldDataType.Number, false, true, true, true),
                new("default_piecework_rate", "默认计件单价", ExportFieldDataType.Number, false, true, true, true),
                new("_system_id", "系统ID", ExportFieldDataType.Text, false, true, true, false, false, true),
                new("_concurrency_stamp", "并发版本", ExportFieldDataType.Text, false, true, true, false, false, true),
                new("is_active", "状态", ExportFieldDataType.Boolean, true),
                new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.Projects] =
            [
                new("project_number", "项目编号", ExportFieldDataType.Text, true),
                new("project_name", "项目名称", ExportFieldDataType.Text, true),
                new("stage", "项目阶段", ExportFieldDataType.Text, true),
                new("contract_signing_status", "合同状态", ExportFieldDataType.Text, true),
                new("affiliation_type", "项目合作方式", ExportFieldDataType.Text, true),
                new("general_contractor", "总包单位", ExportFieldDataType.Text, false),
                new("actual_start_date", "实际开工日期", ExportFieldDataType.Date, false),
                new("actual_completion_date", "实际完工日期", ExportFieldDataType.Date, false),
                new("is_active", "状态", ExportFieldDataType.Boolean, true),
                new("notes", "备注", ExportFieldDataType.Text, false),
                new("_system_id", "系统ID", ExportFieldDataType.Text, false, true, true, false, false, true),
                new("_concurrency_stamp", "并发版本", ExportFieldDataType.Text, false, true, true, false, false, true)
            ],
            [ExportDataset.Contracts] =
            [
                new("project_number", "项目编号", ExportFieldDataType.Text, true), new("contract_number", "合同编号", ExportFieldDataType.Text, true), new("name", "合同名称", ExportFieldDataType.Text, true), new("contract_type", "合同类型", ExportFieldDataType.Text, true), new("counterparty_name", "对方单位", ExportFieldDataType.Text, false), new("signed_date", "签订日期", ExportFieldDataType.Date, false), new("total_amount", "合同金额", ExportFieldDataType.Number, true), new("is_active", "状态", ExportFieldDataType.Boolean, true), new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.StageResults] =
            [
                new("project_number", "项目编号", ExportFieldDataType.Text, true), new("title", "成果标题", ExportFieldDataType.Text, true), new("result_type", "成果类型", ExportFieldDataType.Text, true), new("status", "状态", ExportFieldDataType.Text, true), new("result_date", "成果日期", ExportFieldDataType.Date, true), new("quality_result", "质量结果", ExportFieldDataType.Text, false), new("description", "说明", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.EmployeeCertificates] =
            [
                new("employee_number", "员工编号", ExportFieldDataType.Text, true),
                new("employee_name", "员工姓名", ExportFieldDataType.Text, true),
                new("certificate_type", "证书类型", ExportFieldDataType.Text, true),
                new("certificate_number", "证书编号", ExportFieldDataType.Text, false),
                new("specialty_level_scope", "专业/等级/范围", ExportFieldDataType.Text, true),
                new("issuing_authority", "发证机关", ExportFieldDataType.Text, false),
                new("issued_on", "签发日期", ExportFieldDataType.Date, false),
                new("expires_on", "到期日期", ExportFieldDataType.Date, true),
                new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.Partners] =
            [
                new("partner_number", "单位编号", ExportFieldDataType.Text, true),
                new("name", "单位名称", ExportFieldDataType.Text, true),
                new("short_name", "简称", ExportFieldDataType.Text, true),
                new("roles", "业务角色", ExportFieldDataType.Text, true),
                new("is_active", "状态", ExportFieldDataType.Boolean, true),
                new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.Payroll] =
            [
                new("batch_number", "批次编号", ExportFieldDataType.Text, true),
                new("batch_name", "批次名称", ExportFieldDataType.Text, true),
                new("batch_type", "批次类型", ExportFieldDataType.Text, true),
                new("start_date", "开始日期", ExportFieldDataType.Date, true),
                new("end_date", "结束日期", ExportFieldDataType.Date, true),
                new("payment_date", "发放日期", ExportFieldDataType.Date, true),
                new("project", "发放项目", ExportFieldDataType.Text, false),
                new("legal_entity", "发放公司", ExportFieldDataType.Text, false),
                new("account", "付款账户", ExportFieldDataType.Text, false),
                new("recipient_type", "人员来源", ExportFieldDataType.Text, true),
                new("recipient_name", "人员姓名", ExportFieldDataType.Text, true),
                new("crew", "施工班组", ExportFieldDataType.Text, false),
                new("amount", "个人金额", ExportFieldDataType.Number, true, true, true, true),
                new("actual_amount", "批次实际总额", ExportFieldDataType.Number, true, true, true, true),
                new("payable_amount", "应发工资", ExportFieldDataType.Number, true, true, true, true),
                new("paid_amount", "已发工资", ExportFieldDataType.Number, true, true, true, true),
                new("unpaid_amount", "未发工资", ExportFieldDataType.Number, true, true, true, true),
                new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.Collections] =
            [
                new("project_number", "项目编号", ExportFieldDataType.Text, true),
                new("collection_date", "收款日期", ExportFieldDataType.Date, true),
                new("legal_entity", "签约公司", ExportFieldDataType.Text, true),
                new("partner", "合作单位", ExportFieldDataType.Text, false),
                new("account", "收款账户", ExportFieldDataType.Text, true),
                new("amount", "收款金额", ExportFieldDataType.Number, true),
                new("payment_method", "收款方式", ExportFieldDataType.Text, true),
                new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.Payments] =
            [
                new("project_number", "项目编号", ExportFieldDataType.Text, true),
                new("payment_date", "付款日期", ExportFieldDataType.Date, true),
                new("legal_entity", "签约公司", ExportFieldDataType.Text, true),
                new("partner", "合作单位", ExportFieldDataType.Text, true),
                new("account", "付款账户", ExportFieldDataType.Text, true),
                new("amount", "付款金额", ExportFieldDataType.Number, true),
                new("payment_method", "付款方式", ExportFieldDataType.Text, true),
                new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.Invoices] =
            [
                new("project_number", "项目编号", ExportFieldDataType.Text, true),
                new("invoice_number", "发票号码", ExportFieldDataType.Text, true),
                new("invoice_date", "发票日期", ExportFieldDataType.Date, true),
                new("direction", "发票方向", ExportFieldDataType.Text, true),
                new("legal_entity", "签约公司", ExportFieldDataType.Text, true),
                new("gross_amount", "含税金额", ExportFieldDataType.Number, true),
                new("status", "状态", ExportFieldDataType.Text, true)
            ],
            [ExportDataset.Accounts] =
            [
                new("legal_entity", "签约公司", ExportFieldDataType.Text, true),
                new("account_name", "账户名称", ExportFieldDataType.Text, true),
                new("account_type", "账户类型", ExportFieldDataType.Text, true),
                new("opening_balance", "期初余额", ExportFieldDataType.Number, true),
                new("current_balance", "当前余额", ExportFieldDataType.Number, true),
                new("is_active", "状态", ExportFieldDataType.Boolean, true),
                new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.Companies] =
            [
                new("company_code", "公司编码", ExportFieldDataType.Text, true),
                new("name", "公司全称", ExportFieldDataType.Text, true),
                new("short_name", "公司简称", ExportFieldDataType.Text, true),
                new("category", "组合分类", ExportFieldDataType.Text, true),
                new("legal_representative", "法人/经营者", ExportFieldDataType.Text, true),
                new("tax_code", "统一社会信用代码/税号", ExportFieldDataType.Text, true),
                new("phone", "电话", ExportFieldDataType.Text, false),
                new("registered_address", "注册地址", ExportFieldDataType.Text, false),
                new("business_address", "经营地址", ExportFieldDataType.Text, false),
                new("invoice_title", "开票抬头", ExportFieldDataType.Text, false),
                new("is_active", "状态", ExportFieldDataType.Boolean, true),
                new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.CompanyAccounts] =
            [
                new("company_code", "公司编码", ExportFieldDataType.Text, true),
                new("account_name", "账户名称", ExportFieldDataType.Text, true),
                new("account_type", "账户类型", ExportFieldDataType.Text, true),
                new("account_number", "账号", ExportFieldDataType.Text, false, true, true, true),
                new("bank_name", "开户行", ExportFieldDataType.Text, false, true, true, true),
                new("opening_balance", "期初余额", ExportFieldDataType.Number, true),
                new("default_collection", "默认收款", ExportFieldDataType.Boolean, true),
                new("default_payment", "默认付款", ExportFieldDataType.Boolean, true),
                new("default_invoice", "默认开票", ExportFieldDataType.Boolean, true),
                new("is_active", "状态", ExportFieldDataType.Boolean, true),
                new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.CompanyCertificates] =
            [
                new("company_code", "公司编码", ExportFieldDataType.Text, true),
                new("certificate_type", "证书类型", ExportFieldDataType.Text, true),
                new("certificate_number", "证书编号", ExportFieldDataType.Text, false),
                new("specialty_level_scope", "专业/等级/范围", ExportFieldDataType.Text, true),
                new("issuing_authority", "发证机关", ExportFieldDataType.Text, false),
                new("issued_on", "签发日期", ExportFieldDataType.Date, false),
                new("expires_on", "到期日期", ExportFieldDataType.Date, false),
                new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.Equipment] =
            [
                new("equipment_number", "设备编号", ExportFieldDataType.Text, true),
                new("name", "设备名称", ExportFieldDataType.Text, true),
                new("model", "型号", ExportFieldDataType.Text, true),
                new("category", "分类", ExportFieldDataType.Text, true),
                new("ownership", "权属", ExportFieldDataType.Text, true),
                new("owner_company", "所属公司", ExportFieldDataType.Text, true),
                new("lessor", "出租方", ExportFieldDataType.Text, true),
                new("status", "状态", ExportFieldDataType.Text, true),
                new("internal_daily_rate", "内部参考日价", ExportFieldDataType.Number, false),
                new("notes", "备注", ExportFieldDataType.Text, false)
            ],
            [ExportDataset.EquipmentLeases] =
            [
                new("equipment_number", "设备编号", ExportFieldDataType.Text, true), new("contract_number", "租赁合同号", ExportFieldDataType.Text, true), new("lessor", "出租方", ExportFieldDataType.Text, true), new("start_date", "开始日期", ExportFieldDataType.Date, true), new("end_date", "结束日期", ExportFieldDataType.Date, true), new("rent_mode", "计租方式", ExportFieldDataType.Text, true), new("unit_rate", "基础单价", ExportFieldDataType.Number, true)
            ],
            [ExportDataset.EquipmentUsages] =
            [
                new("equipment_number", "设备编号", ExportFieldDataType.Text, true), new("project_number", "项目编号", ExportFieldDataType.Text, true), new("company", "签约公司", ExportFieldDataType.Text, true), new("entry_date", "进场日期", ExportFieldDataType.Date, true), new("exit_date", "退场日期", ExportFieldDataType.Date, true), new("rent_mode", "计租方式", ExportFieldDataType.Text, true), new("unit_rate", "基础单价", ExportFieldDataType.Number, true)
            ],
            [ExportDataset.EquipmentPeriods] =
            [
                new("equipment_number", "设备编号", ExportFieldDataType.Text, true), new("project_number", "项目编号", ExportFieldDataType.Text, true), new("start_date", "开始日期", ExportFieldDataType.Date, true), new("end_date", "结束日期", ExportFieldDataType.Date, true), new("period_type", "日期段类型", ExportFieldDataType.Text, true), new("chargeable", "是否计租", ExportFieldDataType.Boolean, true)
            ],
            [ExportDataset.EquipmentSettlements] =
            [
                new("equipment_number", "设备编号", ExportFieldDataType.Text, true), new("project_number", "项目编号", ExportFieldDataType.Text, true), new("settlement_date", "结算日期", ExportFieldDataType.Date, true), new("base_amount", "基础租金", ExportFieldDataType.Number, true), new("total_amount", "结算总额", ExportFieldDataType.Number, true), new("offset_amount", "抵扣金额", ExportFieldDataType.Number, true), new("payable_id", "应付记录", ExportFieldDataType.Text, false), new("notes", "备注", ExportFieldDataType.Text, false)
            ]
        };

    public IReadOnlyList<ExportFieldDefinition> GetFieldCatalog(ExportDataset dataset) =>
        Catalogs.TryGetValue(dataset, out var fields) ? fields : throw new NotSupportedException($"暂不支持导出数据集：{dataset}");

    public async Task<ExportFileResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        var userId = NormalizeRequired(request.UserId, nameof(request.UserId));
        var fields = ExportSelectionValidator.ResolveFields(GetFieldCatalog(request.Dataset), request.SelectedFields)
            .Select(field => request.CanViewSensitiveData ? field with { IsSensitive = false } : field)
            .ToArray();
        ExportSelectionValidator.ValidateCutoffDate(request.CutoffDate);
        var task = new DataExchangeTask
        {
            UserId = userId,
            Direction = DataExchangeDirection.Export,
            DatasetsJson = JsonSerializer.Serialize(new[] { request.Dataset }),
            SelectedFieldsJson = JsonSerializer.Serialize(new Dictionary<ExportDataset, IReadOnlyList<string>> { [request.Dataset] = fields.Select(item => item.Key).ToArray() }),
            FilterJson = JsonSerializer.Serialize(new { request.CutoffDate, request.ProjectIds }),
            Scope = request.Scope,
            PackageFormat = request.PackageFormat,
            IncludeAttachments = request.IncludeAttachments,
            Status = DataExchangeTaskStatus.Running
        };
        db.DataExchangeTasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var file = request.Dataset switch
            {
            ExportDataset.ProjectOverview => await ExportProjectOverviewAsync(fields, request.CutoffDate, request.ProjectIds, cancellationToken),
            ExportDataset.Projects => await ExportProjectsAsync(fields, cancellationToken),
            ExportDataset.Contracts => await ExportContractsAsync(fields, cancellationToken),
            ExportDataset.StageResults => await ExportStageResultsAsync(fields, cancellationToken),
            ExportDataset.Employees => await ExportEmployeesAsync(fields, cancellationToken),
            ExportDataset.EmployeeCertificates => await ExportEmployeeCertificatesAsync(fields, cancellationToken),
            ExportDataset.Partners => await ExportPartnersAsync(fields, cancellationToken),
            ExportDataset.Payroll => await ExportPayrollAsync(fields, request.CutoffDate, cancellationToken),
            ExportDataset.Collections => await ExportCollectionsAsync(fields, request.CutoffDate, cancellationToken),
            ExportDataset.Payments => await ExportPaymentsAsync(fields, request.CutoffDate, cancellationToken),
            ExportDataset.Invoices => await ExportInvoicesAsync(fields, request.CutoffDate, cancellationToken),
            ExportDataset.Accounts => await ExportAccountsAsync(fields, request.CutoffDate, cancellationToken),
            ExportDataset.Companies => await ExportCompaniesAsync(fields, cancellationToken),
            ExportDataset.CompanyAccounts => await ExportCompanyAccountsAsync(fields, cancellationToken),
            ExportDataset.CompanyCertificates => await ExportCompanyCertificatesAsync(fields, cancellationToken),
            ExportDataset.Equipment => await ExportEquipmentAsync(fields, cancellationToken),
            ExportDataset.EquipmentLeases => await ExportEquipmentLeasesAsync(fields, cancellationToken),
            ExportDataset.EquipmentUsages => await ExportEquipmentUsagesAsync(fields, cancellationToken),
            ExportDataset.EquipmentPeriods => await ExportEquipmentPeriodsAsync(fields, cancellationToken),
            ExportDataset.EquipmentSettlements => await ExportEquipmentSettlementsAsync(fields, cancellationToken),
                _ => throw new NotSupportedException($"暂不支持导出数据集：{request.Dataset}")
            };
            task.Status = DataExchangeTaskStatus.Completed;
            task.FileName = file.FileName;
            task.ContentType = file.ContentType;
            task.ResultContent = file.Content;
            task.RowCount = SimpleXlsxReader.Read(file.Content).Sum(sheet => Math.Max(0, sheet.Rows.Count - 1));
            task.Sha256 = Convert.ToHexString(SHA256.HashData(file.Content));
            task.CompletedAt = DateTimeOffset.UtcNow;
            db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "DataExport", EntityType = nameof(DataExchangeTask), EntityId = task.Id.ToString(), Reason = $"导出 {request.Dataset}", AfterJson = JsonSerializer.Serialize(new { request.Dataset, fields = fields.Select(item => item.Key), task.RowCount, task.Sha256 }) });
            await SaveLastSelectionAsync(userId, request.Dataset, fields.Select(item => item.Key).ToArray(), request.CutoffDate, cancellationToken);
            return file;
        }
        catch (Exception exception)
        {
            task.Status = DataExchangeTaskStatus.Failed;
            task.ErrorMessage = exception.Message[..Math.Min(exception.Message.Length, 2000)];
            task.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ExportFileResult> ExportModulesAsync(ExportModuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var datasets = request.Datasets.Distinct().ToArray();
        if (datasets.Length == 0) throw new ArgumentException("至少选择一个数据集。", nameof(request));
        if (request.IncludeAttachments && !request.CanViewSensitiveData) throw new UnauthorizedAccessException("只有具备敏感数据权限的管理员可以导出附件。");
        var files = new List<ExportFileResult>(datasets.Length);
        foreach (var dataset in datasets)
        {
            request.SelectedFields.TryGetValue(dataset, out var selectedFields);
            IReadOnlyList<Guid>? projectIds = null;
            request.ProjectIds?.TryGetValue(dataset, out projectIds);
            files.Add(await ExportAsync(new ExportRequest(dataset, request.UserId, selectedFields ?? [], null, projectIds, request.CanViewSensitiveData, ExportScope.SelectedModules, request.PackageFormat, request.IncludeAttachments), cancellationToken));
        }
        if (datasets.Length == 1 && request.PackageFormat == ExportPackageFormat.Workbook) return files[0];

        if (request.PackageFormat == ExportPackageFormat.Workbook)
        {
            var workbook = new SimpleXlsxWorkbook();
            var directory = datasets.Select((dataset, index) => (IReadOnlyList<object?>)[new XlsxHyperlink(dataset.ToString(), $"'{DatasetSheetName(dataset)}'!A1"), new XlsxHyperlink(files[index].FileName, files[index].FileName, true)]).ToArray();
            workbook.AddWorksheet("目录", ["数据集", "文件"], directory);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "目录" };
            foreach (var file in files)
            {
                foreach (var sheet in SimpleXlsxReader.Read(file.Content))
                {
                    var sheetName = SafeWorksheetName(sheet.Name, usedNames);
                    workbook.AddWorksheet(sheetName, sheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray(), sheet.Rows.Skip(1));
                }
            }
            return new ExportFileResult($"数据交换_{DateTime.Now:yyyyMMddHHmmss}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", workbook.ToArray());
        }

        await using var output = new MemoryStream();
        var attachments = request.IncludeAttachments
            ? await db.Attachments.AsNoTracking().Where(item => !item.IsDeleted).OrderBy(item => item.UploadedAt).ToListAsync(cancellationToken)
            : [];
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var navigation = new SimpleXlsxWorkbook();
            navigation.AddWorksheet("目录", ["数据集", "文件"], datasets.Select((dataset, index) => (IReadOnlyList<object?>)[dataset.ToString(), new XlsxHyperlink(files[index].FileName, files[index].FileName, true)]));
            if (attachments.Count > 0)
            {
                navigation.AddWorksheet("附件", ["原文件名", "所属项目", "相对路径"], attachments.Select(item =>
                {
                    var relativePath = $"attachments/{item.Id:N}/{SafeFileName(item.OriginalFileName)}";
                    return (IReadOnlyList<object?>)[item.OriginalFileName, item.ProjectId?.ToString(), new XlsxHyperlink(relativePath, relativePath, true)];
                }));
            }
            var navigationEntry = archive.CreateEntry("data-navigation.xlsx", CompressionLevel.Fastest);
            await using (var navigationStream = navigationEntry.Open())
            {
                var navigationBytes = navigation.ToArray();
                await navigationStream.WriteAsync(navigationBytes, cancellationToken);
            }
            var checksumLines = new List<string> { $"{Convert.ToHexString(SHA256.HashData(navigation.ToArray()))}  data-navigation.xlsx" };
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.FileName, CompressionLevel.Fastest);
                await using var target = entry.Open();
                await target.WriteAsync(file.Content, cancellationToken);
                checksumLines.Add($"{Convert.ToHexString(SHA256.HashData(file.Content))}  {file.FileName}");
            }
            if (attachments.Count > 0 && fileStore is not null)
            {
                foreach (var attachment in attachments)
                {
                    var path = $"attachments/{attachment.Id:N}/{SafeFileName(attachment.OriginalFileName)}";
                    var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
                    await using var source = await fileStore.OpenReadAsync(attachment.StoredName, cancellationToken);
                    await using var target = entry.Open();
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        hash.AppendData(buffer, 0, read);
                    }
                    checksumLines.Add($"{Convert.ToHexString(hash.GetHashAndReset())}  {path}");
                }
            }
            var manifest = JsonSerializer.Serialize(new
            {
                exportedAt = DateTimeOffset.UtcNow,
                datasets,
                files = files.Select(file => new { file.FileName, sha256 = Convert.ToHexString(SHA256.HashData(file.Content)) }),
                attachments = attachments.Select(item => new { item.Id, item.OriginalFileName, path = $"attachments/{item.Id:N}/{SafeFileName(item.OriginalFileName)}" })
            });
            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Fastest);
            await using (var manifestStream = new StreamWriter(manifestEntry.Open()))
            {
                await manifestStream.WriteAsync(manifest);
            }
            var checksumEntry = archive.CreateEntry("checksums.sha256", CompressionLevel.Fastest);
            await using var checksumStream = new StreamWriter(checksumEntry.Open());
            await checksumStream.WriteAsync(string.Join(Environment.NewLine, checksumLines));
        }
        return new ExportFileResult($"数据交换_{DateTime.Now:yyyyMMddHHmmss}.zip", "application/zip", output.ToArray());
    }

    public async Task<IReadOnlyList<ExportTaskDto>> ListTasksAsync(string userId, CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequired(userId, nameof(userId));
        var tasks = (await db.DataExchangeTasks.AsNoTracking().Where(item => item.UserId == normalized && item.Direction == DataExchangeDirection.Export)
            .ToListAsync(cancellationToken)).OrderByDescending(item => item.CreatedAt).Take(100).ToArray();
        return tasks.Select(item => new ExportTaskDto(item.Id, item.UserId, JsonSerializer.Deserialize<ExportDataset[]>(item.DatasetsJson) ?? [], item.Scope, item.PackageFormat, item.IncludeAttachments, item.Status, item.RowCount, item.FileName, item.ErrorMessage, item.CreatedAt, item.CompletedAt)).ToArray();
    }

    public async Task<ExportSelectionDto?> GetLastSelectionAsync(string userId, ExportDataset dataset, CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        var template = await db.ExportTemplates.AsNoTracking().SingleOrDefaultAsync(item =>
            item.OwnerUserId == normalizedUserId && item.Dataset == dataset && item.IsLastSelection,
            cancellationToken);
        return template is null ? null : new ExportSelectionDto(dataset, DeserializeFields(template.SelectedFieldsJson), template.CutoffDate);
    }

    public async Task<ExportTemplateDto> SaveTemplateAsync(SaveExportTemplateRequest request, CancellationToken cancellationToken)
    {
        var owner = NormalizeRequired(request.OwnerUserId, nameof(request.OwnerUserId));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        ExportSelectionValidator.ValidateTemplate(request.Scope, owner, request.CanPublishShared);
        var fields = ExportSelectionValidator.ResolveFields(GetFieldCatalog(request.Dataset), request.SelectedFields);
        ExportSelectionValidator.ValidateCutoffDate(request.CutoffDate);
        if (await db.ExportTemplates.AnyAsync(item => item.OwnerUserId == owner && item.Dataset == request.Dataset && item.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"导出模板名称已存在：{name}");
        }

        var template = new ExportTemplate
        {
            OwnerUserId = owner,
            Name = name,
            Dataset = request.Dataset,
            Scope = request.Scope,
            SelectedFieldsJson = JsonSerializer.Serialize(fields.Select(item => item.Key)),
            CutoffDate = request.CutoffDate
        };
        db.ExportTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(template);
    }

    public async Task<IReadOnlyList<ExportTemplateDto>> ListTemplatesAsync(string userId, ExportDataset dataset, CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        var templates = await db.ExportTemplates.AsNoTracking()
            .Where(item => item.Dataset == dataset && !item.IsLastSelection && (item.Scope == ExportTemplateScope.Shared || item.OwnerUserId == normalizedUserId))
            .OrderBy(item => item.Scope)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return templates.Select(ToDto).ToArray();
    }

    private async Task<ExportFileResult> ExportProjectOverviewAsync(IReadOnlyList<ExportFieldDefinition> fields, DateOnly? cutoffDate, IReadOnlyList<Guid>? projectIds, CancellationToken cancellationToken)
    {
        var query = db.Projects.AsNoTracking()
            .Include(item => item.Contracts)
                .ThenInclude(item => item.LineItems)
            .Include(item => item.ResponsibleUser)
            .Include(item => item.Department)
            .Include(item => item.Branch)
            .Include(item => item.LegalEntities)
                .ThenInclude(item => item.LegalEntity)
            .Where(item => item.IsActive);
        if (projectIds is not null)
        {
            var allowedProjectIds = projectIds.Distinct().ToArray();
            query = query.Where(item => allowedProjectIds.Contains(item.Id));
        }
        var projects = await query.OrderBy(item => item.ProjectNumber).ToListAsync(cancellationToken);
        var rows = new List<IReadOnlyList<object?>>(projects.Count);
        decimal contractTotal = 0m;
        decimal receivableTotal = 0m;
        decimal collectedTotal = 0m;
        decimal uncollectedTotal = 0m;
        decimal payableTotal = 0m;
        decimal paidTotal = 0m;
        decimal unpaidTotal = 0m;
        decimal invoiceTotal = 0m;
        decimal uninvoicedTotal = 0m;
        for (var index = 0; index < projects.Count; index++)
        {
            var project = projects[index];
            var projectSummary = ProjectSummaryService.Calculate(project);
            var finance = await financeService.GetSummaryAsync(new FinanceSummaryFilter(project.Id, CutoffDate: cutoffDate), cancellationToken);
            var values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["serial_number"] = index + 1,
                ["project_number"] = project.ProjectNumber,
                ["project_name"] = project.Name,
                ["stage"] = project.Stage.ToString(),
                ["contract_signing_status"] = project.ContractSigningStatus.ToString(),
                ["affiliation_type"] = project.AffiliationType switch
                {
                    EngineeringManager.Domain.Projects.ProjectAffiliationType.ExternalPartyAttachedToUs => "他方挂靠我方",
                    EngineeringManager.Domain.Projects.ProjectAffiliationType.WeAttachedToExternalParty => "我方挂靠他方",
                    _ => "自营项目"
                },
                ["parent_project"] = project.ParentProjectName,
                ["general_contractor"] = project.GeneralContractorName,
                ["general_contractor_contact"] = project.GeneralContractorContact,
                ["general_contractor_phone"] = project.GeneralContractorPhone,
                ["responsible_user"] = project.ResponsibleUser?.DisplayName,
                ["department"] = project.Department?.Name,
                ["branch"] = project.Branch?.Name,
                ["legal_entities"] = string.Join("、", project.LegalEntities.OrderByDescending(item => item.IsPrimary).Select(item => item.LegalEntity.ShortName)),
                ["actual_start_date"] = project.ActualStartDate,
                ["actual_completion_date"] = project.ActualCompletionDate,
                ["contract_amount"] = projectSummary.ContractAmount,
                ["estimated_amount"] = projectSummary.EstimatedAmount,
                ["settled_amount"] = projectSummary.SettledAmount,
                ["current_project_amount"] = projectSummary.CurrentAmount,
                ["settlement_status"] = projectSummary.SettlementStatus.ToString(),
                ["contract_count"] = projectSummary.ContractCount,
                ["line_item_count"] = projectSummary.LineItemCount,
                ["receivable_amount"] = finance.ReceivableAmount,
                ["collected_amount"] = finance.CollectedAmount,
                ["uncollected_amount"] = finance.UncollectedAmount,
                ["payable_amount"] = finance.PayableAmount,
                ["paid_amount"] = finance.PaidAmount,
                ["unpaid_amount"] = finance.UnpaidAmount,
                ["expected_invoice_amount"] = finance.ReceivableAmount,
                ["output_invoice_amount"] = finance.OutputInvoiceAmount,
                ["uninvoiced_amount"] = finance.UninvoicedAmount,
                ["notes"] = project.Notes
            };
            rows.Add(fields.Select(field => values[field.Key]).ToArray());
            contractTotal += projectSummary.ContractAmount;
            receivableTotal += finance.ReceivableAmount;
            collectedTotal += finance.CollectedAmount;
            uncollectedTotal += finance.UncollectedAmount;
            payableTotal += finance.PayableAmount;
            paidTotal += finance.PaidAmount;
            unpaidTotal += finance.UnpaidAmount;
            invoiceTotal += finance.OutputInvoiceAmount;
            uninvoicedTotal += finance.UninvoicedAmount;
        }

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "总览汇总",
            ["指标", "数值"],
            [
                ["项目数量", projects.Count],
                ["合同总额", contractTotal],
                ["应收款", receivableTotal],
                ["已收款", collectedTotal],
                ["未收款", uncollectedTotal],
                ["应付款", payableTotal],
                ["已付款", paidTotal],
                ["未付款", unpaidTotal],
                ["应开票", receivableTotal],
                ["已开票", invoiceTotal],
                ["未开票", uninvoicedTotal],
                ["统计截止日", cutoffDate]
            ]);
        workbook.AddWorksheet("项目明细", fields.Select(item => item.Label).ToArray(), rows);
        return new ExportFileResult($"项目经营总览_{DateTime.Now:yyyyMMddHHmmss}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", workbook.ToArray());
    }

    private async Task<ExportFileResult> ExportEmployeesAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken cancellationToken)
    {
        var employees = await db.Employees.AsNoTracking().OrderBy(item => item.EmployeeNumber).ToListAsync(cancellationToken);
        return CreateSingleSheet("员工", fields, employees.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["employee_number"] = item.EmployeeNumber,
            ["name"] = item.Name,
            ["employee_type"] = EmployeeTypeLabel(item.EmployeeType),
            ["position"] = item.PositionTitle,
            ["phone"] = item.Phone,
            ["identity_number"] = item.IdentityNumber,
            ["bank_account_number"] = item.BankAccountNumber,
            ["bank_name"] = item.BankName,
            ["default_monthly_salary"] = item.DefaultMonthlySalary,
            ["default_daily_rate"] = item.DefaultDailyRate,
            ["default_hourly_rate"] = item.DefaultHourlyRate,
            ["default_piecework_rate"] = item.DefaultPieceworkRate,
            ["_system_id"] = item.Id.ToString(),
            ["_concurrency_stamp"] = item.ConcurrencyStamp.ToString(),
            ["is_active"] = item.IsActive,
            ["notes"] = item.Notes
        })), "员工台账");
    }

    private async Task<ExportFileResult> ExportProjectsAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken cancellationToken)
    {
        var projects = await db.Projects.AsNoTracking().OrderBy(item => item.ProjectNumber).ToListAsync(cancellationToken);
        return CreateSingleSheet("项目", fields, projects.Select(item => Project(fields, new(StringComparer.Ordinal)
        {
            ["project_number"] = item.ProjectNumber, ["project_name"] = item.Name, ["stage"] = item.Stage.ToString(), ["contract_signing_status"] = item.ContractSigningStatus.ToString(), ["affiliation_type"] = item.AffiliationType.ToString(), ["general_contractor"] = item.GeneralContractorName, ["actual_start_date"] = item.ActualStartDate, ["actual_completion_date"] = item.ActualCompletionDate, ["is_active"] = item.IsActive, ["notes"] = item.Notes, ["_system_id"] = item.Id.ToString(), ["_concurrency_stamp"] = item.ConcurrencyStamp.ToString()
        })), "项目台账");
    }

    private async Task<ExportFileResult> ExportContractsAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken cancellationToken)
    {
        var contracts = await db.Contracts.AsNoTracking().Include(item => item.Project).OrderBy(item => item.Project.ProjectNumber).ThenBy(item => item.ContractNumber).ToListAsync(cancellationToken);
        return CreateSingleSheet("合同", fields, contracts.Select(item => Project(fields, new(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber, ["contract_number"] = item.ContractNumber, ["name"] = item.Name, ["contract_type"] = item.ContractType.ToString(), ["counterparty_name"] = item.CounterpartyName, ["signed_date"] = item.SignedDate, ["total_amount"] = item.TotalAmount, ["is_active"] = item.IsActive, ["notes"] = item.Notes
        })), "合同台账");
    }

    private async Task<ExportFileResult> ExportStageResultsAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken cancellationToken)
    {
        var results = await db.StageResults.AsNoTracking().Include(item => item.Project).OrderBy(item => item.Project.ProjectNumber).ThenBy(item => item.ResultDate).ToListAsync(cancellationToken);
        return CreateSingleSheet("阶段成果", fields, results.Select(item => Project(fields, new(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber, ["title"] = item.Title, ["result_type"] = item.ResultType.ToString(), ["status"] = item.Status.ToString(), ["result_date"] = item.ResultDate, ["quality_result"] = item.QualityResult.ToString(), ["description"] = item.Description
        })), "阶段成果");
    }

    private static string EmployeeTypeLabel(EmployeeType employeeType) => employeeType switch
    {
        EmployeeType.Formal => "正式员工",
        EmployeeType.Labor => "劳务员工",
        EmployeeType.Temporary => "特殊临时人员",
        _ => employeeType.ToString()
    };

    private async Task<ExportFileResult> ExportPartnersAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken cancellationToken)
    {
        var partners = await db.BusinessPartners.AsNoTracking().Include(item => item.Roles).OrderBy(item => item.PartnerNumber).ToListAsync(cancellationToken);
        return CreateSingleSheet("合作单位", fields, partners.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["partner_number"] = item.PartnerNumber,
            ["name"] = item.Name,
            ["short_name"] = item.ShortName,
            ["roles"] = string.Join("、", item.Roles.Select(role => role.RoleType.ToString())),
            ["is_active"] = item.IsActive,
            ["notes"] = item.Notes
        })), "合作单位台账");
    }

    private async Task<ExportFileResult> ExportPayrollAsync(IReadOnlyList<ExportFieldDefinition> fields, DateOnly? cutoffDate, CancellationToken cancellationToken)
    {
        var query = db.PayrollBatches.AsNoTracking()
            .Include(item => item.Items)
            .Include(item => item.Payments)
            .Include(item => item.Project)
            .Include(item => item.LegalEntity)
            .Include(item => item.Account)
            .AsQueryable();
        if (cutoffDate.HasValue)
        {
            query = query.Where(item => (item.IsUnifiedDisbursement ? item.PaymentDate : item.EndDate) <= cutoffDate);
        }

        var batches = await query.OrderByDescending(item => item.PaymentDate ?? item.EndDate).ToListAsync(cancellationToken);
        var rows = new List<IReadOnlyList<object?>>();
        foreach (var batch in batches)
        {
            var summary = PayrollCalculator.Calculate(batch.Items.Select(item => new PayrollComponentInput(item.Nature, item.Amount)), batch.Payments.Sum(item => item.Amount));
            if (batch.IsUnifiedDisbursement)
            {
                foreach (var payment in batch.Payments.OrderBy(item => item.RecipientType).ThenBy(item => item.RecipientNameSnapshot ?? item.PayeeName))
                {
                    rows.Add(Project(fields, PayrollValues(batch, summary, payment)));
                }
            }
            else
            {
                rows.Add(Project(fields, PayrollValues(batch, summary, null)));
            }
        }
        return CreateSingleSheet("工资", fields, rows, "工资台账");
    }

    private static Dictionary<string, object?> PayrollValues(PayrollBatch batch, PayrollSummary summary, PayrollPayment? payment) =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["batch_number"] = batch.BatchNumber,
            ["batch_name"] = batch.Name,
            ["batch_type"] = batch.BatchType.ToString(),
            ["start_date"] = batch.StartDate,
            ["end_date"] = batch.EndDate,
            ["payment_date"] = batch.PaymentDate ?? payment?.PaymentDate,
            ["project"] = batch.Project?.Name,
            ["legal_entity"] = batch.LegalEntity?.ShortName,
            ["account"] = batch.Account?.AccountName ?? payment?.Account?.AccountName,
            ["recipient_type"] = payment?.RecipientType.ToString(),
            ["recipient_name"] = payment?.RecipientNameSnapshot ?? payment?.PayeeName,
            ["crew"] = payment?.CrewNameSnapshot,
            ["amount"] = payment?.Amount,
            ["actual_amount"] = batch.IsUnifiedDisbursement ? batch.ActualAmount : summary.PaidAmount,
            ["payable_amount"] = summary.PayableAmount,
            ["paid_amount"] = summary.PaidAmount,
            ["unpaid_amount"] = summary.UnpaidAmount,
            ["notes"] = payment?.Notes ?? batch.Notes
        };

    private async Task<ExportFileResult> ExportCollectionsAsync(IReadOnlyList<ExportFieldDefinition> fields, DateOnly? cutoffDate, CancellationToken cancellationToken)
    {
        var query = db.FinanceCashAllocations.AsNoTracking()
            .Include(item => item.Project)
            .Include(item => item.CashEntry).ThenInclude(item => item.LegalEntity)
            .Include(item => item.CashEntry).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.CashEntry).ThenInclude(item => item.Account)
            .Where(item => item.CashEntry.Direction == LedgerDirection.Receivable && item.CashEntry.CashType == LedgerCashType.Collection &&
                !item.CashEntry.IsReversal && item.CashEntry.Status == LedgerRecordStatus.Active);
        if (cutoffDate.HasValue) query = query.Where(item => item.CashEntry.BusinessDate <= cutoffDate);
        var entries = await query.OrderByDescending(item => item.CashEntry.BusinessDate).ThenBy(item => item.AllocationOrder).ToListAsync(cancellationToken);
        return CreateSingleSheet("收款", fields, entries.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project?.ProjectNumber,
            ["collection_date"] = item.CashEntry.BusinessDate,
            ["legal_entity"] = item.CashEntry.LegalEntity.ShortName,
            ["partner"] = item.CashEntry.BusinessPartner?.ShortName,
            ["account"] = item.CashEntry.Account?.AccountName,
            ["amount"] = item.Amount,
            ["payment_method"] = item.CashEntry.PaymentMethod,
            ["notes"] = item.CashEntry.Notes
        })), "收款台账");
    }

    private async Task<ExportFileResult> ExportPaymentsAsync(IReadOnlyList<ExportFieldDefinition> fields, DateOnly? cutoffDate, CancellationToken cancellationToken)
    {
        var query = db.FinanceCashAllocations.AsNoTracking()
            .Include(item => item.Project)
            .Include(item => item.CashEntry).ThenInclude(item => item.LegalEntity)
            .Include(item => item.CashEntry).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.CashEntry).ThenInclude(item => item.Account)
            .Where(item => item.CashEntry.Direction == LedgerDirection.Payable && item.CashEntry.CashType == LedgerCashType.Payment &&
                !item.CashEntry.IsReversal && item.CashEntry.Status == LedgerRecordStatus.Active);
        if (cutoffDate.HasValue) query = query.Where(item => item.CashEntry.BusinessDate <= cutoffDate);
        var entries = await query.OrderByDescending(item => item.CashEntry.BusinessDate).ThenBy(item => item.AllocationOrder).ToListAsync(cancellationToken);
        return CreateSingleSheet("付款", fields, entries.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project?.ProjectNumber,
            ["payment_date"] = item.CashEntry.BusinessDate,
            ["legal_entity"] = item.CashEntry.LegalEntity.ShortName,
            ["partner"] = item.CashEntry.BusinessPartner?.ShortName,
            ["account"] = item.CashEntry.Account?.AccountName,
            ["amount"] = item.Amount,
            ["payment_method"] = item.CashEntry.PaymentMethod,
            ["notes"] = item.CashEntry.Notes
        })), "付款台账");
    }

    private async Task<ExportFileResult> ExportInvoicesAsync(IReadOnlyList<ExportFieldDefinition> fields, DateOnly? cutoffDate, CancellationToken cancellationToken)
    {
        var query = db.InvoiceEntries.AsNoTracking().Include(item => item.Project).Include(item => item.LegalEntity).AsQueryable();
        if (cutoffDate.HasValue) query = query.Where(item => item.InvoiceDate <= cutoffDate);
        var entries = await query.OrderByDescending(item => item.InvoiceDate).ToListAsync(cancellationToken);
        return CreateSingleSheet("发票", fields, entries.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber,
            ["invoice_number"] = item.InvoiceNumber,
            ["invoice_date"] = item.InvoiceDate,
            ["direction"] = item.Direction.ToString(),
            ["legal_entity"] = item.LegalEntity.ShortName,
            ["gross_amount"] = item.GrossAmount,
            ["status"] = item.Status.ToString()
        })), "发票台账");
    }

    private async Task<ExportFileResult> ExportAccountsAsync(IReadOnlyList<ExportFieldDefinition> fields, DateOnly? cutoffDate, CancellationToken cancellationToken)
    {
        var accounts = await db.FinancialAccounts.AsNoTracking().Include(item => item.LegalEntity).OrderBy(item => item.AccountName).ToListAsync(cancellationToken);
        var accountIds = accounts.Select(item => item.Id).ToArray();
        var transactionQuery = db.AccountTransactions.AsNoTracking().Where(item => accountIds.Contains(item.AccountId));
        if (cutoffDate.HasValue) transactionQuery = transactionQuery.Where(item => item.TransactionDate <= cutoffDate);
        var transactions = await transactionQuery.ToListAsync(cancellationToken);
        return CreateSingleSheet("资金账户", fields, accounts.Select(item =>
        {
            var inflow = transactions.Where(tx => tx.AccountId == item.Id && tx.Direction == AccountTransactionDirection.Inflow).Sum(tx => tx.Amount);
            var outflow = transactions.Where(tx => tx.AccountId == item.Id && tx.Direction == AccountTransactionDirection.Outflow).Sum(tx => tx.Amount);
            return Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["legal_entity"] = item.LegalEntity.ShortName,
                ["account_name"] = item.AccountName,
                ["account_type"] = item.AccountType.ToString(),
                ["opening_balance"] = item.OpeningBalance,
                ["current_balance"] = item.OpeningBalance + inflow - outflow,
                ["is_active"] = item.IsActive,
                ["notes"] = item.Notes
            });
        }), "资金账户台账");
    }

    private async Task<ExportFileResult> ExportCompaniesAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken cancellationToken)
    {
        var companies = await db.LegalEntities.AsNoTracking().Include(item => item.CompanyCategory).OrderBy(item => item.Code).ToListAsync(cancellationToken);
        return CreateSingleSheet("自有公司", fields, companies.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["company_code"] = item.Code,
            ["name"] = item.Name,
            ["short_name"] = item.ShortName,
            ["category"] = item.CompanyCategory?.Name,
            ["legal_representative"] = item.LegalRepresentative,
            ["tax_code"] = item.UnifiedSocialCreditCode,
            ["phone"] = item.Phone,
            ["registered_address"] = item.RegisteredAddress,
            ["business_address"] = item.BusinessAddress,
            ["invoice_title"] = item.InvoiceTitle,
            ["is_active"] = item.IsActive,
            ["notes"] = item.Notes
        })), "自有公司");
    }

    private async Task<ExportFileResult> ExportCompanyAccountsAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken cancellationToken)
    {
        var accounts = await db.FinancialAccounts.AsNoTracking().Include(item => item.LegalEntity).OrderBy(item => item.LegalEntity.Code).ThenBy(item => item.AccountName).ToListAsync(cancellationToken);
        return CreateSingleSheet("公司账户", fields, accounts.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["company_code"] = item.LegalEntity.Code,
            ["account_name"] = item.AccountName,
            ["account_type"] = item.AccountType.ToString(),
            ["account_number"] = item.AccountNumber,
            ["bank_name"] = item.BankName,
            ["opening_balance"] = item.OpeningBalance,
            ["default_collection"] = item.IsDefaultCollection,
            ["default_payment"] = item.IsDefaultPayment,
            ["default_invoice"] = item.IsDefaultInvoice,
            ["is_active"] = item.IsActive,
            ["notes"] = item.Notes
        })), "公司账户");
    }

    private async Task<ExportFileResult> ExportCompanyCertificatesAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken cancellationToken)
    {
        var certificates = await db.CompanyCertificates.AsNoTracking().Include(item => item.LegalEntity)
            .Where(item => !item.IsDeleted).OrderBy(item => item.LegalEntity.Code).ThenBy(item => item.CertificateType).ToListAsync(cancellationToken);
        return CreateSingleSheet("公司证照", fields, certificates.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["company_code"] = item.LegalEntity.Code,
            ["certificate_type"] = item.CertificateType,
            ["certificate_number"] = item.CertificateNumber,
            ["specialty_level_scope"] = item.SpecialtyLevelScope,
            ["issuing_authority"] = item.IssuingAuthority,
            ["issued_on"] = item.IssuedOn,
            ["expires_on"] = item.ExpiresOn,
            ["notes"] = item.Notes
        })), "公司证照");
    }

    private async Task<ExportFileResult> ExportEmployeeCertificatesAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken cancellationToken)
    {
        var certificates = await db.EmployeeCertificates.AsNoTracking().Include(item => item.Employee)
            .Where(item => !item.IsDeleted).OrderBy(item => item.Employee.EmployeeNumber).ThenBy(item => item.CertificateType).ToListAsync(cancellationToken);
        return CreateSingleSheet("员工证书", fields, certificates.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["employee_number"] = item.Employee.EmployeeNumber,
            ["employee_name"] = item.Employee.Name,
            ["certificate_type"] = item.CertificateType,
            ["certificate_number"] = item.CertificateNumber,
            ["specialty_level_scope"] = item.SpecialtyLevelScope,
            ["issuing_authority"] = item.IssuingAuthority,
            ["issued_on"] = item.IssuedOn,
            ["expires_on"] = item.ExpiresOn,
            ["notes"] = item.Notes
        })), "员工证书");
    }

    private async Task<ExportFileResult> ExportEquipmentAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken token)
    {
        var items = await db.Equipment.AsNoTracking().Include(item => item.OwnerLegalEntity).Include(item => item.LessorBusinessPartner).OrderBy(item => item.EquipmentNumber).ToListAsync(token);
        return CreateSingleSheet("设备档案", fields, items.Select(item => Project(fields, new(StringComparer.Ordinal) { ["equipment_number"] = item.EquipmentNumber, ["name"] = item.Name, ["model"] = item.Model, ["category"] = item.Category, ["ownership"] = item.OwnershipType.ToString(), ["owner_company"] = item.OwnerLegalEntity?.Name, ["lessor"] = item.LessorBusinessPartner?.Name, ["status"] = item.Status.ToString(), ["internal_daily_rate"] = item.InternalDailyRate, ["notes"] = item.Notes })), "设备档案");
    }

    private async Task<ExportFileResult> ExportEquipmentLeasesAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken token)
    {
        var items = await db.EquipmentLeaseAgreements.AsNoTracking().Include(item => item.Equipment).Include(item => item.LessorBusinessPartner).OrderBy(item => item.Equipment.EquipmentNumber).ToListAsync(token);
        return CreateSingleSheet("租赁约定", fields, items.Select(item => Project(fields, new(StringComparer.Ordinal) { ["equipment_number"] = item.Equipment.EquipmentNumber, ["contract_number"] = item.ContractNumber, ["lessor"] = item.LessorBusinessPartner.Name, ["start_date"] = item.StartDate, ["end_date"] = item.EndDate, ["rent_mode"] = item.RentMode.ToString(), ["unit_rate"] = item.UnitRate })), "设备租赁");
    }

    private async Task<ExportFileResult> ExportEquipmentUsagesAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken token)
    {
        var items = await db.EquipmentProjectUsages.AsNoTracking().Include(item => item.Equipment).Include(item => item.Project).Include(item => item.LegalEntity).OrderBy(item => item.EntryDate).ToListAsync(token);
        return CreateSingleSheet("设备使用", fields, items.Select(item => Project(fields, new(StringComparer.Ordinal) { ["equipment_number"] = item.Equipment.EquipmentNumber, ["project_number"] = item.Project.ProjectNumber, ["company"] = item.LegalEntity.Name, ["entry_date"] = item.EntryDate, ["exit_date"] = item.ExitDate, ["rent_mode"] = item.RentMode.ToString(), ["unit_rate"] = item.UnitRate })), "设备使用");
    }

    private async Task<ExportFileResult> ExportEquipmentPeriodsAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken token)
    {
        var items = await db.EquipmentWorkPeriods.AsNoTracking().Include(item => item.Usage).ThenInclude(item => item.Equipment).Include(item => item.Usage).ThenInclude(item => item.Project).OrderBy(item => item.StartDate).ToListAsync(token);
        return CreateSingleSheet("设备日期段", fields, items.Select(item => Project(fields, new(StringComparer.Ordinal) { ["equipment_number"] = item.Usage.Equipment.EquipmentNumber, ["project_number"] = item.Usage.Project.ProjectNumber, ["start_date"] = item.StartDate, ["end_date"] = item.EndDate, ["period_type"] = item.PeriodType.ToString(), ["chargeable"] = item.IsChargeable })), "设备日期段");
    }

    private async Task<ExportFileResult> ExportEquipmentSettlementsAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken token)
    {
        var items = await db.EquipmentSettlements.AsNoTracking().Include(item => item.Usage).ThenInclude(item => item.Equipment).Include(item => item.Usage).ThenInclude(item => item.Project).OrderBy(item => item.SettlementDate).ToListAsync(token);
        return CreateSingleSheet("设备结算", fields, items.Select(item => Project(fields, new(StringComparer.Ordinal) { ["equipment_number"] = item.Usage.Equipment.EquipmentNumber, ["project_number"] = item.Usage.Project.ProjectNumber, ["settlement_date"] = item.SettlementDate, ["base_amount"] = item.BaseAmount, ["total_amount"] = item.TotalAmount, ["offset_amount"] = item.OffsetAmount, ["payable_id"] = item.PayableEntryId?.ToString(), ["notes"] = item.Notes })), "设备结算");
    }

    private static object?[] Project(IReadOnlyList<ExportFieldDefinition> fields, Dictionary<string, object?> values) =>
        fields.Select(field => field.IsSensitive ? MaskSensitive(values[field.Key]) : values[field.Key]).ToArray();

    private static string MaskSensitive(object? value) => value switch
    {
        null => string.Empty,
        string text when text.Length <= 4 => "******",
        string text => $"******{text[^4..]}",
        _ => "已脱敏"
    };

    private static string SafeWorksheetName(string name, HashSet<string> used)
    {
        var candidate = new string(name.Where(character => !"[]:*?/\\".Contains(character)).ToArray());
        candidate = string.IsNullOrWhiteSpace(candidate) ? "数据" : candidate[..Math.Min(candidate.Length, 31)];
        var suffix = 2;
        var unique = candidate;
        while (!used.Add(unique))
        {
            var tail = $"-{suffix++}";
            unique = candidate[..Math.Min(candidate.Length, 31 - tail.Length)] + tail;
        }
        return unique;
    }

    private static string DatasetSheetName(ExportDataset dataset) => dataset switch
    {
        ExportDataset.ProjectOverview => "总览汇总",
        ExportDataset.Projects => "项目",
        ExportDataset.Contracts => "合同",
        ExportDataset.StageResults => "阶段成果",
        ExportDataset.Employees => "员工",
        ExportDataset.EmployeeCertificates => "员工证书",
        ExportDataset.Partners => "合作单位",
        ExportDataset.Payroll => "工资",
        ExportDataset.Collections => "收款",
        ExportDataset.Payments => "付款",
        ExportDataset.Invoices => "发票",
        ExportDataset.Accounts => "资金账户",
        ExportDataset.Companies => "自有公司",
        ExportDataset.CompanyAccounts => "公司账户",
        ExportDataset.CompanyCertificates => "公司证照",
        ExportDataset.Equipment => "设备档案",
        ExportDataset.EquipmentLeases => "租赁约定",
        ExportDataset.EquipmentUsages => "设备使用",
        ExportDataset.EquipmentPeriods => "设备日期段",
        ExportDataset.EquipmentSettlements => "设备结算",
        _ => dataset.ToString()
    };

    private static string SafeFileName(string fileName)
    {
        var safe = new string(Path.GetFileName(fileName).Where(character => !Path.GetInvalidFileNameChars().Contains(character)).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "attachment.bin" : safe;
    }

    private static ExportFileResult CreateSingleSheet(string sheetName, IReadOnlyList<ExportFieldDefinition> fields, IEnumerable<IReadOnlyList<object?>> rows, string filePrefix)
    {
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(sheetName, fields.Select(item => item.Label).ToArray(), rows);
        return new ExportFileResult($"{filePrefix}_{DateTime.Now:yyyyMMddHHmmss}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", workbook.ToArray());
    }

    private async Task SaveLastSelectionAsync(string userId, ExportDataset dataset, IReadOnlyList<string> fields, DateOnly? cutoffDate, CancellationToken cancellationToken)
    {
        var template = await db.ExportTemplates.SingleOrDefaultAsync(item => item.OwnerUserId == userId && item.Dataset == dataset && item.IsLastSelection, cancellationToken);
        if (template is null)
        {
            template = new ExportTemplate { OwnerUserId = userId, Name = LastSelectionName, Dataset = dataset, Scope = ExportTemplateScope.Personal, IsLastSelection = true };
            db.ExportTemplates.Add(template);
        }

        template.SelectedFieldsJson = JsonSerializer.Serialize(fields);
        template.CutoffDate = cutoffDate;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        template.ConcurrencyStamp = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ExportTemplateDto ToDto(ExportTemplate template) =>
        new(template.Id, template.OwnerUserId, template.Name, template.Dataset, template.Scope, DeserializeFields(template.SelectedFieldsJson), template.CutoffDate);

    private static string[] DeserializeFields(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空。", parameterName);
        }

        return value.Trim();
    }
}
