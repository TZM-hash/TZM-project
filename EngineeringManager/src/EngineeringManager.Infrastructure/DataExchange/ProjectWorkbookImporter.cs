using System.Globalization;
using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed class ProjectWorkbookImporter(ApplicationDbContext db, IFileStore? fileStore = null)
{
    private sealed record ParsedRow(ProjectWorkbookSheet Sheet, int RowNumber, Dictionary<string, string?> Values, HashSet<string> PresentKeys);
    private sealed record ParsedSheet(ProjectWorkbookSheet Sheet, string WorksheetName, List<ParsedRow> Rows);
    private sealed record ParsedWorkbook(IReadOnlyList<ParsedSheet> Sheets, List<ImportErrorDto> Errors);
    private sealed record ImportOptions(ImportMode Mode, bool IncludeAttachments, bool BlankMeansNoChange, IReadOnlyDictionary<ProjectWorkbookSheet, IReadOnlyDictionary<string, string>>? Mappings);
    private sealed record ValidationResult(ParsedWorkbook Workbook, IReadOnlyList<ProjectWorkbookSheetPreviewDto> SheetPreviews, IReadOnlyList<ImportErrorDto> Errors);

    public async Task<ProjectWorkbookImportPreviewDto> PreviewAsync(ProjectWorkbookImportRequest request, CancellationToken cancellationToken)
    {
        var parsed = await ParseAsync(request, cancellationToken);
        if (request.Actor is not null) AddPermissionErrors(request.Actor, parsed);
        var validated = await ValidateAsync(parsed, request.Mode, request.BlankMeansNoChange, cancellationToken);
        var errorRows = CountErrorRows(validated);
        var totalRows = validated.SheetPreviews.Sum(item => item.TotalRows);
        var batch = new ImportBatch
        {
            CreatedByUserId = request.UserId,
            Dataset = ExportDataset.ProjectOverview,
            Mode = request.Mode,
            OriginalFileName = request.OriginalFileName,
            OriginalContent = request.Content,
            MappingJson = JsonSerializer.Serialize(new ImportOptions(request.Mode, request.IncludeAttachments, request.BlankMeansNoChange, request.Mappings)),
            Status = DataExchangeTaskStatus.PreviewReady,
            TotalRows = totalRows,
            ValidRows = Math.Max(0, totalRows - errorRows),
            ErrorRows = errorRows
        };
        foreach (var error in validated.Errors)
        {
            batch.Errors.Add(new ImportError { RowNumber = error.RowNumber, ColumnName = error.ColumnName, Message = error.Message, RawValue = error.RawValue });
        }
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        return new ProjectWorkbookImportPreviewDto(batch.Id, batch.TotalRows, batch.ValidRows, batch.ErrorRows, validated.SheetPreviews, validated.Errors);
    }

    public async Task ConfirmAsync(ProjectWorkbookActor actor, Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await db.ImportBatches.Include(item => item.Errors).SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken)
            ?? throw new InvalidOperationException("导入批次不存在。");
        if (!string.Equals(batch.CreatedByUserId, actor.UserId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("只能确认本人创建的导入批次。");
        if (!actor.CanImport)
            throw new UnauthorizedAccessException("当前用户无权导入项目工作簿。");
        if (batch.Status != DataExchangeTaskStatus.PreviewReady)
            throw new InvalidOperationException("导入批次状态已变化，不能重复确认。");
        if (batch.ErrorRows > 0) throw new InvalidOperationException("导入批次存在错误，修正后重新预览。");
        var options = JsonSerializer.Deserialize<ImportOptions>(batch.MappingJson) ?? new ImportOptions(batch.Mode, false, false, null);
        var parsed = await ParseAsync(new ProjectWorkbookImportRequest(batch.CreatedByUserId, batch.OriginalFileName, batch.OriginalContent, options.Mode, options.IncludeAttachments, options.Mappings, options.BlankMeansNoChange, actor), cancellationToken);
        AddPermissionErrors(actor, parsed);
        var validated = await ValidateAsync(parsed, options.Mode, options.BlankMeansNoChange, cancellationToken);
        if (validated.Errors.Count > 0)
        {
            throw new InvalidOperationException("导入预览已过期或出现新的校验错误，请重新预览。");
        }

        var archive = options.IncludeAttachments || batch.OriginalFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ProjectWorkbookArchive.Read(batch.OriginalContent) : null;
        if (archive is not null && archive.Errors.Count > 0) throw new InvalidOperationException("附件归档校验失败，请修正后重新预览。");
        if (archive is not null && fileStore is null) throw new InvalidOperationException("导入附件需要文件存储。");
        var savedFiles = new List<string>();
        var committed = false;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            batch.Status = DataExchangeTaskStatus.Running;
            await db.SaveChangesAsync(cancellationToken);
            await WriteProjectsAsync(validated.Workbook, options.Mode, options.BlankMeansNoChange, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await WriteContractsAsync(validated.Workbook, options.Mode, options.BlankMeansNoChange, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await WriteQuantityLinesAsync(validated.Workbook, options.Mode, options.BlankMeansNoChange, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await WriteProjectDetailsAsync(validated.Workbook, options.BlankMeansNoChange, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await WriteFinanceAsync(validated.Workbook, options.BlankMeansNoChange, cancellationToken);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                var entityTypes = string.Join(", ", exception.Entries.Select(item => item.Metadata.ClrType.Name));
                throw new DbUpdateConcurrencyException($"项目工作簿财务写入发生并发冲突：{entityTypes}", exception);
            }
            if (archive is not null && fileStore is not null)
            {
                foreach (var item in archive.Attachments)
                {
                    var originalFileName = Path.GetFileName(item.OriginalFileName);
                    if (string.IsNullOrWhiteSpace(originalFileName) || !string.Equals(originalFileName, item.OriginalFileName, StringComparison.Ordinal))
                        throw new InvalidDataException("附件原文件名不安全。");
                    var relation = await ResolveAttachmentRelationAsync(item, cancellationToken);
                    if (!Enum.TryParse<AttachmentCategory>(item.Category, true, out var category) || !Enum.IsDefined(category))
                        throw new InvalidDataException($"附件分类无效：{item.Category}");
                    await using var content = new MemoryStream(item.Content, writable: false);
                    var storedName = await fileStore.SaveAsync(content, originalFileName, cancellationToken);
                    savedFiles.Add(storedName);
                    var attachment = new Attachment
                    {
                        StoredName = storedName, OriginalFileName = originalFileName, SizeBytes = item.SizeBytes, ContentType = item.ContentType,
                        Category = category, Description = item.Description, ProjectId = relation.ProjectId, ContractId = relation.ContractId, StageResultId = relation.StageResultId
                    };
                    db.Attachments.Add(attachment);
                }
                await db.SaveChangesAsync(cancellationToken);
            }
            batch.Status = DataExchangeTaskStatus.Completed;
            batch.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
        }
        catch (Exception exception)
        {
            if (!committed)
                await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            Exception? cleanupException = null;
            if (fileStore is not null)
            {
                foreach (var storedName in savedFiles)
                {
                    try { await fileStore.DeleteAsync(storedName, CancellationToken.None); }
                    catch (Exception cleanupError) { cleanupException ??= cleanupError; }
                }
            }
            var failedBatch = await db.ImportBatches.Include(item => item.Errors).SingleAsync(item => item.Id == batchId, CancellationToken.None);
            failedBatch.Status = DataExchangeTaskStatus.Failed;
            failedBatch.ErrorRows = Math.Max(1, failedBatch.ErrorRows);
            db.ImportErrors.Add(new ImportError
            {
                Batch = failedBatch,
                RowNumber = 1,
                ColumnName = "导入批次/提交",
                Message = cleanupException is null ? "导入事务失败，已回滚。" : $"导入事务失败且附件补偿删除失败：{cleanupException.Message}"
            });
            await db.SaveChangesAsync(CancellationToken.None);
            if (cleanupException is not null) throw new AggregateException(exception, cleanupException);
            throw;
        }
    }

    private async Task<(Guid? ProjectId, Guid? ContractId, Guid? StageResultId)> ResolveAttachmentRelationAsync(
        ProjectWorkbookArchiveAttachment item,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.ProjectNumber))
            throw new InvalidDataException("附件清单缺少项目编号。");
        var projectId = await db.Projects.Where(project => project.ProjectNumber == item.ProjectNumber)
            .Select(project => (Guid?)project.Id).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidDataException($"附件关联项目不存在：{item.ProjectNumber}");

        if (!string.IsNullOrWhiteSpace(item.StageResultKey))
        {
            var parts = item.StageResultKey.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !DateOnly.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var resultDate))
                throw new InvalidDataException("附件阶段成果业务键无效。");
            var stageResultId = await db.StageResults.Where(result => result.ProjectId == projectId && result.Title == parts[0] && result.ResultDate == resultDate)
                .Select(result => (Guid?)result.Id).SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidDataException($"附件关联阶段成果不存在：{item.StageResultKey}");
            return (null, null, stageResultId);
        }

        if (!string.IsNullOrWhiteSpace(item.ContractNumber))
        {
            var contractId = await db.Contracts.Where(contract => contract.ProjectId == projectId && contract.ContractNumber == item.ContractNumber)
                .Select(contract => (Guid?)contract.Id).SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidDataException($"附件关联合同不存在：{item.ContractNumber}");
            return (null, contractId, null);
        }

        return (projectId, null, null);
    }

    private static Task<ParsedWorkbook> ParseAsync(ProjectWorkbookImportRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var archiveErrors = new List<ImportErrorDto>();
        var workbookContent = request.Content;
        ProjectWorkbookArchiveReadResult? archive = null;
        if (request.IncludeAttachments || request.OriginalFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            archive = ProjectWorkbookArchive.Read(request.Content);
            workbookContent = archive.Workbook;
            archiveErrors.AddRange(archive.Errors);
        }
        var sheets = workbookContent.Length == 0 ? [] : SimpleXlsxReader.Read(workbookContent);
        var parsed = new List<ParsedSheet>();
        var errors = archiveErrors;
        ValidateWorkbookMetadata(sheets, request.Mappings is not null, errors);
        foreach (var sheet in sheets)
        {
            if (sheet.Name is "目录说明" or "_metadata") continue;
            var mappedDefinition = request.Mappings is { Count: 1 } ? ProjectWorkbookCatalog.Get(request.Mappings.Keys.Single()) : null;
            var definition = ProjectWorkbookCatalog.Sheets.SingleOrDefault(item => item.WorksheetName == sheet.Name) ?? mappedDefinition;
            if (definition is null)
            {
                errors.Add(new ImportErrorDto(1, $"{sheet.Name}/工作表", "未知项目工作簿工作表。", sheet.Name));
                continue;
            }
            if (!definition.CanImport)
            {
                // Standard exports contain calculated, read-only sheets. They are ignored on import.
                continue;
            }
            var rows = sheet.Rows.ToArray();
            if (rows.Length == 0) continue;
            var headerRow = rows[0];
            var mappings = request.Mappings?.GetValueOrDefault(definition.Sheet);
            var keys = new Dictionary<int, string>();
            for (var column = 0; column < headerRow.Count; column++)
            {
                var header = ConvertValue(headerRow[column]);
                if (string.IsNullOrWhiteSpace(header)) continue;
                var target = mappings?.GetValueOrDefault(header)
                    ?? definition.Fields.FirstOrDefault(field => field.Header == header || field.Aliases?.Contains(header, StringComparer.OrdinalIgnoreCase) == true)?.Key;
                if (target is not null) keys[column] = target;
            }
            foreach (var required in definition.Fields.Where(field => field.IsRequired && field.CanImport))
            {
                if (request.Mode == ImportMode.Update) continue;
                if (!keys.Values.Contains(required.Key, StringComparer.Ordinal))
                {
                    errors.Add(new ImportErrorDto(1, $"{sheet.Name}/{required.Header}", "缺少必填列。", null));
                }
            }
            var parsedRows = new List<ParsedRow>();
            for (var rowIndex = 1; rowIndex < rows.Length; rowIndex++)
            {
                var row = rows[rowIndex];
                if (row.All(value => string.IsNullOrWhiteSpace(ConvertValue(value)))) continue;
                var values = new Dictionary<string, string?>(StringComparer.Ordinal);
                var present = new HashSet<string>(StringComparer.Ordinal);
                foreach (var pair in keys)
                {
                    present.Add(pair.Value);
                    values[pair.Value] = pair.Key < row.Count ? ConvertValue(row[pair.Key]) : null;
                }
                parsedRows.Add(new ParsedRow(definition.Sheet, rowIndex + 1, values, present));
            }
            parsed.Add(new ParsedSheet(definition.Sheet, sheet.Name, parsedRows));
        }
        var attachmentSheet = parsed.FirstOrDefault(item => item.Sheet == ProjectWorkbookSheet.Attachments);
        if (attachmentSheet is not null)
        {
            if (archive is null)
            {
                errors.Add(new ImportErrorDto(1, $"{attachmentSheet.WorksheetName}/ZIP", "附件清单必须随附件 ZIP 一起导入。", null));
            }
            else
            {
                ValidateAttachmentSheet(attachmentSheet, archive.Attachments, errors);
            }
        }
        else if (archive is not null && archive.Attachments.Count > 0)
        {
            errors.Add(new ImportErrorDto(1, "附件清单/工作表", "ZIP 中存在附件，但工作簿缺少附件清单工作表。", null));
        }
        return Task.FromResult(new ParsedWorkbook(parsed, errors));
    }

    private static void ValidateAttachmentSheet(
        ParsedSheet sheet,
        IReadOnlyList<ProjectWorkbookArchiveAttachment> attachments,
        List<ImportErrorDto> errors)
    {
        var expected = attachments.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in sheet.Rows)
        {
            var path = row.Values.GetValueOrDefault("relative_path");
            if (string.IsNullOrWhiteSpace(path) || !expected.TryGetValue(path, out var attachment))
            {
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/相对路径", "附件清单中的相对路径未在 ZIP manifest 中找到。", path));
                continue;
            }
            if (!seen.Add(path))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/相对路径", "附件清单相对路径重复。", path));
            CompareAttachmentValue(row, sheet, "sha256", attachment.Sha256, errors);
            CompareAttachmentValue(row, sheet, "original_file_name", attachment.OriginalFileName, errors);
            CompareAttachmentValue(row, sheet, "category", attachment.Category, errors);
            CompareAttachmentValue(row, sheet, "project_number", attachment.ProjectNumber, errors);
            CompareAttachmentValue(row, sheet, "contract_number", attachment.ContractNumber, errors);
            CompareAttachmentValue(row, sheet, "size_bytes", attachment.SizeBytes.ToString(CultureInfo.InvariantCulture), errors);
        }
        foreach (var path in expected.Keys.Where(path => !seen.Contains(path)))
            errors.Add(new ImportErrorDto(1, $"{sheet.WorksheetName}/相对路径", "ZIP manifest 中的附件未出现在附件清单工作表。", path));
    }

    private static void CompareAttachmentValue(
        ParsedRow row,
        ParsedSheet sheet,
        string key,
        string? expected,
        List<ImportErrorDto> errors)
    {
        if (expected is null) return;
        var actual = row.Values.GetValueOrDefault(key);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            var header = ProjectWorkbookCatalog.Get(sheet.Sheet).Fields.FirstOrDefault(item => item.Key == key)?.Header ?? key;
            errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/{header}", "附件清单与 ZIP manifest 不一致。", actual));
        }
    }

    private static void ValidateWorkbookMetadata(
        IReadOnlyList<SimpleXlsxSheet> sheets,
        bool isMappedImport,
        List<ImportErrorDto> errors)
    {
        if (isMappedImport) return;
        var metadata = sheets.SingleOrDefault(item => item.Name == "_metadata");
        if (metadata is null || metadata.Rows.Count < 2)
        {
            errors.Add(new ImportErrorDto(1, "_metadata/版本", "标准项目工作簿缺少版本元数据。", null));
            return;
        }
        if (!metadata.IsVeryHidden)
            errors.Add(new ImportErrorDto(1, "_metadata/状态", "标准项目工作簿元数据表必须为 veryHidden。", null));

        var headers = metadata.Rows[0].Select(ConvertValue).ToArray();
        var values = metadata.Rows[1];
        string? Value(string key)
        {
            var index = Array.FindIndex(headers, header => string.Equals(header, key, StringComparison.Ordinal));
            return index >= 0 && index < values.Count ? ConvertValue(values[index]) : null;
        }

        var workbookVersion = Value("WorkbookVersion");
        var datasetVersion = Value("DatasetVersion");
        if (!string.Equals(workbookVersion, ProjectWorkbookVersions.Workbook, StringComparison.Ordinal))
            errors.Add(new ImportErrorDto(2, "_metadata/WorkbookVersion", "项目工作簿版本不受支持。", workbookVersion));
        if (!string.Equals(datasetVersion, ProjectWorkbookVersions.Dataset, StringComparison.Ordinal))
            errors.Add(new ImportErrorDto(2, "_metadata/DatasetVersion", "项目数据集版本不受支持。", datasetVersion));
    }

    private static void AddPermissionErrors(ProjectWorkbookActor actor, ParsedWorkbook workbook)
    {
        foreach (var sheet in workbook.Sheets.Where(item => !actor.CanManageSheet(item.Sheet)))
            workbook.Errors.Add(new ImportErrorDto(1, $"{sheet.WorksheetName}/权限", "当前用户没有该工作表的维护权限。", sheet.WorksheetName));
    }

    private async Task<ValidationResult> ValidateAsync(ParsedWorkbook workbook, ImportMode mode, bool blankMeansNoChange, CancellationToken cancellationToken)
    {
        var sheetPrefixes = workbook.Sheets.Select(item => $"{item.WorksheetName}/").ToArray();
        var errors = workbook.Errors
            .Where(error => !sheetPrefixes.Any(prefix => error.ColumnName.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();
        var previews = new List<ProjectWorkbookSheetPreviewDto>();
        foreach (var sheet in workbook.Sheets)
        {
            var prefix = $"{sheet.WorksheetName}/";
            var sheetErrors = workbook.Errors.Where(error => error.ColumnName.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            ValidateDuplicateKeys(sheet, sheetErrors);
            var newRows = 0;
            var updatedRows = 0;
            var unchangedRows = 0;
            var sheetBlocked = sheetErrors.Any(error => error.ColumnName.EndsWith("/权限", StringComparison.Ordinal));
            foreach (var row in sheet.Rows)
            {
                ValidateRowRequired(sheet, row, mode, sheetErrors);
                ValidateRowTypes(sheet, row, sheetErrors);
                await ValidateRelationsAsync(sheet, row, workbook, sheetErrors, cancellationToken);
                await ValidateFinanceRelationsAsync(sheet, row, workbook, sheetErrors, cancellationToken);
                var existing = await FindExistingAsync(sheet, row, cancellationToken);
                await ValidateExistingAsync(sheet, row, mode, sheetErrors, cancellationToken);
                if (sheetBlocked || sheetErrors.Any(error => error.RowNumber == row.RowNumber)) continue;
                if (existing is null) newRows++;
                else if (await IsRowChangedAsync(sheet, row, existing, cancellationToken)) updatedRows++;
                else unchangedRows++;
            }
            errors.AddRange(sheetErrors);
            var errorRows = sheetErrors.Select(item => item.RowNumber).Distinct().Count();
            previews.Add(new ProjectWorkbookSheetPreviewDto(sheet.Sheet, sheet.WorksheetName, sheet.Rows.Count, newRows, updatedRows, unchangedRows, 0, errorRows, sheetErrors));
        }
        return new ValidationResult(workbook, previews, errors);
    }

    private static int CountErrorRows(ValidationResult result)
    {
        var sheetErrorRows = result.SheetPreviews.Sum(item => item.ErrorRows);
        var sheetNames = result.Workbook.Sheets.Select(item => item.WorksheetName).ToArray();
        var globalErrorRows = result.Errors
            .Where(error => !sheetNames.Any(name => error.ColumnName.StartsWith($"{name}/", StringComparison.Ordinal)))
            .Select(error => error.RowNumber)
            .Distinct()
            .Count();
        return sheetErrorRows + globalErrorRows;
    }

    private static void ValidateDuplicateKeys(ParsedSheet sheet, List<ImportErrorDto> errors)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in sheet.Rows)
        {
            var key = sheet.Sheet switch
            {
                ProjectWorkbookSheet.ProjectMaster => row.Values.GetValueOrDefault("project_number"),
                ProjectWorkbookSheet.Contracts => $"{row.Values.GetValueOrDefault("project_number")}|{row.Values.GetValueOrDefault("contract_number")}",
                ProjectWorkbookSheet.QuantityLines => $"{row.Values.GetValueOrDefault("project_number")}|{row.Values.GetValueOrDefault("contract_number")}|{row.Values.GetValueOrDefault("code")}",
                ProjectWorkbookSheet.Milestones => $"{row.Values.GetValueOrDefault("project_number")}|{row.Values.GetValueOrDefault("name")}",
                ProjectWorkbookSheet.Assignments => $"{row.Values.GetValueOrDefault("project_number")}|{row.Values.GetValueOrDefault("user_id")}",
                ProjectWorkbookSheet.Partners => $"{row.Values.GetValueOrDefault("project_number")}|{row.Values.GetValueOrDefault("partner_number")}",
                ProjectWorkbookSheet.StageResults => $"{row.Values.GetValueOrDefault("project_number")}|{row.Values.GetValueOrDefault("title")}|{row.Values.GetValueOrDefault("result_date")}",
                ProjectWorkbookSheet.Invoices => $"{row.Values.GetValueOrDefault("project_number")}|{row.Values.GetValueOrDefault("invoice_number")}",
                _ => row.Values.GetValueOrDefault("_system_id")
            };
            if (string.IsNullOrWhiteSpace(key) || key.Contains("||", StringComparison.Ordinal)) continue;
            if (seen.TryGetValue(key, out var firstRow))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/业务键", $"业务键重复，首次出现在第 {firstRow} 行。", key));
            else seen[key] = row.RowNumber;
        }
    }

    private static void ValidateRowRequired(ParsedSheet sheet, ParsedRow row, ImportMode mode, List<ImportErrorDto> errors)
    {
        foreach (var field in ProjectWorkbookCatalog.Get(sheet.Sheet).Fields.Where(item => item.IsRequired && item.CanImport))
        {
            if (mode == ImportMode.Update && !row.PresentKeys.Contains(field.Key)) continue;
            if (!row.Values.TryGetValue(field.Key, out var value) || string.IsNullOrWhiteSpace(value))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/{field.Header}", "必填字段不能为空。", value));
        }

        if (mode == ImportMode.Update && !row.PresentKeys.Contains("_system_id") && !HasUpdateBusinessKey(sheet.Sheet, row))
            errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/定位字段", "仅更新模式必须保留系统 ID 或完整业务定位键。", null));
        if (mode == ImportMode.Update)
        {
            foreach (var key in RequiredUpdateDependencyKeys(sheet.Sheet).Where(key => !row.PresentKeys.Contains(key) || string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault(key))))
            {
                var header = ProjectWorkbookCatalog.Get(sheet.Sheet).Fields.First(item => item.Key == key).Header;
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/{header}", "仅更新模式缺少业务依赖字段。", null));
            }
        }
    }

    private static IReadOnlyList<string> RequiredUpdateDependencyKeys(ProjectWorkbookSheet sheet) => sheet switch
    {
        ProjectWorkbookSheet.Contracts or ProjectWorkbookSheet.Milestones or ProjectWorkbookSheet.Assignments
            or ProjectWorkbookSheet.Partners or ProjectWorkbookSheet.Construction or ProjectWorkbookSheet.StageResults
            or ProjectWorkbookSheet.Receivables or ProjectWorkbookSheet.Collections or ProjectWorkbookSheet.Payables
            or ProjectWorkbookSheet.Payments or ProjectWorkbookSheet.Invoices or ProjectWorkbookSheet.Deductions => ["project_number"],
        ProjectWorkbookSheet.QuantityLines => ["project_number", "contract_number"],
        _ => []
    };

    private static bool HasUpdateBusinessKey(ProjectWorkbookSheet sheet, ParsedRow row)
    {
        static bool HasValue(ParsedRow item, string key) => !string.IsNullOrWhiteSpace(item.Values.GetValueOrDefault(key));
        return sheet switch
        {
            ProjectWorkbookSheet.ProjectMaster => HasValue(row, "project_number"),
            ProjectWorkbookSheet.Contracts => HasValue(row, "project_number") && HasValue(row, "contract_number"),
            ProjectWorkbookSheet.QuantityLines => HasValue(row, "project_number") && HasValue(row, "contract_number") && HasValue(row, "code"),
            ProjectWorkbookSheet.Milestones => HasValue(row, "project_number") && HasValue(row, "name"),
            ProjectWorkbookSheet.Assignments => HasValue(row, "project_number") && HasValue(row, "user_id"),
            ProjectWorkbookSheet.Partners => HasValue(row, "project_number") && HasValue(row, "partner_number"),
            ProjectWorkbookSheet.StageResults => HasValue(row, "project_number") && HasValue(row, "title") && HasValue(row, "result_date"),
            ProjectWorkbookSheet.Invoices => HasValue(row, "project_number") && HasValue(row, "invoice_number"),
            _ => false
        };
    }

    private static void ValidateRowTypes(ParsedSheet sheet, ParsedRow row, List<ImportErrorDto> errors)
    {
        foreach (var field in ProjectWorkbookCatalog.Get(sheet.Sheet).Fields.Where(item => item.CanImport && row.Values.TryGetValue(item.Key, out var value) && !string.IsNullOrWhiteSpace(value)))
        {
            var value = row.Values[field.Key]!;
            if (field.DataType == ExportFieldDataType.Number && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/{field.Header}", "数字格式无效。", value));
            if (field.DataType == ExportFieldDataType.Date && !DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/{field.Header}", "日期格式无效，应为 yyyy-MM-dd。", value));
            if (field.DataType == ExportFieldDataType.Boolean && !TryParseBoolean(value, out _))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/{field.Header}", "布尔值无效，应填写 true/false、1/0 或是/否。", value));
            if (IsEnumField(sheet.Sheet, field.Key) && !IsValidEnumValue(sheet.Sheet, field.Key, value))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/{field.Header}", "枚举值无效。", value));
            if (field.Key == "_dataset_version" && !string.Equals(value, ProjectWorkbookVersions.Dataset, StringComparison.Ordinal))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/{field.Header}", "数据集版本不受支持。", value));
            if (IsGuidField(field.Key) && !Guid.TryParse(value, out _))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/{field.Header}", "ID 格式无效。", value));
            if (field.Key == "legal_entity_ids" && value.Split([',', '，', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(item => !Guid.TryParse(item, out _)))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/{field.Header}", "签约公司 ID 列表格式无效。", value));
        }
    }

    private static bool IsGuidField(string key) => key is "department_id" or "branch_id" or "account_id" or "receivable_id" or "payable_id" or "settlement_id" or "stage_result_id"
        or "_system_id" or "_project_system_id" or "_contract_system_id" or "_concurrency_stamp";

    private static bool IsEnumField(ProjectWorkbookSheet sheet, string key) => (sheet, key) switch
    {
        (ProjectWorkbookSheet.ProjectMaster, "stage" or "contract_signing_status" or "affiliation_type") => true,
        (ProjectWorkbookSheet.Contracts, "contract_type" or "allocation_mode") => true,
        (ProjectWorkbookSheet.Assignments, "assignment_type") => true,
        (ProjectWorkbookSheet.Partners, "role_type") => true,
        (ProjectWorkbookSheet.Construction, "record_type") => true,
        (ProjectWorkbookSheet.StageResults, "result_type" or "status" or "quality_result") => true,
        (ProjectWorkbookSheet.Receivables, "source_type" or "settlement_state") => true,
        (ProjectWorkbookSheet.Collections, "payment_method") => true,
        (ProjectWorkbookSheet.Payables, "source_type" or "settlement_state") => true,
        (ProjectWorkbookSheet.Payments, "payment_method") => true,
        (ProjectWorkbookSheet.Invoices, "direction" or "status") => true,
        (ProjectWorkbookSheet.Deductions, "status") => true,
        (ProjectWorkbookSheet.Attachments, "category") => true,
        _ => false
    };

    private static bool IsValidEnumValue(ProjectWorkbookSheet sheet, string key, string value) => (sheet, key) switch
    {
        (ProjectWorkbookSheet.ProjectMaster, "stage") => IsDefined<ProjectStage>(value),
        (ProjectWorkbookSheet.ProjectMaster, "contract_signing_status") => IsDefined<ContractSigningStatus>(value),
        (ProjectWorkbookSheet.ProjectMaster, "affiliation_type") => IsDefined<ProjectAffiliationType>(value),
        (ProjectWorkbookSheet.Contracts, "contract_type") => IsDefined<ContractType>(value),
        (ProjectWorkbookSheet.Contracts, "allocation_mode") => IsDefined<ContractAllocationMode>(value),
        (ProjectWorkbookSheet.Assignments, "assignment_type") => IsDefined<ProjectAssignmentType>(value),
        (ProjectWorkbookSheet.Partners, "role_type") => IsDefined<BusinessPartnerRoleType>(value),
        (ProjectWorkbookSheet.Construction, "record_type") => IsDefined<ProjectConstructionRecordType>(value),
        (ProjectWorkbookSheet.StageResults, "result_type") => IsDefined<StageResultType>(value),
        (ProjectWorkbookSheet.StageResults, "status") => IsDefined<StageResultStatus>(value),
        (ProjectWorkbookSheet.StageResults, "quality_result") => IsDefined<QualityResult>(value),
        (ProjectWorkbookSheet.Receivables, "source_type") => IsDefined<ReceivableSourceType>(value) || IsDefined<LedgerSourceType>(value),
        (ProjectWorkbookSheet.Receivables, "settlement_state") => IsDefined<LedgerSettlementState>(value),
        (ProjectWorkbookSheet.Collections, "payment_method") => IsDefined<PaymentMethod>(value),
        (ProjectWorkbookSheet.Payables, "source_type") => IsDefined<PayableSourceType>(value) || IsDefined<LedgerSourceType>(value),
        (ProjectWorkbookSheet.Payables, "settlement_state") => IsDefined<LedgerSettlementState>(value),
        (ProjectWorkbookSheet.Payments, "payment_method") => IsDefined<PaymentMethod>(value),
        (ProjectWorkbookSheet.Invoices, "direction") => IsDefined<InvoiceDirection>(value),
        (ProjectWorkbookSheet.Invoices, "status") => IsDefined<InvoiceStatus>(value) || IsDefined<LedgerRecordStatus>(value),
        (ProjectWorkbookSheet.Deductions, "status") => IsDefined<LedgerRecordStatus>(value),
        (ProjectWorkbookSheet.Attachments, "category") => IsDefined<AttachmentCategory>(value),
        _ => true
    };

    private static bool IsDefined<TEnum>(string value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var parsed) && Enum.IsDefined(parsed);

    private async Task ValidateRelationsAsync(ParsedSheet sheet, ParsedRow row, ParsedWorkbook workbook, List<ImportErrorDto> errors, CancellationToken cancellationToken)
    {
        if (sheet.Sheet != ProjectWorkbookSheet.ProjectMaster)
        {
            var projectNumber = row.Values.GetValueOrDefault("project_number");
            var projectInFile = workbook.Sheets.FirstOrDefault(item => item.Sheet == ProjectWorkbookSheet.ProjectMaster)?.Rows.Any(item => item.Values.GetValueOrDefault("project_number") == projectNumber) == true;
            if (!projectInFile && !await db.Projects.AnyAsync(item => item.ProjectNumber == projectNumber, cancellationToken))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/项目编号", "项目不存在或未在本批次创建。", projectNumber));
        }
        if (sheet.Sheet == ProjectWorkbookSheet.QuantityLines)
        {
            var projectNumber = row.Values.GetValueOrDefault("project_number");
            var contractNumber = row.Values.GetValueOrDefault("contract_number");
            var contractInFile = workbook.Sheets.FirstOrDefault(item => item.Sheet == ProjectWorkbookSheet.Contracts)?.Rows.Any(item => item.Values.GetValueOrDefault("project_number") == projectNumber && item.Values.GetValueOrDefault("contract_number") == contractNumber) == true;
            if (!contractInFile && !await db.Contracts.AnyAsync(item => item.Project.ProjectNumber == projectNumber && item.ContractNumber == contractNumber, cancellationToken))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/合同编号", "合同不存在或未在本批次创建。", contractNumber));
        }
        if (sheet.Sheet is not ProjectWorkbookSheet.ProjectMaster and not ProjectWorkbookSheet.QuantityLines
            && row.PresentKeys.Contains("contract_number") && !string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("contract_number")))
        {
            var projectNumber = row.Values.GetValueOrDefault("project_number");
            var contractNumber = row.Values.GetValueOrDefault("contract_number");
            if (!await db.Contracts.AnyAsync(item => item.Project.ProjectNumber == projectNumber && item.ContractNumber == contractNumber, cancellationToken)
                && workbook.Sheets.FirstOrDefault(item => item.Sheet == ProjectWorkbookSheet.Contracts)?.Rows.Any(item => item.Values.GetValueOrDefault("project_number") == projectNumber && item.Values.GetValueOrDefault("contract_number") == contractNumber) != true)
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/合同编号", "合同不存在或不属于当前项目。", contractNumber));
        }
        await ValidateLookupRelationsAsync(sheet, row, errors, cancellationToken);
    }

    private async Task ValidateLookupRelationsAsync(ParsedSheet sheet, ParsedRow row, List<ImportErrorDto> errors, CancellationToken cancellationToken)
    {
        async Task Require(bool valid, string header, string message, string? raw)
        {
            await Task.CompletedTask;
            if (!valid) errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/{header}", message, raw));
        }

        if (sheet.Sheet == ProjectWorkbookSheet.ProjectMaster)
        {
            var responsibleUserId = row.Values.GetValueOrDefault("responsible_user_id");
            if (!string.IsNullOrWhiteSpace(responsibleUserId)) await Require(await db.Users.AnyAsync(item => item.Id == responsibleUserId, cancellationToken), "负责人账号", "负责人账号不存在。", responsibleUserId);
            if (Guid.TryParse(row.Values.GetValueOrDefault("department_id"), out var departmentId)) await Require(await db.OrganizationUnits.AnyAsync(item => item.Id == departmentId && item.UnitType == OrganizationUnitType.Department && item.IsActive, cancellationToken), "部门ID", "部门不存在、已停用或类型不匹配。", departmentId.ToString());
            if (Guid.TryParse(row.Values.GetValueOrDefault("branch_id"), out var branchId)) await Require(await db.OrganizationUnits.AnyAsync(item => item.Id == branchId && item.UnitType == OrganizationUnitType.Branch && item.IsActive, cancellationToken), "分支机构ID", "分支机构不存在、已停用或类型不匹配。", branchId.ToString());
            if (!string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("legal_entity_ids")))
            {
                var ids = row.Values["legal_entity_ids"]!.Split([',', '，', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(item => Guid.TryParse(item, out _)).Select(Guid.Parse).Distinct().ToArray();
                var count = await db.LegalEntities.CountAsync(item => ids.Contains(item.Id) && item.IsActive, cancellationToken);
                await Require(count == ids.Length, "签约公司ID", "签约公司包含不存在或已停用的记录。", row.Values["legal_entity_ids"]);
            }
        }
        else if (sheet.Sheet == ProjectWorkbookSheet.Assignments && !string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("user_id")))
            await Require(await db.Users.AnyAsync(item => item.Id == row.Values["user_id"], cancellationToken), "人员账号", "项目人员账号不存在。", row.Values["user_id"]);
        else if (sheet.Sheet == ProjectWorkbookSheet.Partners && !string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("partner_number")))
            await Require(await db.BusinessPartners.AnyAsync(item => item.PartnerNumber == row.Values["partner_number"] && item.IsActive, cancellationToken), "单位编号", "合作单位不存在或已停用。", row.Values["partner_number"]);
        else if (sheet.Sheet == ProjectWorkbookSheet.Construction)
        {
            var equipmentNumber = row.Values.GetValueOrDefault("equipment_number");
            var crewNumber = row.Values.GetValueOrDefault("crew_partner_number");
            var transferFrom = row.Values.GetValueOrDefault("transfer_from_project_number");
            var transferTo = row.Values.GetValueOrDefault("transfer_to_project_number");
            if (!string.IsNullOrWhiteSpace(equipmentNumber)) await Require(await db.Equipment.AnyAsync(item => item.EquipmentNumber == equipmentNumber, cancellationToken), "设备编号", "设备不存在。", equipmentNumber);
            if (!string.IsNullOrWhiteSpace(crewNumber)) await Require(await db.BusinessPartners.AnyAsync(item => item.PartnerNumber == crewNumber && item.IsActive, cancellationToken), "班组编号", "施工班组不存在或已停用。", crewNumber);
            if (!string.IsNullOrWhiteSpace(transferFrom)) await Require(await db.Projects.AnyAsync(item => item.ProjectNumber == transferFrom, cancellationToken), "调入项目", "调入项目不存在。", transferFrom);
            if (!string.IsNullOrWhiteSpace(transferTo)) await Require(await db.Projects.AnyAsync(item => item.ProjectNumber == transferTo, cancellationToken), "调出项目", "调出项目不存在。", transferTo);
        }
    }

    private async Task ValidateFinanceRelationsAsync(ParsedSheet sheet, ParsedRow row, ParsedWorkbook workbook, List<ImportErrorDto> errors, CancellationToken cancellationToken)
    {
        if (sheet.Sheet is not (ProjectWorkbookSheet.Receivables or ProjectWorkbookSheet.Collections or ProjectWorkbookSheet.Payables or ProjectWorkbookSheet.Payments or ProjectWorkbookSheet.Invoices or ProjectWorkbookSheet.Deductions)) return;
        if (row.PresentKeys.Contains("amount") && decimal.TryParse(row.Values.GetValueOrDefault("amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount <= 0m)
            errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/金额", "金额必须大于零。", row.Values.GetValueOrDefault("amount")));
        if (row.PresentKeys.Contains("gross_amount") && decimal.TryParse(row.Values.GetValueOrDefault("gross_amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var gross) && gross <= 0m)
            errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/含税金额", "金额必须大于零。", row.Values.GetValueOrDefault("gross_amount")));

        var existing = await FindExistingAsync(sheet, row, cancellationToken);
        var projectNumber = row.Values.GetValueOrDefault("project_number");
        var companyCode = row.Values.GetValueOrDefault("legal_entity_code");
        if (string.IsNullOrWhiteSpace(companyCode))
        {
            var legalEntityId = existing switch
            {
                ReceivableEntry item => item.LegalEntityId,
                CollectionEntry item => item.LegalEntityId,
                PayableEntry item => item.LegalEntityId,
                PaymentEntry item => item.LegalEntityId,
                InvoiceEntry item => item.LegalEntityId,
                FinanceSettlement item => item.LegalEntityId,
                FinanceCashEntry item => item.LegalEntityId,
                FinanceInvoice item => item.LegalEntityId,
                FinanceDeduction item => item.Settlement.LegalEntityId,
                _ => Guid.Empty
            };
            if (legalEntityId != Guid.Empty)
                companyCode = await db.LegalEntities.Where(item => item.Id == legalEntityId).Select(item => item.Code).SingleAsync(cancellationToken);
        }
        var projectInFile = workbook.Sheets.FirstOrDefault(item => item.Sheet == ProjectWorkbookSheet.ProjectMaster)?.Rows
            .FirstOrDefault(item => item.Values.GetValueOrDefault("project_number") == projectNumber);
        var companyIdForCode = string.IsNullOrWhiteSpace(companyCode)
            ? Guid.Empty
            : await db.LegalEntities.Where(item => item.Code == companyCode).Select(item => item.Id).SingleOrDefaultAsync(cancellationToken);
        var companyInFile = projectInFile is not null && companyIdForCode != Guid.Empty
            && ParseGuidList(projectInFile.Values.GetValueOrDefault("legal_entity_ids")).Contains(companyIdForCode);
        if (!string.IsNullOrWhiteSpace(companyCode) && !companyInFile && !await db.ProjectLegalEntities.AnyAsync(item => item.Project.ProjectNumber == projectNumber && item.LegalEntity.Code == companyCode, cancellationToken))
            errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/签约公司编码", "签约公司不存在或未关联到当前项目。", companyCode));

        if (sheet.Sheet is ProjectWorkbookSheet.Payables or ProjectWorkbookSheet.Payments && string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("partner_number")))
            errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/合作单位编号", "应付和付款必须指定合作单位。", null));
        if (!string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("partner_number")) && !await db.BusinessPartners.AnyAsync(item => item.PartnerNumber == row.Values["partner_number"] && item.IsActive, cancellationToken))
            errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/合作单位编号", "合作单位不存在或已停用。", row.Values["partner_number"]));

        if (sheet.Sheet is ProjectWorkbookSheet.Collections or ProjectWorkbookSheet.Payments && row.PresentKeys.Contains("account_id"))
        {
            if (!Guid.TryParse(row.Values.GetValueOrDefault("account_id"), out var accountId)
                || !await db.FinancialAccounts.AnyAsync(item => item.Id == accountId && item.IsActive && item.LegalEntity.Code == companyCode, cancellationToken))
                errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/账户ID", "资金账户不存在、已停用或不属于当前签约公司。", row.Values.GetValueOrDefault("account_id")));
        }

        var receivableInFile = workbook.Sheets.FirstOrDefault(item => item.Sheet == ProjectWorkbookSheet.Receivables)?.Rows.Any(item => item.Values.GetValueOrDefault("_system_id") == row.Values.GetValueOrDefault("receivable_id") && item.Values.GetValueOrDefault("project_number") == projectNumber) == true;
        if (sheet.Sheet == ProjectWorkbookSheet.Collections && row.PresentKeys.Contains("receivable_id") && !string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("receivable_id"))
            && !receivableInFile && (!Guid.TryParse(row.Values.GetValueOrDefault("receivable_id"), out var receivableId)
                || !await db.FinanceSettlements.AnyAsync(item => item.Id == receivableId && item.Project!.ProjectNumber == projectNumber && item.Direction == LedgerDirection.Receivable && item.Status == LedgerRecordStatus.Active, cancellationToken)
                && !await db.ReceivableEntries.AnyAsync(item => item.Id == receivableId && item.Project.ProjectNumber == projectNumber && !item.IsVoided, cancellationToken)))
            errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/应收系统ID", "应收记录不存在、已作废或不属于当前项目。", row.Values.GetValueOrDefault("receivable_id")));
        var payableInFile = workbook.Sheets.FirstOrDefault(item => item.Sheet == ProjectWorkbookSheet.Payables)?.Rows.Any(item => item.Values.GetValueOrDefault("_system_id") == row.Values.GetValueOrDefault("payable_id") && item.Values.GetValueOrDefault("project_number") == projectNumber) == true;
        if (sheet.Sheet == ProjectWorkbookSheet.Payments && row.PresentKeys.Contains("payable_id") && !string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("payable_id"))
            && !payableInFile && (!Guid.TryParse(row.Values.GetValueOrDefault("payable_id"), out var payableId)
                || !await db.FinanceSettlements.AnyAsync(item => item.Id == payableId && item.Project!.ProjectNumber == projectNumber && item.Direction == LedgerDirection.Payable && item.Status == LedgerRecordStatus.Active, cancellationToken)
                && !await db.PayableEntries.AnyAsync(item => item.Id == payableId && item.Project.ProjectNumber == projectNumber && !item.IsVoided, cancellationToken)))
            errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/应付系统ID", "应付记录不存在、已作废或不属于当前项目。", row.Values.GetValueOrDefault("payable_id")));
        if (sheet.Sheet == ProjectWorkbookSheet.Deductions && row.PresentKeys.Contains("settlement_id") && !string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("settlement_id"))
            && (!Guid.TryParse(row.Values.GetValueOrDefault("settlement_id"), out var settlementId)
                || !await db.FinanceSettlements.AnyAsync(item => item.Id == settlementId && item.Project!.ProjectNumber == projectNumber && item.Status == LedgerRecordStatus.Active, cancellationToken)))
            errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/结算系统ID", "结算记录不存在、已作废或不属于当前项目。", row.Values.GetValueOrDefault("settlement_id")));
    }

    private async Task ValidateExistingAsync(ParsedSheet sheet, ParsedRow row, ImportMode mode, List<ImportErrorDto> errors, CancellationToken cancellationToken)
    {
        var existing = await FindExistingAsync(sheet, row, cancellationToken);
        if (existing is not null && mode == ImportMode.New) errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/系统ID", "仅新增模式不能覆盖已有记录。", row.Values.GetValueOrDefault("_system_id")));
        if (existing is null && mode == ImportMode.Update) errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/系统ID", "仅更新模式找不到已有记录。", row.Values.GetValueOrDefault("_system_id")));
        if (existing is not null && !await ExistingMatchesBusinessKeysAsync(sheet.Sheet, row, existing, cancellationToken))
            errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/系统ID", "系统 ID 与项目或父级业务键不一致。", row.Values.GetValueOrDefault("_system_id")));
        if (existing is not null && row.Values.TryGetValue("_concurrency_stamp", out var stamp) && Guid.TryParse(stamp, out var expected) && existing switch
            {
                Project project => project.ConcurrencyStamp != expected,
                Contract contract => contract.ConcurrencyStamp != expected,
                ContractLineItem line => line.ConcurrencyStamp != expected,
                ProjectConstructionRecord construction => construction.ConcurrencyStamp != expected,
                StageResult stageResult => stageResult.ConcurrencyStamp != expected,
                ReceivableEntry receivable => receivable.ConcurrencyStamp != expected,
                CollectionEntry collection => collection.ConcurrencyStamp != expected,
                PayableEntry payable => payable.ConcurrencyStamp != expected,
                PaymentEntry payment => payment.ConcurrencyStamp != expected,
                InvoiceEntry invoice => invoice.ConcurrencyStamp != expected,
                FinanceCashEntry cash => cash.ConcurrencyStamp != expected,
                FinanceInvoice invoice => invoice.ConcurrencyStamp != expected,
                FinanceDeduction deduction => deduction.ConcurrencyStamp != expected,
                FinanceSettlement settlement => settlement.ConcurrencyStamp != expected,
                _ => false
            })
            errors.Add(new ImportErrorDto(row.RowNumber, $"{sheet.WorksheetName}/并发版本", "记录已被其他用户修改，请重新导出。", stamp));
    }

    private async Task<bool> ExistingMatchesBusinessKeysAsync(
        ProjectWorkbookSheet sheet,
        ParsedRow row,
        object existing,
        CancellationToken cancellationToken)
    {
        var projectNumber = row.Values.GetValueOrDefault("project_number");
        var contractNumber = row.Values.GetValueOrDefault("contract_number");
        var projectId = ExpectedGuid(row, "_project_system_id");
        var contractId = ExpectedGuid(row, "_contract_system_id");
        var id = existing switch
        {
            Project item => item.Id,
            Contract item => item.Id,
            ContractLineItem item => item.Id,
            ProjectMilestone item => item.Id,
            ProjectAssignment item => item.Id,
            ProjectPartner item => item.Id,
            ProjectConstructionRecord item => item.Id,
            StageResult item => item.Id,
            ReceivableEntry item => item.Id,
            CollectionEntry item => item.Id,
            PayableEntry item => item.Id,
            PaymentEntry item => item.Id,
            InvoiceEntry item => item.Id,
            FinanceCashEntry item => item.Id,
            FinanceInvoice item => item.Id,
            FinanceDeduction item => item.Id,
            FinanceSettlement item => item.Id,
            _ => Guid.Empty
        };
        if (id == Guid.Empty) return false;

        return sheet switch
        {
            ProjectWorkbookSheet.ProjectMaster => existing is Project project
                && string.Equals(project.ProjectNumber, projectNumber, StringComparison.Ordinal)
                && MatchesOptionalGuid(row, "_project_system_id", project.Id),
            ProjectWorkbookSheet.Contracts => await db.Contracts.AnyAsync(item => item.Id == id
                && item.Project.ProjectNumber == projectNumber
                && item.ContractNumber == contractNumber
                && (!projectId.HasValue || item.ProjectId == projectId.Value), cancellationToken),
            ProjectWorkbookSheet.QuantityLines => await db.ContractLineItems.AnyAsync(item => item.Id == id
                && item.Contract.Project.ProjectNumber == projectNumber
                && item.Contract.ContractNumber == contractNumber
                && (!projectId.HasValue || item.Contract.ProjectId == projectId.Value)
                && (!contractId.HasValue || item.ContractId == contractId.Value), cancellationToken),
            ProjectWorkbookSheet.Milestones => await db.ProjectMilestones.AnyAsync(item => item.Id == id && item.Project.ProjectNumber == projectNumber && (!projectId.HasValue || item.ProjectId == projectId.Value), cancellationToken),
            ProjectWorkbookSheet.Assignments => await db.ProjectAssignments.AnyAsync(item => item.Id == id && item.Project.ProjectNumber == projectNumber && (!projectId.HasValue || item.ProjectId == projectId.Value), cancellationToken),
            ProjectWorkbookSheet.Partners => await db.ProjectPartners.AnyAsync(item => item.Id == id && item.Project.ProjectNumber == projectNumber && (!projectId.HasValue || item.ProjectId == projectId.Value) && (!contractId.HasValue || item.ContractId == contractId.Value), cancellationToken),
            ProjectWorkbookSheet.Construction => await db.ProjectConstructionRecords.AnyAsync(item => item.Id == id && item.Project.ProjectNumber == projectNumber && (!projectId.HasValue || item.ProjectId == projectId.Value), cancellationToken),
            ProjectWorkbookSheet.StageResults => await db.StageResults.AnyAsync(item => item.Id == id && item.Project.ProjectNumber == projectNumber && (!projectId.HasValue || item.ProjectId == projectId.Value) && (!contractId.HasValue || item.ContractId == contractId.Value), cancellationToken),
            ProjectWorkbookSheet.Receivables => existing is FinanceSettlement
                ? await db.FinanceSettlements.AnyAsync(item => item.Id == id && item.Direction == LedgerDirection.Receivable && item.Project!.ProjectNumber == projectNumber
                    && (!projectId.HasValue || item.ProjectId == projectId.Value) && (!contractId.HasValue || item.ContractId == contractId.Value), cancellationToken)
                : await db.ReceivableEntries.AnyAsync(item => item.Id == id && item.Project.ProjectNumber == projectNumber && (!projectId.HasValue || item.ProjectId == projectId.Value) && (!contractId.HasValue || item.ContractId == contractId.Value), cancellationToken),
            ProjectWorkbookSheet.Collections => existing is FinanceCashEntry
                ? await db.FinanceCashEntries.AnyAsync(item => item.Id == id
                    && item.Direction == LedgerDirection.Receivable
                    && item.Allocations.Any(allocation => allocation.Project!.ProjectNumber == projectNumber
                        && (!projectId.HasValue || allocation.ProjectId == projectId.Value)
                        && (!contractId.HasValue || allocation.ContractId == contractId.Value)), cancellationToken)
                : await db.CollectionEntries.AnyAsync(item => item.Id == id && item.Project.ProjectNumber == projectNumber && (!projectId.HasValue || item.ProjectId == projectId.Value) && (!contractId.HasValue || item.ContractId == contractId.Value), cancellationToken),
            ProjectWorkbookSheet.Payables => existing is FinanceSettlement
                ? await db.FinanceSettlements.AnyAsync(item => item.Id == id && item.Direction == LedgerDirection.Payable && item.Project!.ProjectNumber == projectNumber
                    && (!projectId.HasValue || item.ProjectId == projectId.Value) && (!contractId.HasValue || item.ContractId == contractId.Value), cancellationToken)
                : await db.PayableEntries.AnyAsync(item => item.Id == id && item.Project.ProjectNumber == projectNumber && (!projectId.HasValue || item.ProjectId == projectId.Value) && (!contractId.HasValue || item.ContractId == contractId.Value), cancellationToken),
            ProjectWorkbookSheet.Payments => existing is FinanceCashEntry
                ? await db.FinanceCashEntries.AnyAsync(item => item.Id == id
                    && item.Direction == LedgerDirection.Payable
                    && item.Allocations.Any(allocation => allocation.Project!.ProjectNumber == projectNumber
                        && (!projectId.HasValue || allocation.ProjectId == projectId.Value)
                        && (!contractId.HasValue || allocation.ContractId == contractId.Value)), cancellationToken)
                : await db.PaymentEntries.AnyAsync(item => item.Id == id && item.Project.ProjectNumber == projectNumber && (!projectId.HasValue || item.ProjectId == projectId.Value) && (!contractId.HasValue || item.ContractId == contractId.Value), cancellationToken),
            ProjectWorkbookSheet.Invoices => existing is FinanceInvoice
                ? await db.FinanceInvoices.AnyAsync(item => item.Id == id && item.Allocations.Any(allocation => allocation.Project!.ProjectNumber == projectNumber
                    && (!projectId.HasValue || allocation.ProjectId == projectId.Value)
                    && (!contractId.HasValue || allocation.ContractId == contractId.Value)), cancellationToken)
                : await db.InvoiceEntries.AnyAsync(item => item.Id == id && item.Project.ProjectNumber == projectNumber && (!projectId.HasValue || item.ProjectId == projectId.Value) && (!contractId.HasValue || item.ContractId == contractId.Value), cancellationToken),
            ProjectWorkbookSheet.Deductions => existing is FinanceDeduction
                && await db.FinanceDeductions.AnyAsync(item => item.Id == id && item.Settlement.Project!.ProjectNumber == projectNumber
                    && (!projectId.HasValue || item.Settlement.ProjectId == projectId.Value)
                    && (!contractId.HasValue || item.Settlement.ContractId == contractId.Value), cancellationToken),
            _ => true
        };
    }

    private static bool MatchesOptionalGuid(ParsedRow row, string key, Guid? actual) =>
        !row.PresentKeys.Contains(key)
        || string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault(key))
        || Guid.TryParse(row.Values.GetValueOrDefault(key), out var expected) && actual == expected;

    private static Guid? ExpectedGuid(ParsedRow row, string key) =>
        Guid.TryParse(row.Values.GetValueOrDefault(key), out var value) ? value : null;

    private async Task<bool> IsRowChangedAsync(ParsedSheet sheet, ParsedRow row, object existing, CancellationToken cancellationToken)
    {
        var current = await CurrentValuesAsync(sheet.Sheet, existing, cancellationToken);
        foreach (var field in ProjectWorkbookCatalog.Get(sheet.Sheet).Fields.Where(field => field.CanImport && !field.IsHidden && row.PresentKeys.Contains(field.Key)))
        {
            if (!string.Equals(NormalizeComparable(row.Values.GetValueOrDefault(field.Key)), NormalizeComparable(current.GetValueOrDefault(field.Key)), StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private async Task<Dictionary<string, object?>> CurrentValuesAsync(ProjectWorkbookSheet sheet, object existing, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        switch (sheet)
        {
            case ProjectWorkbookSheet.ProjectMaster when existing is Project project:
                values["project_number"] = project.ProjectNumber; values["project_name"] = project.Name; values["parent_project"] = project.ParentProjectName;
                values["general_contractor"] = project.GeneralContractorName; values["general_contractor_contact"] = project.GeneralContractorContact; values["general_contractor_phone"] = project.GeneralContractorPhone;
                values["responsible_user_id"] = project.ResponsibleUserId; values["department_id"] = project.DepartmentId?.ToString(); values["branch_id"] = project.BranchId?.ToString(); values["stage"] = project.Stage.ToString(); values["contract_signing_status"] = project.ContractSigningStatus.ToString(); values["affiliation_type"] = project.AffiliationType.ToString(); values["actual_start_date"] = project.ActualStartDate; values["actual_completion_date"] = project.ActualCompletionDate; values["is_active"] = project.IsActive; values["notes"] = project.Notes;
                values["legal_entity_ids"] = string.Join(",", await db.ProjectLegalEntities.Where(item => item.ProjectId == project.Id).OrderBy(item => item.LegalEntityId).Select(item => item.LegalEntityId).ToListAsync(cancellationToken));
                break;
            case ProjectWorkbookSheet.Contracts when existing is Contract contract:
                values["project_number"] = await db.Projects.Where(item => item.Id == contract.ProjectId).Select(item => item.ProjectNumber).SingleAsync(cancellationToken); values["contract_number"] = contract.ContractNumber; values["name"] = contract.Name; values["contract_type"] = contract.ContractType.ToString(); values["allocation_mode"] = contract.AllocationMode.ToString(); values["counterparty_name"] = contract.CounterpartyName; values["signed_date"] = contract.SignedDate; values["total_amount"] = contract.TotalAmount; values["is_active"] = contract.IsActive; values["notes"] = contract.Notes;
                break;
            case ProjectWorkbookSheet.QuantityLines when existing is ContractLineItem line:
                var lineParent = await db.Contracts.Where(item => item.Id == line.ContractId).Select(item => new { item.ProjectId, item.ContractNumber }).SingleAsync(cancellationToken); values["project_number"] = await db.Projects.Where(item => item.Id == lineParent.ProjectId).Select(item => item.ProjectNumber).SingleAsync(cancellationToken); values["contract_number"] = lineParent.ContractNumber; values["code"] = line.Code; values["name"] = line.Name; values["unit"] = line.Unit; values["estimated_quantity"] = line.EstimatedQuantity; values["estimated_unit_price"] = line.EstimatedUnitPrice; values["settled_quantity"] = line.SettledQuantity; values["settled_unit_price"] = line.SettledUnitPrice; values["is_settlement_confirmed"] = line.IsSettlementConfirmed; values["notes"] = line.Notes;
                break;
            case ProjectWorkbookSheet.Milestones when existing is ProjectMilestone milestone:
                values["project_number"] = await db.Projects.Where(item => item.Id == milestone.ProjectId).Select(item => item.ProjectNumber).SingleAsync(cancellationToken); values["name"] = milestone.Name; values["planned_date"] = milestone.PlannedDate; values["actual_date"] = milestone.ActualDate; values["is_completed"] = milestone.IsCompleted; values["sort_order"] = milestone.SortOrder; values["notes"] = milestone.Notes;
                break;
            case ProjectWorkbookSheet.Assignments when existing is ProjectAssignment assignment:
                values["project_number"] = await db.Projects.Where(item => item.Id == assignment.ProjectId).Select(item => item.ProjectNumber).SingleAsync(cancellationToken); values["user_id"] = assignment.UserId; values["assignment_type"] = assignment.AssignmentType.ToString(); values["is_active"] = assignment.IsActive; values["notes"] = assignment.Notes;
                break;
            case ProjectWorkbookSheet.Partners when existing is ProjectPartner partner:
                var partnerValues = await db.ProjectPartners.Where(item => item.Id == partner.Id).Select(item => new { ProjectNumber = item.Project.ProjectNumber, PartnerNumber = item.Partner.PartnerNumber, ContractNumber = item.Contract == null ? null : item.Contract.ContractNumber }).SingleAsync(cancellationToken); values["project_number"] = partnerValues.ProjectNumber; values["partner_number"] = partnerValues.PartnerNumber; values["contract_number"] = partnerValues.ContractNumber; values["role_type"] = partner.RoleType.ToString(); values["is_primary"] = partner.IsPrimary; values["is_active"] = partner.IsActive; values["notes"] = partner.Notes;
                break;
            case ProjectWorkbookSheet.Construction when existing is ProjectConstructionRecord construction:
                var constructionValues = await db.ProjectConstructionRecords.Where(item => item.Id == construction.Id).Select(item => new { ProjectNumber = item.Project.ProjectNumber, EquipmentNumber = item.Equipment == null ? null : item.Equipment.EquipmentNumber, CrewNumber = item.CrewBusinessPartner == null ? null : item.CrewBusinessPartner.PartnerNumber, FromNumber = item.TransferFromProject == null ? null : item.TransferFromProject.ProjectNumber, ToNumber = item.TransferToProject == null ? null : item.TransferToProject.ProjectNumber }).SingleAsync(cancellationToken); values["project_number"] = constructionValues.ProjectNumber; values["equipment_number"] = constructionValues.EquipmentNumber; values["crew_partner_number"] = constructionValues.CrewNumber; values["transfer_from_project_number"] = constructionValues.FromNumber; values["transfer_to_project_number"] = constructionValues.ToNumber; values["record_type"] = construction.RecordType.ToString(); values["entry_date"] = construction.EntryDate; values["exit_date"] = construction.ExitDate; values["stop_days"] = construction.StopDays; values["is_draft"] = construction.IsDraft; values["show_in_project_overview"] = construction.ShowInProjectOverview; values["notes"] = construction.Notes;
                break;
            case ProjectWorkbookSheet.StageResults when existing is StageResult stage:
                values["project_number"] = await db.Projects.Where(item => item.Id == stage.ProjectId).Select(item => item.ProjectNumber).SingleAsync(cancellationToken); values["contract_number"] = stage.ContractId.HasValue ? await db.Contracts.Where(item => item.Id == stage.ContractId.Value).Select(item => item.ContractNumber).SingleAsync(cancellationToken) : null; values["title"] = stage.Title; values["result_type"] = stage.ResultType.ToString(); values["status"] = stage.Status.ToString(); values["result_date"] = stage.ResultDate; values["quality_result"] = stage.QualityResult.ToString(); values["description"] = stage.Description;
                break;
            default:
                return await FinanceCurrentValuesAsync(sheet, existing, cancellationToken);
        }
        return values;
    }

    private async Task<Dictionary<string, object?>> FinanceCurrentValuesAsync(ProjectWorkbookSheet sheet, object existing, CancellationToken cancellationToken)
    {
        var cashAllocation = existing is FinanceCashEntry cash
            ? cash.Allocations.FirstOrDefault() ?? await db.FinanceCashAllocations.FirstOrDefaultAsync(item => item.CashEntryId == cash.Id, cancellationToken)
            : null;
        var invoiceAllocation = existing is FinanceInvoice invoice
            ? invoice.Allocations.FirstOrDefault() ?? await db.FinanceInvoiceAllocations.FirstOrDefaultAsync(item => item.InvoiceId == invoice.Id, cancellationToken)
            : null;
        var deductionSettlement = existing is FinanceDeduction deduction ? deduction.Settlement : null;
        var projectId = existing switch { ReceivableEntry item => item.ProjectId, CollectionEntry item => item.ProjectId, PayableEntry item => item.ProjectId, PaymentEntry item => item.ProjectId, InvoiceEntry item => item.ProjectId, FinanceSettlement item => item.ProjectId ?? Guid.Empty, FinanceCashEntry => cashAllocation?.ProjectId ?? Guid.Empty, FinanceInvoice => invoiceAllocation?.ProjectId ?? Guid.Empty, FinanceDeduction => deductionSettlement?.ProjectId ?? Guid.Empty, _ => Guid.Empty };
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { ["project_number"] = await db.Projects.Where(item => item.Id == projectId).Select(item => item.ProjectNumber).SingleAsync(cancellationToken) };
        var contractId = existing switch { ReceivableEntry item => item.ContractId, CollectionEntry item => item.ContractId, PayableEntry item => item.ContractId, PaymentEntry item => item.ContractId, InvoiceEntry item => item.ContractId, FinanceSettlement item => item.ContractId, FinanceCashEntry => cashAllocation?.ContractId, FinanceInvoice => invoiceAllocation?.ContractId, FinanceDeduction => deductionSettlement?.ContractId, _ => null };
        values["contract_number"] = contractId.HasValue ? await db.Contracts.Where(item => item.Id == contractId.Value).Select(item => item.ContractNumber).SingleAsync(cancellationToken) : null;
        var companyId = existing switch { ReceivableEntry item => item.LegalEntityId, CollectionEntry item => item.LegalEntityId, PayableEntry item => item.LegalEntityId, PaymentEntry item => item.LegalEntityId, InvoiceEntry item => item.LegalEntityId, FinanceSettlement item => item.LegalEntityId, FinanceCashEntry item => item.LegalEntityId, FinanceInvoice item => item.LegalEntityId, FinanceDeduction => deductionSettlement?.LegalEntityId ?? Guid.Empty, _ => Guid.Empty };
        values["legal_entity_code"] = await db.LegalEntities.Where(item => item.Id == companyId).Select(item => item.Code).SingleAsync(cancellationToken);
        var partnerId = existing switch { ReceivableEntry item => item.BusinessPartnerId, CollectionEntry item => item.BusinessPartnerId, PayableEntry item => item.BusinessPartnerId, PaymentEntry item => item.BusinessPartnerId, InvoiceEntry item => item.BusinessPartnerId, FinanceSettlement item => item.BusinessPartnerId, FinanceCashEntry item => item.BusinessPartnerId, FinanceInvoice item => item.BusinessPartnerId, FinanceDeduction => deductionSettlement?.BusinessPartnerId, _ => null };
        values["partner_number"] = partnerId.HasValue ? await db.BusinessPartners.Where(item => item.Id == partnerId.Value).Select(item => item.PartnerNumber).SingleAsync(cancellationToken) : null;
        switch (existing)
        {
            case ReceivableEntry item: values["source_type"] = item.SourceType.ToString(); values["entry_date"] = item.EntryDate; values["due_date"] = item.DueDate; values["amount"] = item.Amount; values["description"] = item.Description; values["is_voided"] = item.IsVoided; break;
            case CollectionEntry item: values["receivable_id"] = item.ReceivableEntryId?.ToString(); values["collection_date"] = item.CollectionDate; values["account_id"] = item.AccountId.ToString(); values["amount"] = item.Amount; values["payment_method"] = item.PaymentMethod.ToString(); values["notes"] = item.Notes; break;
            case PayableEntry item: values["source_type"] = item.SourceType.ToString(); values["entry_date"] = item.EntryDate; values["due_date"] = item.DueDate; values["amount"] = item.Amount; values["description"] = item.Description; values["is_voided"] = item.IsVoided; break;
            case PaymentEntry item: values["payable_id"] = item.PayableEntryId?.ToString(); values["payment_date"] = item.PaymentDate; values["account_id"] = item.AccountId.ToString(); values["amount"] = item.Amount; values["payment_method"] = item.PaymentMethod.ToString(); values["notes"] = item.Notes; break;
            case InvoiceEntry item: values["direction"] = item.Direction.ToString(); values["invoice_number"] = item.InvoiceNumber; values["invoice_date"] = item.InvoiceDate; values["invoice_type"] = item.InvoiceType; values["tax_rate"] = item.TaxRate; values["net_amount"] = item.NetAmount; values["tax_amount"] = item.TaxAmount; values["gross_amount"] = item.GrossAmount; values["status"] = item.Status.ToString(); break;
            case FinanceSettlement item:
                values["source_type"] = item.SourceType.ToString(); values["settlement_state"] = item.SettlementState.ToString(); values["entry_date"] = item.BusinessDate; values["original_amount"] = item.OriginalAmount; values["original_invoice_amount"] = item.OriginalInvoiceAmount; values["amount"] = item.OriginalAmount; values["description"] = item.Notes; values["is_voided"] = item.Status == LedgerRecordStatus.Voided; break;
            case FinanceCashEntry item when item.Direction == LedgerDirection.Receivable:
                values["receivable_id"] = cashAllocation?.SettlementId.ToString(); values["collection_date"] = item.BusinessDate; values["account_id"] = item.AccountId?.ToString(); values["amount"] = cashAllocation?.Amount ?? item.Amount; values["payment_method"] = item.PaymentMethod; values["notes"] = item.Notes; break;
            case FinanceCashEntry item:
                values["payable_id"] = cashAllocation?.SettlementId.ToString(); values["payment_date"] = item.BusinessDate; values["account_id"] = item.AccountId?.ToString(); values["amount"] = cashAllocation?.Amount ?? item.Amount; values["payment_method"] = item.PaymentMethod; values["notes"] = item.Notes; break;
            case FinanceInvoice item:
                values["direction"] = item.Direction == LedgerDirection.Receivable ? InvoiceDirection.Output.ToString() : InvoiceDirection.Input.ToString(); values["invoice_number"] = item.InvoiceNumber; values["invoice_date"] = item.InvoiceDate; values["invoice_type"] = item.InvoiceType; values["tax_rate"] = item.TaxRate; values["net_amount"] = item.NetAmount; values["tax_amount"] = item.TaxAmount; values["gross_amount"] = invoiceAllocation?.Amount ?? item.Amount; values["status"] = item.Status.ToString(); break;
            case FinanceDeduction item:
                values["settlement_id"] = item.SettlementId.ToString(); values["deduction_date"] = item.BusinessDate; values["amount"] = item.Amount; values["reduce_invoice_amount"] = item.ReduceInvoiceAmount; values["reason"] = item.Reason; values["status"] = item.Status.ToString(); break;
        }
        return values;
    }

    private static string? NormalizeComparable(object? value) => ConvertValue(value)?.Trim();

    private async Task<object?> FindExistingAsync(ParsedSheet sheet, ParsedRow row, CancellationToken cancellationToken)
    {
        var id = Guid.TryParse(row.Values.GetValueOrDefault("_system_id"), out var parsed) ? parsed : (Guid?)null;
        return sheet.Sheet switch
        {
            ProjectWorkbookSheet.ProjectMaster => id.HasValue ? await db.Projects.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken) : await db.Projects.SingleOrDefaultAsync(item => item.ProjectNumber == row.Values.GetValueOrDefault("project_number"), cancellationToken),
            ProjectWorkbookSheet.Contracts => id.HasValue ? await db.Contracts.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken) : await db.Contracts.SingleOrDefaultAsync(item => item.Project.ProjectNumber == row.Values.GetValueOrDefault("project_number") && item.ContractNumber == row.Values.GetValueOrDefault("contract_number"), cancellationToken),
            ProjectWorkbookSheet.QuantityLines => id.HasValue ? await db.ContractLineItems.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken) : await db.ContractLineItems.SingleOrDefaultAsync(item => item.Contract.Project.ProjectNumber == row.Values.GetValueOrDefault("project_number") && item.Contract.ContractNumber == row.Values.GetValueOrDefault("contract_number") && item.Code == row.Values.GetValueOrDefault("code"), cancellationToken),
            ProjectWorkbookSheet.Milestones => id.HasValue ? await db.ProjectMilestones.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken) : await db.ProjectMilestones.SingleOrDefaultAsync(item => item.Project.ProjectNumber == row.Values.GetValueOrDefault("project_number") && item.Name == row.Values.GetValueOrDefault("name"), cancellationToken),
            ProjectWorkbookSheet.Assignments => id.HasValue ? await db.ProjectAssignments.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken) : await db.ProjectAssignments.SingleOrDefaultAsync(item => item.Project.ProjectNumber == row.Values.GetValueOrDefault("project_number") && item.UserId == row.Values.GetValueOrDefault("user_id"), cancellationToken),
            ProjectWorkbookSheet.Partners => id.HasValue ? await db.ProjectPartners.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken) : await db.ProjectPartners.SingleOrDefaultAsync(item => item.Project.ProjectNumber == row.Values.GetValueOrDefault("project_number") && item.Partner.PartnerNumber == row.Values.GetValueOrDefault("partner_number"), cancellationToken),
            ProjectWorkbookSheet.Construction => id.HasValue ? await db.ProjectConstructionRecords.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken) : null,
            ProjectWorkbookSheet.StageResults => id.HasValue ? await db.StageResults.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken) : await db.StageResults.SingleOrDefaultAsync(item => item.Project.ProjectNumber == row.Values.GetValueOrDefault("project_number") && item.Title == row.Values.GetValueOrDefault("title") && item.ResultDate == ParseDate(row.Values.GetValueOrDefault("result_date")), cancellationToken),
            ProjectWorkbookSheet.Receivables => id.HasValue
                ? (object?)await db.FinanceSettlements.SingleOrDefaultAsync(item => item.Id == id.Value && item.Direction == LedgerDirection.Receivable, cancellationToken)
                    ?? await db.ReceivableEntries.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
                : null,
            ProjectWorkbookSheet.Collections => id.HasValue
                ? (object?)await db.FinanceCashEntries.Include(item => item.Allocations).SingleOrDefaultAsync(item => item.Id == id.Value && item.Direction == LedgerDirection.Receivable, cancellationToken)
                    ?? await db.CollectionEntries.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
                : null,
            ProjectWorkbookSheet.Payables => id.HasValue
                ? (object?)await db.FinanceSettlements.SingleOrDefaultAsync(item => item.Id == id.Value && item.Direction == LedgerDirection.Payable, cancellationToken)
                    ?? await db.PayableEntries.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
                : null,
            ProjectWorkbookSheet.Payments => id.HasValue
                ? (object?)await db.FinanceCashEntries.Include(item => item.Allocations).SingleOrDefaultAsync(item => item.Id == id.Value && item.Direction == LedgerDirection.Payable, cancellationToken)
                    ?? await db.PaymentEntries.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
                : null,
            ProjectWorkbookSheet.Invoices => await FindExistingInvoiceAsync(id, row, cancellationToken),
            ProjectWorkbookSheet.Deductions => id.HasValue ? await db.FinanceDeductions.Include(item => item.Settlement).SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken) : null,
            _ => null
        };
    }

    private async Task<object?> FindExistingInvoiceAsync(Guid? id, ParsedRow row, CancellationToken cancellationToken)
    {
        var central = id.HasValue
            ? await db.FinanceInvoices.Include(item => item.Allocations).SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
            : await db.FinanceInvoices.Include(item => item.Allocations).SingleOrDefaultAsync(item => item.InvoiceNumber == row.Values.GetValueOrDefault("invoice_number")
                && item.Allocations.Any(allocation => allocation.Project!.ProjectNumber == row.Values.GetValueOrDefault("project_number")), cancellationToken);
        if (central is not null) return central;
        return id.HasValue
            ? await db.InvoiceEntries.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
            : await db.InvoiceEntries.SingleOrDefaultAsync(item => item.Project.ProjectNumber == row.Values.GetValueOrDefault("project_number") && item.InvoiceNumber == row.Values.GetValueOrDefault("invoice_number"), cancellationToken);
    }

    private async Task WriteProjectsAsync(ParsedWorkbook workbook, ImportMode mode, bool blankMeansNoChange, CancellationToken cancellationToken)
    {
        var sheet = workbook.Sheets.FirstOrDefault(item => item.Sheet == ProjectWorkbookSheet.ProjectMaster);
        if (sheet is null) return;
        foreach (var row in sheet.Rows)
        {
            var project = await FindExistingAsync(sheet, row, cancellationToken) as Project;
            if (project is null)
            {
                project = new Project { Id = RequestedId(row), ProjectNumber = Required(row, "project_number"), Name = Required(row, "project_name") };
                db.Projects.Add(project);
            }
            await ApplyProjectAsync(project, row, blankMeansNoChange, cancellationToken);
        }
    }

    private async Task WriteContractsAsync(ParsedWorkbook workbook, ImportMode mode, bool blankMeansNoChange, CancellationToken cancellationToken)
    {
        var sheet = workbook.Sheets.FirstOrDefault(item => item.Sheet == ProjectWorkbookSheet.Contracts);
        if (sheet is null) return;
        foreach (var row in sheet.Rows)
        {
            var project = await db.Projects.SingleAsync(item => item.ProjectNumber == row.Values["project_number"], cancellationToken);
            var contract = await FindExistingAsync(sheet, row, cancellationToken) as Contract;
            if (contract is null)
            {
                contract = new Contract { Id = RequestedId(row), Project = project, ProjectId = project.Id, ContractNumber = Required(row, "contract_number"), Name = Required(row, "name") };
                db.Contracts.Add(contract);
            }
            Apply(contract, row, blankMeansNoChange);
        }
    }

    private async Task WriteQuantityLinesAsync(ParsedWorkbook workbook, ImportMode mode, bool blankMeansNoChange, CancellationToken cancellationToken)
    {
        var sheet = workbook.Sheets.FirstOrDefault(item => item.Sheet == ProjectWorkbookSheet.QuantityLines);
        if (sheet is null) return;
        foreach (var row in sheet.Rows)
        {
            var contract = await db.Contracts.SingleAsync(item => item.Project.ProjectNumber == row.Values["project_number"] && item.ContractNumber == row.Values["contract_number"], cancellationToken);
            var line = await FindExistingAsync(sheet, row, cancellationToken) as ContractLineItem;
            if (line is null)
            {
                line = new ContractLineItem { Id = RequestedId(row), Contract = contract, ContractId = contract.Id, Code = Required(row, "code"), Name = Required(row, "name"), Unit = Required(row, "unit") };
                db.ContractLineItems.Add(line);
            }
            Apply(line, row, blankMeansNoChange);
        }
    }

    private async Task WriteProjectDetailsAsync(ParsedWorkbook workbook, bool blankMeansNoChange, CancellationToken cancellationToken)
    {
        foreach (var sheet in workbook.Sheets)
        {
            foreach (var row in sheet.Rows)
            {
                var project = sheet.Sheet == ProjectWorkbookSheet.ProjectMaster ? null : await ResolveProjectAsync(row, cancellationToken);
                var existing = await FindExistingAsync(sheet, row, cancellationToken);
                switch (sheet.Sheet)
                {
                    case ProjectWorkbookSheet.Milestones:
                        var milestone = existing as ProjectMilestone ?? new ProjectMilestone { Id = RequestedId(row), Project = project!, ProjectId = project!.Id, Name = Required(row, "name") };
                        if (existing is null) db.ProjectMilestones.Add(milestone);
                        Set(row, "name", value => milestone.Name = value!, milestone.Name, blankMeansNoChange);
                        if (Has(row, "planned_date", blankMeansNoChange)) milestone.PlannedDate = ParseDate(row.Values["planned_date"]);
                        if (Has(row, "actual_date", blankMeansNoChange)) milestone.ActualDate = ParseDate(row.Values["actual_date"]);
                        if (HasNonBlank(row, "is_completed")) milestone.IsCompleted = ParseBoolean(row.Values["is_completed"]);
                        if (Has(row, "sort_order", blankMeansNoChange)) milestone.SortOrder = (int)(ParseDecimal(row.Values["sort_order"]) ?? 0m);
                        Set(row, "notes", value => milestone.Notes = value, milestone.Notes, blankMeansNoChange);
                        break;
                    case ProjectWorkbookSheet.Assignments:
                        var userId = row.Values.GetValueOrDefault("user_id");
                        if (string.IsNullOrWhiteSpace(userId) && existing is null) throw new InvalidOperationException("缺少必填字段：user_id");
                        userId ??= (existing as ProjectAssignment)?.UserId;
                        if (string.IsNullOrWhiteSpace(userId)) throw new InvalidOperationException("项目人员缺少定位账号。");
                        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken) ?? throw new InvalidOperationException($"项目人员账号不存在：{userId}");
                        var assignment = existing as ProjectAssignment ?? new ProjectAssignment { Id = RequestedId(row), Project = project!, ProjectId = project!.Id, User = user, UserId = user.Id };
                        if (existing is null) db.ProjectAssignments.Add(assignment);
                        assignment.User = user;
                        assignment.UserId = user.Id;
                        if (Has(row, "assignment_type", blankMeansNoChange) && Enum.TryParse<ProjectAssignmentType>(row.Values["assignment_type"], true, out var assignmentType)) assignment.AssignmentType = assignmentType;
                        if (HasNonBlank(row, "is_active")) assignment.IsActive = ParseBoolean(row.Values["is_active"]);
                        Set(row, "notes", value => assignment.Notes = value, assignment.Notes, blankMeansNoChange);
                        break;
                    case ProjectWorkbookSheet.Partners:
                        var partnerNumber = row.Values.GetValueOrDefault("partner_number");
                        if (string.IsNullOrWhiteSpace(partnerNumber) && existing is null) throw new InvalidOperationException("缺少必填字段：partner_number");
                        partnerNumber ??= existing is ProjectPartner existingPartner
                            ? await db.BusinessPartners.Where(item => item.Id == existingPartner.BusinessPartnerId).Select(item => item.PartnerNumber).SingleAsync(cancellationToken)
                            : null;
                        if (string.IsNullOrWhiteSpace(partnerNumber)) throw new InvalidOperationException("项目合作单位缺少定位编号。");
                        var partner = await db.BusinessPartners.SingleOrDefaultAsync(item => item.PartnerNumber == partnerNumber, cancellationToken) ?? throw new InvalidOperationException($"合作单位不存在：{partnerNumber}");
                        var projectPartner = existing as ProjectPartner ?? new ProjectPartner { Id = RequestedId(row), Project = project!, ProjectId = project!.Id, Partner = partner, BusinessPartnerId = partner.Id };
                        if (existing is null) db.ProjectPartners.Add(projectPartner);
                        projectPartner.Partner = partner;
                        projectPartner.BusinessPartnerId = partner.Id;
                        if (Has(row, "role_type", blankMeansNoChange) && Enum.TryParse<BusinessPartnerRoleType>(row.Values["role_type"], true, out var role)) projectPartner.RoleType = role;
                        if (HasNonBlank(row, "is_primary")) projectPartner.IsPrimary = ParseBoolean(row.Values["is_primary"]);
                        if (HasNonBlank(row, "is_active")) projectPartner.IsActive = ParseBoolean(row.Values["is_active"]);
                        if (row.PresentKeys.Contains("contract_number"))
                        {
                            projectPartner.Contract = await ResolveContractAsync(row, false, cancellationToken);
                            projectPartner.ContractId = projectPartner.Contract?.Id;
                        }
                        Set(row, "notes", value => projectPartner.Notes = value, projectPartner.Notes, blankMeansNoChange);
                        break;
                    case ProjectWorkbookSheet.Construction:
                        var construction = existing as ProjectConstructionRecord ?? new ProjectConstructionRecord { Id = RequestedId(row), Project = project!, ProjectId = project!.Id };
                        if (existing is null) db.ProjectConstructionRecords.Add(construction);
                        if (Has(row, "record_type", blankMeansNoChange) && Enum.TryParse<ProjectConstructionRecordType>(row.Values["record_type"], true, out var recordType)) construction.RecordType = recordType;
                        if (row.PresentKeys.Contains("equipment_number"))
                        {
                            construction.Equipment = string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("equipment_number"))
                                ? null
                                : await db.Equipment.SingleAsync(item => item.EquipmentNumber == row.Values["equipment_number"], cancellationToken);
                            construction.EquipmentId = construction.Equipment?.Id;
                        }
                        if (row.PresentKeys.Contains("crew_partner_number"))
                        {
                            construction.CrewBusinessPartner = string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("crew_partner_number"))
                                ? null
                                : await db.BusinessPartners.SingleAsync(item => item.PartnerNumber == row.Values["crew_partner_number"], cancellationToken);
                            construction.CrewBusinessPartnerId = construction.CrewBusinessPartner?.Id;
                        }
                        if (row.PresentKeys.Contains("transfer_from_project_number"))
                        {
                            construction.TransferFromProject = string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("transfer_from_project_number"))
                                ? null
                                : await db.Projects.SingleAsync(item => item.ProjectNumber == row.Values["transfer_from_project_number"], cancellationToken);
                            construction.TransferFromProjectId = construction.TransferFromProject?.Id;
                        }
                        if (row.PresentKeys.Contains("transfer_to_project_number"))
                        {
                            construction.TransferToProject = string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("transfer_to_project_number"))
                                ? null
                                : await db.Projects.SingleAsync(item => item.ProjectNumber == row.Values["transfer_to_project_number"], cancellationToken);
                            construction.TransferToProjectId = construction.TransferToProject?.Id;
                        }
                        if (Has(row, "entry_date", blankMeansNoChange)) construction.EntryDate = ParseDate(row.Values["entry_date"]);
                        if (Has(row, "exit_date", blankMeansNoChange)) construction.ExitDate = ParseDate(row.Values["exit_date"]);
                        if (Has(row, "stop_days", blankMeansNoChange)) construction.StopDays = (int)(ParseDecimal(row.Values["stop_days"]) ?? 0m);
                        if (HasNonBlank(row, "is_draft")) construction.IsDraft = ParseBoolean(row.Values["is_draft"]);
                        if (HasNonBlank(row, "show_in_project_overview")) construction.ShowInProjectOverview = ParseBoolean(row.Values["show_in_project_overview"]);
                        Set(row, "notes", value => construction.Notes = value, construction.Notes, blankMeansNoChange);
                        construction.ConcurrencyStamp = Guid.NewGuid();
                        break;
                    case ProjectWorkbookSheet.StageResults:
                        var stageResult = existing as StageResult ?? new StageResult { Id = RequestedId(row), Project = project!, ProjectId = project!.Id, Title = Required(row, "title"), ResultDate = ParseDate(row.Values["result_date"])!.Value };
                        if (existing is null) db.StageResults.Add(stageResult);
                        if (row.PresentKeys.Contains("contract_number"))
                        {
                            stageResult.Contract = await ResolveContractAsync(row, false, cancellationToken);
                            stageResult.ContractId = stageResult.Contract?.Id;
                        }
                        Set(row, "title", value => stageResult.Title = value!, stageResult.Title, blankMeansNoChange);
                        if (Has(row, "result_type", blankMeansNoChange) && Enum.TryParse<StageResultType>(row.Values["result_type"], true, out var resultType)) stageResult.ResultType = resultType;
                        if (Has(row, "status", blankMeansNoChange) && Enum.TryParse<StageResultStatus>(row.Values["status"], true, out var status)) stageResult.Status = status;
                        if (Has(row, "result_date", blankMeansNoChange)) stageResult.ResultDate = ParseDate(row.Values["result_date"])!.Value;
                        if (Has(row, "quality_result", blankMeansNoChange) && Enum.TryParse<QualityResult>(row.Values["quality_result"], true, out var quality)) stageResult.QualityResult = quality;
                        Set(row, "description", value => stageResult.Description = value, stageResult.Description, blankMeansNoChange);
                        stageResult.ConcurrencyStamp = Guid.NewGuid();
                        break;
                }
            }
        }
    }

    private async Task WriteFinanceAsync(ParsedWorkbook workbook, bool blankMeansNoChange, CancellationToken cancellationToken)
    {
        foreach (var sheet in workbook.Sheets.Where(item => item.Sheet is >= ProjectWorkbookSheet.Receivables and <= ProjectWorkbookSheet.Deductions))
        {
            foreach (var row in sheet.Rows)
            {
                var project = await ResolveProjectAsync(row, cancellationToken);
                var existing = await FindExistingAsync(sheet, row, cancellationToken);
                var contract = row.PresentKeys.Contains("contract_number")
                    ? await ResolveContractAsync(row, false, cancellationToken)
                    : await ExistingContractAsync(existing, cancellationToken);
                var company = row.PresentKeys.Contains("legal_entity_code")
                    ? await db.LegalEntities.SingleAsync(item => item.Code == row.Values["legal_entity_code"], cancellationToken)
                    : await ExistingLegalEntityAsync(existing, cancellationToken);
                var partner = row.PresentKeys.Contains("partner_number")
                    ? string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("partner_number")) ? null : await db.BusinessPartners.SingleAsync(item => item.PartnerNumber == row.Values["partner_number"], cancellationToken)
                    : await ExistingPartnerAsync(existing, cancellationToken);
                switch (sheet.Sheet)
                {
                    case ProjectWorkbookSheet.Receivables:
                        if (existing is null)
                        {
                            if (partner is null) throw new InvalidOperationException("应收必须指定合作单位。");
                            var amount = ParseDecimal(row.Values.GetValueOrDefault("original_amount")) ?? ParseDecimal(row.Values.GetValueOrDefault("amount")) ?? 0m;
                            var invoiceAmount = ParseDecimal(row.Values.GetValueOrDefault("original_invoice_amount")) ?? amount;
                            var state = Enum.TryParse<LedgerSettlementState>(row.Values.GetValueOrDefault("settlement_state"), true, out var parsedState) ? parsedState : LedgerSettlementState.Final;
                            db.FinanceSettlements.Add(new FinanceSettlement
                            {
                                Id = RequestedId(row), Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, SettlementState = state,
                                SourceType = ParseLedgerSourceType(row.Values.GetValueOrDefault("source_type")), Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner,
                                BusinessDate = ParseDate(row.Values["entry_date"])!.Value, OriginalAmount = amount, OriginalInvoiceAmount = invoiceAmount,
                                Notes = row.Values.GetValueOrDefault("description"), Status = HasNonBlank(row, "is_voided") && ParseBoolean(row.Values.GetValueOrDefault("is_voided")) ? LedgerRecordStatus.Voided : LedgerRecordStatus.Active
                            });
                            break;
                        }
                        if (existing is FinanceSettlement centralReceivable)
                        {
                            if (partner is null) throw new InvalidOperationException("应收必须指定合作单位。");
                            centralReceivable.Project = project; centralReceivable.ProjectId = project.Id; centralReceivable.Contract = contract; centralReceivable.ContractId = contract?.Id;
                            centralReceivable.LegalEntity = company; centralReceivable.LegalEntityId = company.Id; centralReceivable.BusinessPartner = partner; centralReceivable.BusinessPartnerId = partner.Id;
                            if (Has(row, "source_type", blankMeansNoChange)) centralReceivable.SourceType = ParseLedgerSourceType(row.Values.GetValueOrDefault("source_type"));
                            if (Has(row, "settlement_state", blankMeansNoChange) && Enum.TryParse<LedgerSettlementState>(row.Values.GetValueOrDefault("settlement_state"), true, out var receivableState)) centralReceivable.SettlementState = receivableState;
                            if (Has(row, "entry_date", blankMeansNoChange)) centralReceivable.BusinessDate = ParseDate(row.Values["entry_date"])!.Value;
                            if (Has(row, "original_amount", blankMeansNoChange)) centralReceivable.OriginalAmount = ParseDecimal(row.Values.GetValueOrDefault("original_amount")) ?? 0m;
                            else if (Has(row, "amount", blankMeansNoChange)) centralReceivable.OriginalAmount = ParseDecimal(row.Values.GetValueOrDefault("amount")) ?? 0m;
                            if (Has(row, "original_invoice_amount", blankMeansNoChange)) centralReceivable.OriginalInvoiceAmount = ParseDecimal(row.Values.GetValueOrDefault("original_invoice_amount")) ?? 0m;
                            Set(row, "description", value => centralReceivable.Notes = value, centralReceivable.Notes, blankMeansNoChange);
                            if (HasNonBlank(row, "is_voided")) centralReceivable.Status = ParseBoolean(row.Values.GetValueOrDefault("is_voided")) ? LedgerRecordStatus.Voided : LedgerRecordStatus.Active;
                            centralReceivable.UpdatedAt = DateTimeOffset.UtcNow;
                            centralReceivable.ConcurrencyStamp = Guid.NewGuid();
                            break;
                        }
                        var receivable = existing as ReceivableEntry ?? new ReceivableEntry { Id = RequestedId(row), Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner };
                        if (existing is null) db.ReceivableEntries.Add(receivable);
                        receivable.Project = project; receivable.ProjectId = project.Id; receivable.Contract = contract; receivable.ContractId = contract?.Id; receivable.LegalEntity = company; receivable.LegalEntityId = company.Id; receivable.BusinessPartner = partner; receivable.BusinessPartnerId = partner?.Id;
                        if (Has(row, "source_type", blankMeansNoChange) && Enum.TryParse<ReceivableSourceType>(row.Values["source_type"], true, out var receivableSource)) receivable.SourceType = receivableSource;
                        if (Has(row, "entry_date", blankMeansNoChange)) receivable.EntryDate = ParseDate(row.Values["entry_date"])!.Value;
                        if (Has(row, "due_date", blankMeansNoChange)) receivable.DueDate = ParseDate(row.Values.GetValueOrDefault("due_date"));
                        if (Has(row, "amount", blankMeansNoChange)) receivable.Amount = ParseDecimal(row.Values["amount"]) ?? 0m;
                        Set(row, "description", value => receivable.Description = value, receivable.Description, blankMeansNoChange);
                        if (HasNonBlank(row, "is_voided")) receivable.IsVoided = ParseBoolean(row.Values.GetValueOrDefault("is_voided"));
                        receivable.ConcurrencyStamp = Guid.NewGuid();
                        break;
                    case ProjectWorkbookSheet.Payables:
                        if (partner is null) throw new InvalidOperationException("应付必须指定合作单位。");
                        if (existing is null)
                        {
                            var amount = ParseDecimal(row.Values.GetValueOrDefault("original_amount")) ?? ParseDecimal(row.Values.GetValueOrDefault("amount")) ?? 0m;
                            var invoiceAmount = ParseDecimal(row.Values.GetValueOrDefault("original_invoice_amount")) ?? amount;
                            var state = Enum.TryParse<LedgerSettlementState>(row.Values.GetValueOrDefault("settlement_state"), true, out var parsedState) ? parsedState : LedgerSettlementState.Final;
                            db.FinanceSettlements.Add(new FinanceSettlement
                            {
                                Id = RequestedId(row), Scope = LedgerScope.External, Direction = LedgerDirection.Payable, SettlementState = state,
                                SourceType = ParseLedgerSourceType(row.Values.GetValueOrDefault("source_type")), Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner,
                                BusinessDate = ParseDate(row.Values["entry_date"])!.Value, OriginalAmount = amount, OriginalInvoiceAmount = invoiceAmount,
                                Notes = row.Values.GetValueOrDefault("description"), Status = HasNonBlank(row, "is_voided") && ParseBoolean(row.Values.GetValueOrDefault("is_voided")) ? LedgerRecordStatus.Voided : LedgerRecordStatus.Active
                            });
                            break;
                        }
                        if (existing is FinanceSettlement centralPayable)
                        {
                            centralPayable.Project = project; centralPayable.ProjectId = project.Id; centralPayable.Contract = contract; centralPayable.ContractId = contract?.Id;
                            centralPayable.LegalEntity = company; centralPayable.LegalEntityId = company.Id; centralPayable.BusinessPartner = partner; centralPayable.BusinessPartnerId = partner.Id;
                            if (Has(row, "source_type", blankMeansNoChange)) centralPayable.SourceType = ParseLedgerSourceType(row.Values.GetValueOrDefault("source_type"));
                            if (Has(row, "settlement_state", blankMeansNoChange) && Enum.TryParse<LedgerSettlementState>(row.Values.GetValueOrDefault("settlement_state"), true, out var payableState)) centralPayable.SettlementState = payableState;
                            if (Has(row, "entry_date", blankMeansNoChange)) centralPayable.BusinessDate = ParseDate(row.Values["entry_date"])!.Value;
                            if (Has(row, "original_amount", blankMeansNoChange)) centralPayable.OriginalAmount = ParseDecimal(row.Values.GetValueOrDefault("original_amount")) ?? 0m;
                            else if (Has(row, "amount", blankMeansNoChange)) centralPayable.OriginalAmount = ParseDecimal(row.Values.GetValueOrDefault("amount")) ?? 0m;
                            if (Has(row, "original_invoice_amount", blankMeansNoChange)) centralPayable.OriginalInvoiceAmount = ParseDecimal(row.Values.GetValueOrDefault("original_invoice_amount")) ?? 0m;
                            Set(row, "description", value => centralPayable.Notes = value, centralPayable.Notes, blankMeansNoChange);
                            if (HasNonBlank(row, "is_voided")) centralPayable.Status = ParseBoolean(row.Values.GetValueOrDefault("is_voided")) ? LedgerRecordStatus.Voided : LedgerRecordStatus.Active;
                            centralPayable.UpdatedAt = DateTimeOffset.UtcNow;
                            centralPayable.ConcurrencyStamp = Guid.NewGuid();
                            break;
                        }
                        var payable = existing as PayableEntry ?? new PayableEntry { Id = RequestedId(row), Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner };
                        if (existing is null) db.PayableEntries.Add(payable);
                        payable.Project = project; payable.ProjectId = project.Id; payable.Contract = contract; payable.ContractId = contract?.Id; payable.LegalEntity = company; payable.LegalEntityId = company.Id; payable.BusinessPartner = partner; payable.BusinessPartnerId = partner.Id;
                        if (Has(row, "source_type", blankMeansNoChange) && Enum.TryParse<PayableSourceType>(row.Values["source_type"], true, out var payableSource)) payable.SourceType = payableSource;
                        if (Has(row, "entry_date", blankMeansNoChange)) payable.EntryDate = ParseDate(row.Values["entry_date"])!.Value;
                        if (Has(row, "due_date", blankMeansNoChange)) payable.DueDate = ParseDate(row.Values.GetValueOrDefault("due_date"));
                        if (Has(row, "amount", blankMeansNoChange)) payable.Amount = ParseDecimal(row.Values["amount"]) ?? 0m;
                        Set(row, "description", value => payable.Description = value, payable.Description, blankMeansNoChange);
                        if (HasNonBlank(row, "is_voided")) payable.IsVoided = ParseBoolean(row.Values.GetValueOrDefault("is_voided"));
                        payable.ConcurrencyStamp = Guid.NewGuid();
                        break;
                    case ProjectWorkbookSheet.Collections:
                        var collectionAccountId = row.PresentKeys.Contains("account_id")
                            ? Guid.Parse(Required(row, "account_id"))
                            : (existing as FinanceCashEntry)?.AccountId ?? (existing as CollectionEntry)?.AccountId ?? throw new InvalidOperationException("缺少必填字段：account_id");
                        var collectionAccount = await db.FinancialAccounts.SingleAsync(item => item.Id == collectionAccountId, cancellationToken);
                        if (existing is null)
                        {
                            if (partner is null) throw new InvalidOperationException("收款必须指定合作单位。");
                            var settlementId = await ResolveReceivableIdAsync(row, project.Id, cancellationToken) ?? throw new InvalidOperationException("收款必须关联应收结算。");
                            var cash = new FinanceCashEntry
                            {
                                Id = RequestedId(row), Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, CashType = LedgerCashType.Collection,
                                SourceType = LedgerSourceType.CentralLedger, LegalEntity = company, BusinessPartner = partner, Account = collectionAccount,
                                BusinessDate = ParseDate(row.Values["collection_date"])!.Value, Amount = ParseDecimal(row.Values["amount"]) ?? 0m,
                                PaymentMethod = row.Values.GetValueOrDefault("payment_method"), Notes = row.Values.GetValueOrDefault("notes")
                            };
                            cash.Allocations.Add(new FinanceCashAllocation { CashEntry = cash, SettlementId = settlementId, Project = project, Contract = contract, BusinessPartnerId = partner.Id, Amount = cash.Amount, AllocationOrder = 1 });
                            db.FinanceCashEntries.Add(cash);
                            db.AccountTransactions.Add(new AccountTransaction { Account = collectionAccount, Direction = AccountTransactionDirection.Inflow, SourceType = AccountTransactionSourceType.Collection, SourceId = cash.Id, TransactionDate = cash.BusinessDate, Amount = cash.Amount, Description = cash.Notes });
                            break;
                        }
                        if (existing is FinanceCashEntry existingCollection)
                        {
                            if (partner is null) throw new InvalidOperationException("收款必须指定合作单位。");
                            existingCollection.LegalEntity = company; existingCollection.LegalEntityId = company.Id; existingCollection.BusinessPartner = partner; existingCollection.BusinessPartnerId = partner.Id;
                            existingCollection.Account = collectionAccount; existingCollection.AccountId = collectionAccount.Id;
                            if (Has(row, "collection_date", blankMeansNoChange)) existingCollection.BusinessDate = ParseDate(row.Values["collection_date"])!.Value;
                            if (Has(row, "amount", blankMeansNoChange)) existingCollection.Amount = ParseDecimal(row.Values["amount"]) ?? 0m;
                            if (Has(row, "payment_method", blankMeansNoChange)) existingCollection.PaymentMethod = row.Values.GetValueOrDefault("payment_method");
                            Set(row, "notes", value => existingCollection.Notes = value, existingCollection.Notes, blankMeansNoChange);
                            var collectionSettlementId = Has(row, "receivable_id", blankMeansNoChange)
                                ? await ResolveReceivableIdAsync(row, project.Id, cancellationToken) ?? throw new InvalidOperationException("收款必须关联应收结算。")
                                : existingCollection.Allocations.Single().SettlementId;
                            db.FinanceCashAllocations.RemoveRange(existingCollection.Allocations);
                            existingCollection.Allocations.Clear();
                            db.FinanceCashAllocations.Add(new FinanceCashAllocation { CashEntry = existingCollection, SettlementId = collectionSettlementId, Project = project, Contract = contract, BusinessPartnerId = partner.Id, Amount = existingCollection.Amount, AllocationOrder = 1 });
                            existingCollection.UpdatedAt = DateTimeOffset.UtcNow;
                            existingCollection.ConcurrencyStamp = Guid.NewGuid();
                            var centralCollectionTransaction = await db.AccountTransactions.SingleAsync(item => item.SourceType == AccountTransactionSourceType.Collection && item.SourceId == existingCollection.Id, cancellationToken);
                            centralCollectionTransaction.Account = collectionAccount; centralCollectionTransaction.AccountId = collectionAccount.Id; centralCollectionTransaction.TransactionDate = existingCollection.BusinessDate; centralCollectionTransaction.Amount = existingCollection.Amount; centralCollectionTransaction.Description = existingCollection.Notes;
                            break;
                        }
                        var collection = existing as CollectionEntry ?? new CollectionEntry { Id = RequestedId(row), Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, Account = collectionAccount };
                        if (existing is null) db.CollectionEntries.Add(collection);
                        collection.Project = project; collection.ProjectId = project.Id; collection.Contract = contract; collection.ContractId = contract?.Id; collection.LegalEntity = company; collection.LegalEntityId = company.Id; collection.BusinessPartner = partner; collection.BusinessPartnerId = partner?.Id;
                        collection.Account = collectionAccount; collection.AccountId = collectionAccount.Id;
                        if (Has(row, "receivable_id", blankMeansNoChange)) collection.ReceivableEntryId = await ResolveReceivableIdAsync(row, project.Id, cancellationToken);
                        if (Has(row, "collection_date", blankMeansNoChange)) collection.CollectionDate = ParseDate(row.Values["collection_date"])!.Value;
                        if (Has(row, "amount", blankMeansNoChange)) collection.Amount = ParseDecimal(row.Values["amount"]) ?? 0m;
                        if (Has(row, "payment_method", blankMeansNoChange) && Enum.TryParse<PaymentMethod>(row.Values["payment_method"], true, out var collectionMethod)) collection.PaymentMethod = collectionMethod;
                        Set(row, "notes", value => collection.Notes = value, collection.Notes, blankMeansNoChange);
                        collection.ConcurrencyStamp = Guid.NewGuid();
                        var collectionCash = existing is null
                            ? new AccountTransaction { Direction = AccountTransactionDirection.Inflow, SourceType = AccountTransactionSourceType.Collection, SourceId = collection.Id }
                            : await db.AccountTransactions.SingleAsync(item => item.SourceType == AccountTransactionSourceType.Collection && item.SourceId == collection.Id, cancellationToken);
                        if (existing is null) db.AccountTransactions.Add(collectionCash);
                        collectionCash.Account = collectionAccount; collectionCash.AccountId = collectionAccount.Id; collectionCash.TransactionDate = collection.CollectionDate; collectionCash.Amount = collection.Amount; collectionCash.Description = collection.Notes;
                        break;
                    case ProjectWorkbookSheet.Payments:
                        if (partner is null) throw new InvalidOperationException("付款必须指定合作单位。");
                        var paymentAccountId = row.PresentKeys.Contains("account_id")
                            ? Guid.Parse(Required(row, "account_id"))
                            : (existing as FinanceCashEntry)?.AccountId ?? (existing as PaymentEntry)?.AccountId ?? throw new InvalidOperationException("缺少必填字段：account_id");
                        var paymentAccount = await db.FinancialAccounts.SingleAsync(item => item.Id == paymentAccountId, cancellationToken);
                        if (existing is null)
                        {
                            var settlementId = await ResolvePayableIdAsync(row, project.Id, cancellationToken) ?? throw new InvalidOperationException("付款必须关联应付结算。");
                            var cash = new FinanceCashEntry
                            {
                                Id = RequestedId(row), Scope = LedgerScope.External, Direction = LedgerDirection.Payable, CashType = LedgerCashType.Payment,
                                SourceType = LedgerSourceType.CentralLedger, LegalEntity = company, BusinessPartner = partner, Account = paymentAccount,
                                BusinessDate = ParseDate(row.Values["payment_date"])!.Value, Amount = ParseDecimal(row.Values["amount"]) ?? 0m,
                                PaymentMethod = row.Values.GetValueOrDefault("payment_method"), Notes = row.Values.GetValueOrDefault("notes")
                            };
                            cash.Allocations.Add(new FinanceCashAllocation { CashEntry = cash, SettlementId = settlementId, Project = project, Contract = contract, BusinessPartnerId = partner.Id, Amount = cash.Amount, AllocationOrder = 1 });
                            db.FinanceCashEntries.Add(cash);
                            db.AccountTransactions.Add(new AccountTransaction { Account = paymentAccount, Direction = AccountTransactionDirection.Outflow, SourceType = AccountTransactionSourceType.Payment, SourceId = cash.Id, TransactionDate = cash.BusinessDate, Amount = cash.Amount, Description = cash.Notes });
                            break;
                        }
                        if (existing is FinanceCashEntry existingPayment)
                        {
                            existingPayment.LegalEntity = company; existingPayment.LegalEntityId = company.Id; existingPayment.BusinessPartner = partner; existingPayment.BusinessPartnerId = partner.Id;
                            existingPayment.Account = paymentAccount; existingPayment.AccountId = paymentAccount.Id;
                            if (Has(row, "payment_date", blankMeansNoChange)) existingPayment.BusinessDate = ParseDate(row.Values["payment_date"])!.Value;
                            if (Has(row, "amount", blankMeansNoChange)) existingPayment.Amount = ParseDecimal(row.Values["amount"]) ?? 0m;
                            if (Has(row, "payment_method", blankMeansNoChange)) existingPayment.PaymentMethod = row.Values.GetValueOrDefault("payment_method");
                            Set(row, "notes", value => existingPayment.Notes = value, existingPayment.Notes, blankMeansNoChange);
                            var paymentSettlementId = Has(row, "payable_id", blankMeansNoChange)
                                ? await ResolvePayableIdAsync(row, project.Id, cancellationToken) ?? throw new InvalidOperationException("付款必须关联应付结算。")
                                : existingPayment.Allocations.Single().SettlementId;
                            db.FinanceCashAllocations.RemoveRange(existingPayment.Allocations);
                            existingPayment.Allocations.Clear();
                            db.FinanceCashAllocations.Add(new FinanceCashAllocation { CashEntry = existingPayment, SettlementId = paymentSettlementId, Project = project, Contract = contract, BusinessPartnerId = partner.Id, Amount = existingPayment.Amount, AllocationOrder = 1 });
                            existingPayment.UpdatedAt = DateTimeOffset.UtcNow;
                            existingPayment.ConcurrencyStamp = Guid.NewGuid();
                            var centralPaymentTransaction = await db.AccountTransactions.SingleAsync(item => item.SourceType == AccountTransactionSourceType.Payment && item.SourceId == existingPayment.Id, cancellationToken);
                            centralPaymentTransaction.Account = paymentAccount; centralPaymentTransaction.AccountId = paymentAccount.Id; centralPaymentTransaction.TransactionDate = existingPayment.BusinessDate; centralPaymentTransaction.Amount = existingPayment.Amount; centralPaymentTransaction.Description = existingPayment.Notes;
                            break;
                        }
                        var payment = existing as PaymentEntry ?? new PaymentEntry { Id = RequestedId(row), Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, Account = paymentAccount };
                        if (existing is null) db.PaymentEntries.Add(payment);
                        payment.Project = project; payment.ProjectId = project.Id; payment.Contract = contract; payment.ContractId = contract?.Id; payment.LegalEntity = company; payment.LegalEntityId = company.Id; payment.BusinessPartner = partner; payment.BusinessPartnerId = partner.Id;
                        payment.Account = paymentAccount; payment.AccountId = paymentAccount.Id;
                        if (Has(row, "payable_id", blankMeansNoChange)) payment.PayableEntryId = await ResolvePayableIdAsync(row, project.Id, cancellationToken);
                        if (Has(row, "payment_date", blankMeansNoChange)) payment.PaymentDate = ParseDate(row.Values["payment_date"])!.Value;
                        if (Has(row, "amount", blankMeansNoChange)) payment.Amount = ParseDecimal(row.Values["amount"]) ?? 0m;
                        if (Has(row, "payment_method", blankMeansNoChange) && Enum.TryParse<PaymentMethod>(row.Values["payment_method"], true, out var paymentMethod)) payment.PaymentMethod = paymentMethod;
                        Set(row, "notes", value => payment.Notes = value, payment.Notes, blankMeansNoChange);
                        payment.ConcurrencyStamp = Guid.NewGuid();
                        var paymentCash = existing is null
                            ? new AccountTransaction { Direction = AccountTransactionDirection.Outflow, SourceType = AccountTransactionSourceType.Payment, SourceId = payment.Id }
                            : await db.AccountTransactions.SingleAsync(item => item.SourceType == AccountTransactionSourceType.Payment && item.SourceId == payment.Id, cancellationToken);
                        if (existing is null) db.AccountTransactions.Add(paymentCash);
                        paymentCash.Account = paymentAccount; paymentCash.AccountId = paymentAccount.Id; paymentCash.TransactionDate = payment.PaymentDate; paymentCash.Amount = payment.Amount; paymentCash.Description = payment.Notes;
                        break;
                    case ProjectWorkbookSheet.Invoices:
                        if (existing is null)
                        {
                            if (partner is null) throw new InvalidOperationException("发票必须指定合作单位。");
                            var invoiceDirection = Enum.Parse<InvoiceDirection>(Required(row, "direction"), true) == InvoiceDirection.Output
                                ? LedgerDirection.Receivable
                                : LedgerDirection.Payable;
                            var invoiceAmount = ParseDecimal(row.Values["gross_amount"]) ?? 0m;
                            var centralInvoice = new FinanceInvoice
                            {
                                Id = RequestedId(row), Scope = LedgerScope.External, Direction = invoiceDirection, LegalEntity = company, BusinessPartner = partner,
                                InvoiceNumber = Required(row, "invoice_number"), InvoiceDate = ParseDate(row.Values["invoice_date"])!.Value,
                                InvoiceType = row.Values.GetValueOrDefault("invoice_type"), TaxRate = ParseDecimal(row.Values.GetValueOrDefault("tax_rate")),
                                NetAmount = ParseDecimal(row.Values.GetValueOrDefault("net_amount")), TaxAmount = ParseDecimal(row.Values.GetValueOrDefault("tax_amount")), Amount = invoiceAmount,
                                Status = ParseInvoiceRecordStatus(row.Values.GetValueOrDefault("status"))
                            };
                            foreach (var allocation in await BuildProjectInvoiceAllocationsAsync(project, contract, company, partner, invoiceDirection, invoiceAmount, cancellationToken))
                            {
                                var invoiceAllocation = allocation.InvoiceAllocation;
                                invoiceAllocation.Invoice = centralInvoice;
                                centralInvoice.Allocations.Add(invoiceAllocation);
                            }
                            db.FinanceInvoices.Add(centralInvoice);
                            break;
                        }
                        if (existing is FinanceInvoice existingInvoice)
                        {
                            if (partner is null) throw new InvalidOperationException("发票必须指定合作单位。");
                            existingInvoice.LegalEntity = company; existingInvoice.LegalEntityId = company.Id; existingInvoice.BusinessPartner = partner; existingInvoice.BusinessPartnerId = partner.Id;
                            if (Has(row, "direction", blankMeansNoChange))
                                existingInvoice.Direction = Enum.Parse<InvoiceDirection>(Required(row, "direction"), true) == InvoiceDirection.Output ? LedgerDirection.Receivable : LedgerDirection.Payable;
                            Set(row, "invoice_number", value => existingInvoice.InvoiceNumber = value!, existingInvoice.InvoiceNumber, blankMeansNoChange);
                            if (Has(row, "invoice_date", blankMeansNoChange)) existingInvoice.InvoiceDate = ParseDate(row.Values["invoice_date"])!.Value;
                            Set(row, "invoice_type", value => existingInvoice.InvoiceType = value, existingInvoice.InvoiceType, blankMeansNoChange);
                            if (Has(row, "tax_rate", blankMeansNoChange)) existingInvoice.TaxRate = ParseDecimal(row.Values.GetValueOrDefault("tax_rate"));
                            if (Has(row, "net_amount", blankMeansNoChange)) existingInvoice.NetAmount = ParseDecimal(row.Values.GetValueOrDefault("net_amount"));
                            if (Has(row, "tax_amount", blankMeansNoChange)) existingInvoice.TaxAmount = ParseDecimal(row.Values.GetValueOrDefault("tax_amount"));
                            if (Has(row, "gross_amount", blankMeansNoChange)) existingInvoice.Amount = ParseDecimal(row.Values["gross_amount"]) ?? 0m;
                            if (Has(row, "status", blankMeansNoChange)) existingInvoice.Status = ParseInvoiceRecordStatus(row.Values.GetValueOrDefault("status"));
                            var replacementAllocations = await BuildProjectInvoiceAllocationsAsync(project, contract, company, partner, existingInvoice.Direction, existingInvoice.Amount, cancellationToken, existingInvoice.Id);
                            db.FinanceInvoiceAllocations.RemoveRange(existingInvoice.Allocations);
                            existingInvoice.Allocations.Clear();
                            foreach (var allocation in replacementAllocations)
                            {
                                var invoiceAllocation = allocation.InvoiceAllocation;
                                invoiceAllocation.Invoice = existingInvoice;
                                db.FinanceInvoiceAllocations.Add(invoiceAllocation);
                            }
                            existingInvoice.UpdatedAt = DateTimeOffset.UtcNow;
                            existingInvoice.ConcurrencyStamp = Guid.NewGuid();
                            break;
                        }
                        var invoice = existing as InvoiceEntry ?? new InvoiceEntry { Id = RequestedId(row), Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, InvoiceNumber = Required(row, "invoice_number") };
                        if (existing is null) db.InvoiceEntries.Add(invoice);
                        invoice.Project = project; invoice.ProjectId = project.Id; invoice.Contract = contract; invoice.ContractId = contract?.Id; invoice.LegalEntity = company; invoice.LegalEntityId = company.Id; invoice.BusinessPartner = partner; invoice.BusinessPartnerId = partner?.Id;
                        Set(row, "invoice_number", value => invoice.InvoiceNumber = value!, invoice.InvoiceNumber, blankMeansNoChange);
                        if (Has(row, "direction", blankMeansNoChange) && Enum.TryParse<InvoiceDirection>(row.Values["direction"], true, out var direction)) invoice.Direction = direction;
                        if (Has(row, "invoice_date", blankMeansNoChange)) invoice.InvoiceDate = ParseDate(row.Values["invoice_date"])!.Value;
                        Set(row, "invoice_type", value => invoice.InvoiceType = value, invoice.InvoiceType, blankMeansNoChange);
                        if (Has(row, "tax_rate", blankMeansNoChange)) invoice.TaxRate = ParseDecimal(row.Values["tax_rate"]) ?? 0m;
                        if (Has(row, "net_amount", blankMeansNoChange)) invoice.NetAmount = ParseDecimal(row.Values["net_amount"]) ?? 0m;
                        if (Has(row, "tax_amount", blankMeansNoChange)) invoice.TaxAmount = ParseDecimal(row.Values["tax_amount"]) ?? 0m;
                        if (Has(row, "gross_amount", blankMeansNoChange)) invoice.GrossAmount = ParseDecimal(row.Values["gross_amount"]) ?? 0m;
                        if (Has(row, "status", blankMeansNoChange) && Enum.TryParse<InvoiceStatus>(row.Values["status"], true, out var invoiceStatus)) invoice.Status = invoiceStatus;
                        invoice.ConcurrencyStamp = Guid.NewGuid();
                        break;
                    case ProjectWorkbookSheet.Deductions:
                        var deductionSettlementId = Guid.Parse(Required(row, "settlement_id"));
                        var deductionSettlement = await db.FinanceSettlements.SingleAsync(item => item.Id == deductionSettlementId && item.ProjectId == project.Id, cancellationToken);
                        if (existing is null)
                        {
                            db.FinanceDeductions.Add(new FinanceDeduction
                            {
                                Id = RequestedId(row), Settlement = deductionSettlement, BusinessDate = ParseDate(row.Values["deduction_date"])!.Value,
                                Amount = ParseDecimal(row.Values["amount"]) ?? 0m, ReduceInvoiceAmount = ParseBoolean(row.Values.GetValueOrDefault("reduce_invoice_amount")),
                                Reason = Required(row, "reason"), Status = Enum.TryParse<LedgerRecordStatus>(row.Values.GetValueOrDefault("status"), true, out var deductionStatus) ? deductionStatus : LedgerRecordStatus.Active
                            });
                            break;
                        }
                        if (existing is FinanceDeduction existingDeduction)
                        {
                            existingDeduction.Settlement = deductionSettlement; existingDeduction.SettlementId = deductionSettlement.Id;
                            if (Has(row, "deduction_date", blankMeansNoChange)) existingDeduction.BusinessDate = ParseDate(row.Values["deduction_date"])!.Value;
                            if (Has(row, "amount", blankMeansNoChange)) existingDeduction.Amount = ParseDecimal(row.Values["amount"]) ?? 0m;
                            if (Has(row, "reduce_invoice_amount", blankMeansNoChange)) existingDeduction.ReduceInvoiceAmount = ParseBoolean(row.Values.GetValueOrDefault("reduce_invoice_amount"));
                            Set(row, "reason", value => existingDeduction.Reason = value!, existingDeduction.Reason, blankMeansNoChange);
                            if (Has(row, "status", blankMeansNoChange) && Enum.TryParse<LedgerRecordStatus>(row.Values.GetValueOrDefault("status"), true, out var status)) existingDeduction.Status = status;
                            existingDeduction.UpdatedAt = DateTimeOffset.UtcNow;
                            existingDeduction.ConcurrencyStamp = Guid.NewGuid();
                            deductionSettlement.ConcurrencyStamp = Guid.NewGuid();
                        }
                        break;
                }
            }
        }
    }

    private async Task<IReadOnlyList<FinanceInvoiceAllocationDraft>> BuildProjectInvoiceAllocationsAsync(
        Project project,
        Contract? contract,
        LegalEntity company,
        BusinessPartner partner,
        LedgerDirection direction,
        decimal amount,
        CancellationToken cancellationToken,
        Guid? excludedInvoiceId = null)
    {
        var settlements = await db.FinanceSettlements
            .Include(item => item.Adjustments)
            .Include(item => item.Deductions)
            .Include(item => item.InvoiceAllocations).ThenInclude(item => item.Invoice)
            .Where(item => item.ProjectId == project.Id && item.LegalEntityId == company.Id && item.BusinessPartnerId == partner.Id
                && item.Direction == direction && item.Status == LedgerRecordStatus.Active)
            .OrderBy(item => item.BusinessDate)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (settlements.Count == 0) throw new InvalidOperationException("发票找不到可关联的中央结算记录。");

        var remaining = amount;
        var order = 1;
        var result = new List<FinanceInvoiceAllocationDraft>();
        foreach (var settlement in settlements)
        {
            if (remaining <= 0m) break;
            var shouldInvoice = Math.Max(
                settlement.OriginalInvoiceAmount
                + settlement.Adjustments.Where(item => item.Status == LedgerRecordStatus.Active).Sum(item => item.InvoiceAmountDelta)
                - settlement.Deductions.Where(item => item.Status == LedgerRecordStatus.Active && item.ReduceInvoiceAmount).Sum(item => item.Amount),
                0m);
            var allocated = settlement.InvoiceAllocations.Where(item => item.Invoice.Status == LedgerRecordStatus.Active && item.InvoiceId != excludedInvoiceId).Sum(item => item.Amount);
            var allocationAmount = Math.Min(remaining, Math.Max(shouldInvoice - allocated, 0m));
            if (allocationAmount <= 0m) continue;
            result.Add(new FinanceInvoiceAllocationDraft(settlement, allocationAmount, order++));
            remaining -= allocationAmount;
        }
        if (remaining > 0m)
        {
            var first = settlements[0];
            var existing = result.FirstOrDefault(item => item.Settlement.Id == first.Id);
            if (existing is null) result.Add(new FinanceInvoiceAllocationDraft(first, remaining, order));
            else
            {
                result.Remove(existing);
                result.Insert(0, existing with { Amount = existing.Amount + remaining });
            }
        }
        return result;
    }

    private static LedgerRecordStatus ParseInvoiceRecordStatus(string? value)
    {
        if (Enum.TryParse<LedgerRecordStatus>(value, true, out var status)) return status;
        return Enum.TryParse<InvoiceStatus>(value, true, out var legacy) && legacy == InvoiceStatus.Voided
            ? LedgerRecordStatus.Voided
            : LedgerRecordStatus.Active;
    }

    private static LedgerSourceType ParseLedgerSourceType(string? value) =>
        Enum.TryParse<LedgerSourceType>(value, true, out var sourceType) ? sourceType : LedgerSourceType.CentralLedger;

    private sealed record FinanceInvoiceAllocationDraft(FinanceSettlement Settlement, decimal Amount, int AllocationOrder)
    {
        public FinanceInvoiceAllocation InvoiceAllocation => new()
        {
            Settlement = Settlement,
            ProjectId = Settlement.ProjectId,
            ContractId = Settlement.ContractId,
            ContractLineItemId = Settlement.ContractLineItemId,
            BusinessPartnerId = Settlement.BusinessPartnerId,
            CounterLegalEntityId = Settlement.CounterLegalEntityId,
            Amount = Amount,
            AllocationOrder = AllocationOrder
        };
    }

    private Task<Project> ResolveProjectAsync(ParsedRow row, CancellationToken cancellationToken) => db.Projects.SingleAsync(item => item.ProjectNumber == row.Values["project_number"], cancellationToken);

    private async Task<Contract?> ExistingContractAsync(object? existing, CancellationToken cancellationToken)
    {
        var id = existing switch
        {
            ReceivableEntry item => item.ContractId,
            CollectionEntry item => item.ContractId,
            PayableEntry item => item.ContractId,
            PaymentEntry item => item.ContractId,
            InvoiceEntry item => item.ContractId,
            FinanceSettlement item => item.ContractId,
            FinanceCashEntry item => item.Allocations.FirstOrDefault()?.ContractId,
            FinanceInvoice item => item.Allocations.FirstOrDefault()?.ContractId,
            FinanceDeduction item => item.Settlement.ContractId,
            _ => null
        };
        return id.HasValue ? await db.Contracts.SingleAsync(item => item.Id == id.Value, cancellationToken) : null;
    }

    private async Task<LegalEntity> ExistingLegalEntityAsync(object? existing, CancellationToken cancellationToken)
    {
        var id = existing switch
        {
            ReceivableEntry item => item.LegalEntityId,
            CollectionEntry item => item.LegalEntityId,
            PayableEntry item => item.LegalEntityId,
            PaymentEntry item => item.LegalEntityId,
            InvoiceEntry item => item.LegalEntityId,
            FinanceSettlement item => item.LegalEntityId,
            FinanceCashEntry item => item.LegalEntityId,
            FinanceInvoice item => item.LegalEntityId,
            FinanceDeduction item => item.Settlement.LegalEntityId,
            _ => Guid.Empty
        };
        return id == Guid.Empty
            ? throw new InvalidOperationException("缺少必填字段：legal_entity_code")
            : await db.LegalEntities.SingleAsync(item => item.Id == id, cancellationToken);
    }

    private async Task<BusinessPartner?> ExistingPartnerAsync(object? existing, CancellationToken cancellationToken)
    {
        var id = existing switch
        {
            ReceivableEntry item => item.BusinessPartnerId,
            CollectionEntry item => item.BusinessPartnerId,
            PayableEntry item => item.BusinessPartnerId,
            PaymentEntry item => item.BusinessPartnerId,
            InvoiceEntry item => item.BusinessPartnerId,
            FinanceSettlement item => item.BusinessPartnerId,
            FinanceCashEntry item => item.BusinessPartnerId,
            FinanceInvoice item => item.BusinessPartnerId,
            FinanceDeduction item => item.Settlement.BusinessPartnerId,
            _ => null
        };
        return id.HasValue ? await db.BusinessPartners.SingleAsync(item => item.Id == id.Value, cancellationToken) : null;
    }

    private async Task<Guid?> ResolveReceivableIdAsync(ParsedRow row, Guid projectId, CancellationToken cancellationToken)
    {
        var value = row.Values.GetValueOrDefault("receivable_id");
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Guid.TryParse(value, out var id) ||
            !db.FinanceSettlements.Local.Any(item => item.Id == id && item.ProjectId == projectId && item.Direction == LedgerDirection.Receivable && item.Status == LedgerRecordStatus.Active) &&
            !await db.FinanceSettlements.AnyAsync(item => item.Id == id && item.ProjectId == projectId && item.Direction == LedgerDirection.Receivable && item.Status == LedgerRecordStatus.Active, cancellationToken) &&
            !db.ReceivableEntries.Local.Any(item => item.Id == id && item.ProjectId == projectId && !item.IsVoided) &&
            !await db.ReceivableEntries.AnyAsync(item => item.Id == id && item.ProjectId == projectId && !item.IsVoided, cancellationToken))
            throw new InvalidOperationException("应收记录不存在、已作废或不属于当前项目。");
        return id;
    }

    private async Task<Guid?> ResolvePayableIdAsync(ParsedRow row, Guid projectId, CancellationToken cancellationToken)
    {
        var value = row.Values.GetValueOrDefault("payable_id");
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Guid.TryParse(value, out var id) ||
            !db.FinanceSettlements.Local.Any(item => item.Id == id && item.ProjectId == projectId && item.Direction == LedgerDirection.Payable && item.Status == LedgerRecordStatus.Active) &&
            !await db.FinanceSettlements.AnyAsync(item => item.Id == id && item.ProjectId == projectId && item.Direction == LedgerDirection.Payable && item.Status == LedgerRecordStatus.Active, cancellationToken) &&
            !db.PayableEntries.Local.Any(item => item.Id == id && item.ProjectId == projectId && !item.IsVoided) &&
            !await db.PayableEntries.AnyAsync(item => item.Id == id && item.ProjectId == projectId && !item.IsVoided, cancellationToken))
            throw new InvalidOperationException("应付记录不存在、已作废或不属于当前项目。");
        return id;
    }
    private async Task<Contract?> ResolveContractAsync(ParsedRow row, bool required, CancellationToken cancellationToken)
    {
        var number = row.Values.GetValueOrDefault("contract_number");
        if (string.IsNullOrWhiteSpace(number)) return required ? throw new InvalidOperationException("必须指定合同编号。") : null;
        return await db.Contracts.SingleAsync(item => item.Project.ProjectNumber == row.Values["project_number"] && item.ContractNumber == number, cancellationToken);
    }

    private async Task ApplyProjectAsync(Project project, ParsedRow row, bool blankMeansNoChange, CancellationToken cancellationToken)
    {
        Set(row, "project_number", value => project.ProjectNumber = value!, project.ProjectNumber, blankMeansNoChange);
        Set(row, "project_name", value => project.Name = value!, project.Name, blankMeansNoChange);
        Set(row, "parent_project", value => project.ParentProjectName = value, project.ParentProjectName, blankMeansNoChange);
        Set(row, "general_contractor", value => project.GeneralContractorName = value, project.GeneralContractorName, blankMeansNoChange);
        Set(row, "general_contractor_contact", value => project.GeneralContractorContact = value, project.GeneralContractorContact, blankMeansNoChange);
        Set(row, "general_contractor_phone", value => project.GeneralContractorPhone = value, project.GeneralContractorPhone, blankMeansNoChange);
        if (Has(row, "responsible_user_id", blankMeansNoChange))
        {
            var userId = row.Values.GetValueOrDefault("responsible_user_id");
            if (!string.IsNullOrWhiteSpace(userId) && !await db.Users.AnyAsync(item => item.Id == userId, cancellationToken))
                throw new InvalidOperationException($"负责人账号不存在：{userId}");
            project.ResponsibleUserId = string.IsNullOrWhiteSpace(userId) ? null : userId;
        }
        if (Has(row, "department_id", blankMeansNoChange)) project.DepartmentId = await ResolveOrganizationUnitIdAsync(row.Values.GetValueOrDefault("department_id"), OrganizationUnitType.Department, "部门", cancellationToken);
        if (Has(row, "branch_id", blankMeansNoChange)) project.BranchId = await ResolveOrganizationUnitIdAsync(row.Values.GetValueOrDefault("branch_id"), OrganizationUnitType.Branch, "分支机构", cancellationToken);
        if (Has(row, "stage", blankMeansNoChange) && Enum.TryParse<ProjectStage>(row.Values["stage"], true, out var stage)) project.Stage = stage;
        if (Has(row, "contract_signing_status", blankMeansNoChange) && Enum.TryParse<ContractSigningStatus>(row.Values["contract_signing_status"], true, out var signing)) project.ContractSigningStatus = signing;
        if (Has(row, "affiliation_type", blankMeansNoChange) && Enum.TryParse<ProjectAffiliationType>(row.Values["affiliation_type"], true, out var affiliation)) project.AffiliationType = affiliation;
        if (Has(row, "actual_start_date", blankMeansNoChange)) project.ActualStartDate = ParseDate(row.Values["actual_start_date"]);
        if (Has(row, "actual_completion_date", blankMeansNoChange)) project.ActualCompletionDate = ParseDate(row.Values["actual_completion_date"]);
        if (Has(row, "legal_entity_ids", blankMeansNoChange))
        {
            var legalEntityIds = ParseGuidList(row.Values.GetValueOrDefault("legal_entity_ids"));
            var available = await db.LegalEntities.Where(item => legalEntityIds.Contains(item.Id)).Select(item => item.Id).ToListAsync(cancellationToken);
            if (available.Count != legalEntityIds.Count) throw new InvalidOperationException("签约公司 ID 包含不存在的记录。");
            var existingLinks = await db.ProjectLegalEntities.Where(item => item.ProjectId == project.Id).ToListAsync(cancellationToken);
            db.ProjectLegalEntities.RemoveRange(existingLinks.Where(item => !legalEntityIds.Contains(item.LegalEntityId)));
            foreach (var id in legalEntityIds.Where(id => existingLinks.All(item => item.LegalEntityId != id)))
                db.ProjectLegalEntities.Add(new ProjectLegalEntity { Project = project, ProjectId = project.Id, LegalEntityId = id, IsPrimary = existingLinks.Count == 0 && legalEntityIds.Count > 0 && id == legalEntityIds[0] });
        }
        if (HasNonBlank(row, "is_active")) project.IsActive = ParseBoolean(row.Values["is_active"]);
        Set(row, "notes", value => project.Notes = value, project.Notes, blankMeansNoChange);
        project.ConcurrencyStamp = Guid.NewGuid();
    }

    private async Task<Guid?> ResolveOrganizationUnitIdAsync(string? value, OrganizationUnitType type, string label, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Guid.TryParse(value, out var id) || !await db.OrganizationUnits.AnyAsync(item => item.Id == id && item.UnitType == type && item.IsActive, cancellationToken))
            throw new InvalidOperationException($"{label}不存在、已停用或类型不匹配。");
        return id;
    }

    private static List<Guid> ParseGuidList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var ids = new List<Guid>();
        foreach (var part in value.Split([',', '，', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var id)) throw new InvalidOperationException($"ID 格式无效：{part}");
            if (!ids.Contains(id)) ids.Add(id);
        }
        return ids;
    }

    private static void Apply(Contract contract, ParsedRow row, bool blankMeansNoChange)
    {
        Set(row, "contract_number", value => contract.ContractNumber = value!, contract.ContractNumber, blankMeansNoChange);
        Set(row, "name", value => contract.Name = value!, contract.Name, blankMeansNoChange);
        Set(row, "counterparty_name", value => contract.CounterpartyName = value, contract.CounterpartyName, blankMeansNoChange);
        if (Has(row, "contract_type", blankMeansNoChange) && Enum.TryParse<ContractType>(row.Values["contract_type"], true, out var type)) contract.ContractType = type;
        if (Has(row, "allocation_mode", blankMeansNoChange) && Enum.TryParse<ContractAllocationMode>(row.Values["allocation_mode"], true, out var allocation)) contract.AllocationMode = allocation;
        if (Has(row, "signed_date", blankMeansNoChange)) contract.SignedDate = ParseDate(row.Values["signed_date"]);
        if (Has(row, "total_amount", blankMeansNoChange)) contract.TotalAmount = ParseDecimal(row.Values["total_amount"]) ?? 0m;
        if (HasNonBlank(row, "is_active")) contract.IsActive = ParseBoolean(row.Values["is_active"]);
        Set(row, "notes", value => contract.Notes = value, contract.Notes, blankMeansNoChange);
        contract.ConcurrencyStamp = Guid.NewGuid();
    }

    private static void Apply(ContractLineItem line, ParsedRow row, bool blankMeansNoChange)
    {
        Set(row, "code", value => line.Code = value!, line.Code, blankMeansNoChange); Set(row, "name", value => line.Name = value!, line.Name, blankMeansNoChange); Set(row, "unit", value => line.Unit = value!, line.Unit, blankMeansNoChange);
        if (Has(row, "estimated_quantity", blankMeansNoChange)) line.EstimatedQuantity = ParseDecimal(row.Values["estimated_quantity"]);
        if (Has(row, "estimated_unit_price", blankMeansNoChange)) line.EstimatedUnitPrice = ParseDecimal(row.Values["estimated_unit_price"]);
        if (Has(row, "settled_quantity", blankMeansNoChange)) line.SettledQuantity = ParseDecimal(row.Values["settled_quantity"]);
        if (Has(row, "settled_unit_price", blankMeansNoChange)) line.SettledUnitPrice = ParseDecimal(row.Values["settled_unit_price"]);
        if (HasNonBlank(row, "is_settlement_confirmed")) line.IsSettlementConfirmed = ParseBoolean(row.Values["is_settlement_confirmed"]);
        Set(row, "notes", value => line.Notes = value, line.Notes, blankMeansNoChange);
        line.ConcurrencyStamp = Guid.NewGuid();
    }

    private static bool Has(ParsedRow row, string key, bool blankMeansNoChange) => row.PresentKeys.Contains(key) && (!blankMeansNoChange || !string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault(key)));
    private static bool HasNonBlank(ParsedRow row, string key) => row.PresentKeys.Contains(key) && !string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault(key));
    private static void Set(ParsedRow row, string key, Action<string?> setter, string? current, bool blankMeansNoChange) { if (Has(row, key, blankMeansNoChange)) setter(row.Values.GetValueOrDefault(key)); }
    private static string Required(ParsedRow row, string key) => row.Values.GetValueOrDefault(key)?.Trim() ?? throw new InvalidOperationException($"缺少必填字段：{key}");
    private static Guid RequestedId(ParsedRow row) => Guid.TryParse(row.Values.GetValueOrDefault("_system_id"), out var id) ? id : Guid.NewGuid();
    private static string? ConvertValue(object? value) => value switch { null => null, DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), bool boolean => boolean ? "true" : "false", decimal number => number.ToString(CultureInfo.InvariantCulture), _ => Convert.ToString(value, CultureInfo.InvariantCulture) };
    private static decimal? ParseDecimal(string? value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;
    private static DateOnly? ParseDate(string? value) => DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    private static bool ParseBoolean(string? value) =>
        TryParseBoolean(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"布尔值无效：{value}");

    private static bool TryParseBoolean(string? value, out bool parsed)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "true": case "1": case "是": case "yes":
                parsed = true;
                return true;
            case "false": case "0": case "否": case "no":
                parsed = false;
                return true;
            default:
                parsed = false;
                return false;
        }
    }
}
