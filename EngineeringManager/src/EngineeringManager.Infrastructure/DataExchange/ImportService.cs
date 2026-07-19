using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed class ImportService(ApplicationDbContext db) : IImportService
{
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
        var sheet = sheets.Count > 0 ? sheets[0] : throw new InvalidDataException("导入文件没有工作表。");
        if (sheet.Rows.Count == 0)
        {
            throw new InvalidDataException("导入工作表没有表头。");
        }

        var headers = sheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
        var mapping = ResolveMapping(request.Dataset, headers, request.SourceToTargetMapping);
        var errors = await ValidateRowsAsync(request.Dataset, sheet.Rows.Skip(1).ToArray(), headers, mapping, request.Mode, cancellationToken);
        var totalRows = Math.Max(sheet.Rows.Count - 1, 0);
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
            ValidRows = totalRows - errorRows,
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
        var sheet = SimpleXlsxReader.Read(batch.OriginalContent)[0];
        var headers = sheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
        var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(batch.MappingJson) ?? [];
        foreach (var row in sheet.Rows.Skip(1))
        {
            var values = RowValues(headers, row, mapping);
            AddOrUpdateEntity(batch.Dataset, values, batch.Mode);
        }

        batch.Status = DataExchangeTaskStatus.Completed;
        batch.CompletedAt = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new AuditLog { UserId = batch.CreatedByUserId, Action = "DataImport", EntityType = nameof(ImportBatch), EntityId = batch.Id.ToString(), Reason = $"导入 {batch.Dataset}", AfterJson = JsonSerializer.Serialize(new { batch.Dataset, batch.Mode, batch.TotalRows, batch.ValidRows }) });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

            if (values.TryGetValue(numberKey, out var number) && !string.IsNullOrWhiteSpace(number) && !seenNumbers.Add(number))
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
                if (!TryParseOwnership(ownership, out var ownershipType)) errors.Add(new ImportErrorDto(excelRow, "权属", "权属必须是自有或租赁。", ownership));
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
        }

        foreach (var number in seenNumbers)
        {
            var exists = dataset switch
            {
                ExportDataset.Employees => await db.Employees.AnyAsync(item => item.EmployeeNumber == number, cancellationToken),
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
            default:
                return false;
        }
    }

    private static decimal ParseDecimal(string? value, decimal fallback = 0m) => decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : fallback;

    private static void EnsureConcurrency(Guid current, string? expected, string label)
    {
        if (!string.IsNullOrWhiteSpace(expected) && Guid.TryParse(expected, out var parsed) && parsed != current)
        {
            throw new InvalidOperationException($"{label}已被其他用户修改，导入已停止，请重新导出后再导入。");
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

    private static bool TryParseOwnership(string? value, out EquipmentOwnershipType type) { if (value is "自有" or "SelfOwned") { type = EquipmentOwnershipType.SelfOwned; return true; } if (value is "租赁" or "Rented") { type = EquipmentOwnershipType.Rented; return true; } type = default; return false; }
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

    private static string HeaderFor(ExportDataset dataset, string key) => GetColumns(dataset).Single(item => item.Key == key).Header;

    private static string TemplateSheetName(ExportDataset dataset) => dataset switch
    {
        ExportDataset.Employees => "员工导入",
        ExportDataset.EmployeeCertificates => "员工证书导入",
        ExportDataset.Partners => "合作单位导入",
        ExportDataset.Projects => "项目导入",
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
