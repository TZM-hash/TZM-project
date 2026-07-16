using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Employees;
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
            new("电话", "phone", false)
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
        ]
    };

    public Task<ExportFileResult> GenerateTemplateAsync(ExportDataset dataset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var columns = GetColumns(dataset);
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(TemplateSheetName(dataset), columns.Select(item => item.Header).ToArray(), []);
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
        var errors = await ValidateRowsAsync(request.Dataset, sheet.Rows.Skip(1).ToArray(), headers, mapping, cancellationToken);
        var totalRows = Math.Max(sheet.Rows.Count - 1, 0);
        var errorRows = errors.Select(item => item.RowNumber).Distinct().Count();
        var batch = new ImportBatch
        {
            CreatedByUserId = userId,
            Dataset = request.Dataset,
            OriginalFileName = fileName,
            OriginalContent = request.Content.ToArray(),
            MappingJson = JsonSerializer.Serialize(mapping),
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

        var sheet = SimpleXlsxReader.Read(batch.OriginalContent)[0];
        var headers = sheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
        var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(batch.MappingJson) ?? [];
        foreach (var row in sheet.Rows.Skip(1))
        {
            var values = RowValues(headers, row, mapping);
            AddEntity(batch.Dataset, values);
        }

        await db.SaveChangesAsync(cancellationToken);
        batch.Status = DataExchangeTaskStatus.Completed;
        batch.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<ImportErrorDto>> ValidateRowsAsync(
        ExportDataset dataset,
        IReadOnlyList<object?>[] rows,
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, string> mapping,
        CancellationToken cancellationToken)
    {
        var errors = new List<ImportErrorDto>();
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var numberKey = dataset switch
        {
            ExportDataset.Employees => "employee_number",
            ExportDataset.Partners => "partner_number",
            ExportDataset.Projects => "project_number",
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
                errors.Add(new ImportErrorDto(excelRow, "员工类型", "员工类型必须是正式员工或劳务员工。", type));
            }
        }

        foreach (var number in seenNumbers)
        {
            var exists = dataset switch
            {
                ExportDataset.Employees => await db.Employees.AnyAsync(item => item.EmployeeNumber == number, cancellationToken),
                ExportDataset.Partners => await db.BusinessPartners.AnyAsync(item => item.PartnerNumber == number, cancellationToken),
                ExportDataset.Projects => await db.Projects.AnyAsync(item => item.ProjectNumber == number, cancellationToken),
                _ => false
            };
            if (exists)
            {
                var rowIndex = rows.Select((row, index) => new { row, index }).First(item => RowValues(headers, item.row, mapping).GetValueOrDefault(numberKey) == number).index + 2;
                errors.Add(new ImportErrorDto(rowIndex, HeaderFor(dataset, numberKey), "编号已存在。", number));
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

    private void AddEntity(ExportDataset dataset, Dictionary<string, string?> values)
    {
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
                    Phone = values.GetValueOrDefault("phone")
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
                var stage = Enum.TryParse<ProjectStage>(values.GetValueOrDefault("stage"), ignoreCase: true, out var parsedStage) ? parsedStage : ProjectStage.Preliminary;
                db.Projects.Add(new Project
                {
                    ProjectNumber = values["project_number"]!,
                    Name = values["name"]!,
                    Stage = stage,
                    GeneralContractorName = values.GetValueOrDefault("general_contractor")
                });
                break;
            default:
                throw new NotSupportedException($"暂不支持导入数据集：{dataset}");
        }
    }

    private static bool TryParseEmployeeType(string value, out EmployeeType employeeType)
    {
        if (value is "正式员工" or "Formal") { employeeType = EmployeeType.Formal; return true; }
        if (value is "劳务员工" or "Labor") { employeeType = EmployeeType.Labor; return true; }
        employeeType = default;
        return false;
    }

    private static IReadOnlyList<ImportColumn> GetColumns(ExportDataset dataset) =>
        Columns.TryGetValue(dataset, out var columns) ? columns : throw new NotSupportedException($"暂不支持导入数据集：{dataset}");

    private static string HeaderFor(ExportDataset dataset, string key) => GetColumns(dataset).Single(item => item.Key == key).Header;

    private static string TemplateSheetName(ExportDataset dataset) => dataset switch
    {
        ExportDataset.Employees => "员工导入",
        ExportDataset.Partners => "合作单位导入",
        ExportDataset.Projects => "项目导入",
        _ => throw new NotSupportedException($"暂不支持导入数据集：{dataset}")
    };

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("值不能为空。", parameterName);
        return value.Trim();
    }

    private sealed record ImportColumn(string Header, string Key, bool Required);
}
