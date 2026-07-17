using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Projects;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed class ExportService(ApplicationDbContext db, IFinanceLedgerService financeService) : IExportService
{
    private const string LastSelectionName = "__last_selection__";
    private static readonly Dictionary<ExportDataset, IReadOnlyList<ExportFieldDefinition>> Catalogs =
        new Dictionary<ExportDataset, IReadOnlyList<ExportFieldDefinition>>
        {
            [ExportDataset.ProjectOverview] =
            [
                new("project_number", "项目编号", ExportFieldDataType.Text, true),
                new("project_name", "项目名称", ExportFieldDataType.Text, true),
                new("stage", "项目阶段", ExportFieldDataType.Text, true),
                new("affiliation_type", "项目合作方式", ExportFieldDataType.Text, true),
                new("general_contractor", "总包单位", ExportFieldDataType.Text, true),
                new("contract_amount", "合同金额", ExportFieldDataType.Number, true),
                new("current_project_amount", "当前工程金额", ExportFieldDataType.Number, true),
                new("receivable_amount", "应收款", ExportFieldDataType.Number, true),
                new("collected_amount", "已收款", ExportFieldDataType.Number, true),
                new("uncollected_amount", "未收款", ExportFieldDataType.Number, true),
                new("payable_amount", "应付款", ExportFieldDataType.Number, true),
                new("paid_amount", "已付款", ExportFieldDataType.Number, true),
                new("unpaid_amount", "未付款", ExportFieldDataType.Number, true),
                new("expected_invoice_amount", "应开票", ExportFieldDataType.Number, true),
                new("output_invoice_amount", "已开票", ExportFieldDataType.Number, true),
                new("uninvoiced_amount", "未开票", ExportFieldDataType.Number, true)
            ],
            [ExportDataset.Employees] =
            [
                new("employee_number", "员工编号", ExportFieldDataType.Text, true),
                new("name", "姓名", ExportFieldDataType.Text, true),
                new("employee_type", "员工类型", ExportFieldDataType.Text, true),
                new("position", "岗位", ExportFieldDataType.Text, true),
                new("phone", "电话", ExportFieldDataType.Text, false),
                new("is_active", "状态", ExportFieldDataType.Boolean, true)
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
                new("is_active", "状态", ExportFieldDataType.Boolean, true)
            ],
            [ExportDataset.Payroll] =
            [
                new("batch_number", "批次编号", ExportFieldDataType.Text, true),
                new("batch_name", "批次名称", ExportFieldDataType.Text, true),
                new("batch_type", "批次类型", ExportFieldDataType.Text, true),
                new("start_date", "开始日期", ExportFieldDataType.Date, true),
                new("end_date", "结束日期", ExportFieldDataType.Date, true),
                new("payable_amount", "应发工资", ExportFieldDataType.Number, true),
                new("paid_amount", "已发工资", ExportFieldDataType.Number, true),
                new("unpaid_amount", "未发工资", ExportFieldDataType.Number, true)
            ],
            [ExportDataset.Collections] =
            [
                new("project_number", "项目编号", ExportFieldDataType.Text, true),
                new("collection_date", "收款日期", ExportFieldDataType.Date, true),
                new("legal_entity", "签约公司", ExportFieldDataType.Text, true),
                new("partner", "合作单位", ExportFieldDataType.Text, false),
                new("account", "收款账户", ExportFieldDataType.Text, true),
                new("amount", "收款金额", ExportFieldDataType.Number, true),
                new("payment_method", "收款方式", ExportFieldDataType.Text, true)
            ],
            [ExportDataset.Payments] =
            [
                new("project_number", "项目编号", ExportFieldDataType.Text, true),
                new("payment_date", "付款日期", ExportFieldDataType.Date, true),
                new("legal_entity", "签约公司", ExportFieldDataType.Text, true),
                new("partner", "合作单位", ExportFieldDataType.Text, true),
                new("account", "付款账户", ExportFieldDataType.Text, true),
                new("amount", "付款金额", ExportFieldDataType.Number, true),
                new("payment_method", "付款方式", ExportFieldDataType.Text, true)
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
                new("is_active", "状态", ExportFieldDataType.Boolean, true)
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
                new("is_active", "状态", ExportFieldDataType.Boolean, true)
            ],
            [ExportDataset.CompanyAccounts] =
            [
                new("company_code", "公司编码", ExportFieldDataType.Text, true),
                new("account_name", "账户名称", ExportFieldDataType.Text, true),
                new("account_type", "账户类型", ExportFieldDataType.Text, true),
                new("account_number", "账号", ExportFieldDataType.Text, false),
                new("bank_name", "开户行", ExportFieldDataType.Text, false),
                new("opening_balance", "期初余额", ExportFieldDataType.Number, true),
                new("default_collection", "默认收款", ExportFieldDataType.Boolean, true),
                new("default_payment", "默认付款", ExportFieldDataType.Boolean, true),
                new("default_invoice", "默认开票", ExportFieldDataType.Boolean, true),
                new("is_active", "状态", ExportFieldDataType.Boolean, true)
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
                new("internal_daily_rate", "内部参考日价", ExportFieldDataType.Number, false)
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
                new("equipment_number", "设备编号", ExportFieldDataType.Text, true), new("project_number", "项目编号", ExportFieldDataType.Text, true), new("settlement_date", "结算日期", ExportFieldDataType.Date, true), new("base_amount", "基础租金", ExportFieldDataType.Number, true), new("total_amount", "结算总额", ExportFieldDataType.Number, true), new("offset_amount", "抵扣金额", ExportFieldDataType.Number, true), new("payable_id", "应付记录", ExportFieldDataType.Text, false)
            ]
        };

    public IReadOnlyList<ExportFieldDefinition> GetFieldCatalog(ExportDataset dataset) =>
        Catalogs.TryGetValue(dataset, out var fields) ? fields : throw new NotSupportedException($"暂不支持导出数据集：{dataset}");

    public async Task<ExportFileResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        var userId = NormalizeRequired(request.UserId, nameof(request.UserId));
        var fields = ExportSelectionValidator.ResolveFields(GetFieldCatalog(request.Dataset), request.SelectedFields);
        ExportSelectionValidator.ValidateCutoffDate(request.CutoffDate);
        var file = request.Dataset switch
        {
            ExportDataset.ProjectOverview => await ExportProjectOverviewAsync(fields, request.CutoffDate, request.ProjectIds, cancellationToken),
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
        await SaveLastSelectionAsync(userId, request.Dataset, fields.Select(item => item.Key).ToArray(), request.CutoffDate, cancellationToken);
        return file;
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
        foreach (var project in projects)
        {
            var projectSummary = ProjectSummaryService.Calculate(project);
            var finance = await financeService.GetSummaryAsync(new FinanceSummaryFilter(project.Id, CutoffDate: cutoffDate), cancellationToken);
            var values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["project_number"] = project.ProjectNumber,
                ["project_name"] = project.Name,
                ["stage"] = project.Stage.ToString(),
                ["affiliation_type"] = project.AffiliationType switch
                {
                    EngineeringManager.Domain.Projects.ProjectAffiliationType.ExternalPartyAttachedToUs => "他方挂靠我方",
                    EngineeringManager.Domain.Projects.ProjectAffiliationType.WeAttachedToExternalParty => "我方挂靠他方",
                    _ => "自营项目"
                },
                ["general_contractor"] = project.GeneralContractorName,
                ["contract_amount"] = projectSummary.ContractAmount,
                ["current_project_amount"] = projectSummary.CurrentAmount,
                ["receivable_amount"] = finance.ReceivableAmount,
                ["collected_amount"] = finance.CollectedAmount,
                ["uncollected_amount"] = finance.UncollectedAmount,
                ["payable_amount"] = finance.PayableAmount,
                ["paid_amount"] = finance.PaidAmount,
                ["unpaid_amount"] = finance.UnpaidAmount,
                ["expected_invoice_amount"] = finance.ReceivableAmount,
                ["output_invoice_amount"] = finance.OutputInvoiceAmount,
                ["uninvoiced_amount"] = finance.UninvoicedAmount
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
            ["employee_type"] = item.EmployeeType.ToString(),
            ["position"] = item.PositionTitle,
            ["phone"] = item.Phone,
            ["is_active"] = item.IsActive
        })), "员工台账");
    }

    private async Task<ExportFileResult> ExportPartnersAsync(IReadOnlyList<ExportFieldDefinition> fields, CancellationToken cancellationToken)
    {
        var partners = await db.BusinessPartners.AsNoTracking().Include(item => item.Roles).OrderBy(item => item.PartnerNumber).ToListAsync(cancellationToken);
        return CreateSingleSheet("合作单位", fields, partners.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["partner_number"] = item.PartnerNumber,
            ["name"] = item.Name,
            ["short_name"] = item.ShortName,
            ["roles"] = string.Join("、", item.Roles.Select(role => role.RoleType.ToString())),
            ["is_active"] = item.IsActive
        })), "合作单位台账");
    }

    private async Task<ExportFileResult> ExportPayrollAsync(IReadOnlyList<ExportFieldDefinition> fields, DateOnly? cutoffDate, CancellationToken cancellationToken)
    {
        var query = db.PayrollBatches.AsNoTracking().Include(item => item.Items).Include(item => item.Payments).AsQueryable();
        if (cutoffDate.HasValue)
        {
            query = query.Where(item => item.EndDate <= cutoffDate);
        }

        var batches = await query.OrderByDescending(item => item.EndDate).ToListAsync(cancellationToken);
        return CreateSingleSheet("工资", fields, batches.Select(batch =>
        {
            var summary = PayrollCalculator.Calculate(batch.Items.Select(item => new PayrollComponentInput(item.Nature, item.Amount)), batch.Payments.Sum(item => item.Amount));
            return Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["batch_number"] = batch.BatchNumber,
                ["batch_name"] = batch.Name,
                ["batch_type"] = batch.BatchType.ToString(),
                ["start_date"] = batch.StartDate,
                ["end_date"] = batch.EndDate,
                ["payable_amount"] = summary.PayableAmount,
                ["paid_amount"] = summary.PaidAmount,
                ["unpaid_amount"] = summary.UnpaidAmount
            });
        }), "工资台账");
    }

    private async Task<ExportFileResult> ExportCollectionsAsync(IReadOnlyList<ExportFieldDefinition> fields, DateOnly? cutoffDate, CancellationToken cancellationToken)
    {
        var query = db.CollectionEntries.AsNoTracking().Include(item => item.Project).Include(item => item.LegalEntity).Include(item => item.BusinessPartner).Include(item => item.Account).AsQueryable();
        if (cutoffDate.HasValue) query = query.Where(item => item.CollectionDate <= cutoffDate);
        var entries = await query.OrderByDescending(item => item.CollectionDate).ToListAsync(cancellationToken);
        return CreateSingleSheet("收款", fields, entries.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber,
            ["collection_date"] = item.CollectionDate,
            ["legal_entity"] = item.LegalEntity.ShortName,
            ["partner"] = item.BusinessPartner?.ShortName,
            ["account"] = item.Account.AccountName,
            ["amount"] = item.Amount,
            ["payment_method"] = item.PaymentMethod.ToString()
        })), "收款台账");
    }

    private async Task<ExportFileResult> ExportPaymentsAsync(IReadOnlyList<ExportFieldDefinition> fields, DateOnly? cutoffDate, CancellationToken cancellationToken)
    {
        var query = db.PaymentEntries.AsNoTracking().Include(item => item.Project).Include(item => item.LegalEntity).Include(item => item.BusinessPartner).Include(item => item.Account).AsQueryable();
        if (cutoffDate.HasValue) query = query.Where(item => item.PaymentDate <= cutoffDate);
        var entries = await query.OrderByDescending(item => item.PaymentDate).ToListAsync(cancellationToken);
        return CreateSingleSheet("付款", fields, entries.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber,
            ["payment_date"] = item.PaymentDate,
            ["legal_entity"] = item.LegalEntity.ShortName,
            ["partner"] = item.BusinessPartner.ShortName,
            ["account"] = item.Account.AccountName,
            ["amount"] = item.Amount,
            ["payment_method"] = item.PaymentMethod.ToString()
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
                ["is_active"] = item.IsActive
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
            ["is_active"] = item.IsActive
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
            ["is_active"] = item.IsActive
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
        return CreateSingleSheet("设备档案", fields, items.Select(item => Project(fields, new(StringComparer.Ordinal) { ["equipment_number"] = item.EquipmentNumber, ["name"] = item.Name, ["model"] = item.Model, ["category"] = item.Category, ["ownership"] = item.OwnershipType.ToString(), ["owner_company"] = item.OwnerLegalEntity?.Name, ["lessor"] = item.LessorBusinessPartner?.Name, ["status"] = item.Status.ToString(), ["internal_daily_rate"] = item.InternalDailyRate })), "设备档案");
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
        return CreateSingleSheet("设备结算", fields, items.Select(item => Project(fields, new(StringComparer.Ordinal) { ["equipment_number"] = item.Usage.Equipment.EquipmentNumber, ["project_number"] = item.Usage.Project.ProjectNumber, ["settlement_date"] = item.SettlementDate, ["base_amount"] = item.BaseAmount, ["total_amount"] = item.TotalAmount, ["offset_amount"] = item.OffsetAmount, ["payable_id"] = item.PayableEntryId?.ToString() })), "设备结算");
    }

    private static object?[] Project(IReadOnlyList<ExportFieldDefinition> fields, Dictionary<string, object?> values) =>
        fields.Select(field => values[field.Key]).ToArray();

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
