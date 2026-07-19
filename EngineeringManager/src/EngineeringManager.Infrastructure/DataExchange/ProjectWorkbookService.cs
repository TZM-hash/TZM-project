using System.Security.Cryptography;
using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;
using EngineeringManager.Application.Security;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed class ProjectWorkbookService(
    ApplicationDbContext db,
    IProjectService projectService,
    IFinanceLedgerService financeService,
    IFileStore? fileStore = null)
    : IProjectWorkbookService
{
    public IReadOnlyList<ProjectWorkbookSheetDefinition> GetSheets() => ProjectWorkbookCatalog.Sheets;

    public async Task<ExportFileResult> ExportAsync(ProjectWorkbookExportRequest request, CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(request.Actor, cancellationToken);
        if (!actor.CanExport || !string.Equals(actor.UserId, request.Scope.Actor.UserId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("当前用户无权导出项目工作簿。");

        var selectedSheets = request.Sheets.Count == 0
            ? ProjectWorkbookCatalog.Sheets.Where(item => item.Sheet != ProjectWorkbookSheet.Attachments).Select(item => item.Sheet).ToHashSet()
            : request.Sheets.ToHashSet();
        var isFullWorkbook = request.Sheets.Count == 0
            || selectedSheets.Contains(ProjectWorkbookSheet.ProjectSummary)
            || ProjectWorkbookCatalog.Sheets.Where(item => item.CanImport).All(item => selectedSheets.Contains(item.Sheet));
        if (isFullWorkbook && !actor.CanExportFullWorkbook)
            throw new UnauthorizedAccessException("全量项目工作簿仅限管理员或业务管理员导出。");
        if (selectedSheets.Any(sheet => !actor.CanExportSheet(sheet)))
            throw new UnauthorizedAccessException("当前用户无权导出所选项目工作表。");
        if (request.IncludeAttachments && !actor.CanExportAttachments)
            throw new UnauthorizedAccessException("当前用户无权导出项目附件。");
        if (selectedSheets.Contains(ProjectWorkbookSheet.Attachments) && !request.IncludeAttachments)
            throw new InvalidOperationException("选择附件清单时必须同时包含附件 ZIP。");

        var exporter = new ProjectWorkbookExporter(db, projectService, financeService, fileStore);
        var effectiveRequest = request with
        {
            Scope = request.Scope with { Actor = ProjectScopeActor(actor) },
            Actor = actor
        };
        var result = await exporter.ExportAsync(effectiveRequest, cancellationToken);
        var fileName = $"项目管理工作簿_{DateTime.Now:yyyyMMddHHmmss}.{(result.IsArchive ? "zip" : "xlsx")}";
        var contentType = result.IsArchive ? "application/zip" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        var recordedSheets = request.Sheets.Count == 0
            ? ProjectWorkbookCatalog.Sheets.Where(item => item.Sheet != ProjectWorkbookSheet.Attachments).Select(item => item.Sheet).ToHashSet()
            : request.Sheets.ToHashSet();
        if (request.IncludeAttachments) recordedSheets.Add(ProjectWorkbookSheet.Attachments);
        var task = new DataExchangeTask
        {
            UserId = request.Scope.Actor.UserId,
            Direction = DataExchangeDirection.Export,
            DatasetsJson = JsonSerializer.Serialize(ProjectWorkbookCatalog.Sheets.Where(item => recordedSheets.Contains(item.Sheet)).Select(item => item.Sheet).ToArray()),
            SelectedFieldsJson = "{}",
            FilterJson = JsonSerializer.Serialize(request.Scope.Query),
            Scope = request.Scope.SelectAllMatching ? ExportScope.FullAuthorized : ExportScope.SelectedModules,
            PackageFormat = result.IsArchive ? ExportPackageFormat.Zip : ExportPackageFormat.Workbook,
            IncludeAttachments = request.IncludeAttachments,
            RowCount = result.RowCount,
            FileName = fileName,
            ContentType = contentType,
            ResultContent = result.Content,
            Sha256 = Convert.ToHexString(SHA256.HashData(result.Content)),
            Status = DataExchangeTaskStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow
        };
        db.DataExchangeTasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);
        return new ExportFileResult(fileName, contentType, result.Content);
    }

    public async Task<ProjectWorkbookImportPreviewDto> PreviewAsync(ProjectWorkbookImportRequest request, CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(request.Actor, cancellationToken);
        ValidateImportActor(actor, request.UserId);
        return await new ProjectWorkbookImporter(db, fileStore).PreviewAsync(request with { Actor = actor }, cancellationToken);
    }

    public async Task ConfirmAsync(ProjectWorkbookActor actor, Guid batchId, CancellationToken cancellationToken)
    {
        var effectiveActor = await ResolveActorAsync(actor, cancellationToken);
        ValidateImportActor(effectiveActor, effectiveActor.UserId);
        await new ProjectWorkbookImporter(db, fileStore).ConfirmAsync(effectiveActor, batchId, cancellationToken);
    }

    private static void ValidateImportActor(ProjectWorkbookActor? actor, string userId)
    {
        if (actor is null || string.IsNullOrWhiteSpace(actor.UserId) || !string.Equals(actor.UserId, userId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("工作簿导入缺少授权上下文。");
        if (!actor.CanImport)
            throw new UnauthorizedAccessException("当前用户无权导入项目工作簿。");
    }

    private async Task<ProjectWorkbookActor> ResolveActorAsync(ProjectWorkbookActor? actor, CancellationToken cancellationToken)
    {
        if (actor is null || string.IsNullOrWhiteSpace(actor.UserId))
            throw new UnauthorizedAccessException("工作簿操作缺少授权上下文。");
        var overrides = await db.UserPermissionOverrides.AsNoTracking()
            .Where(item => item.UserId == actor.UserId)
            .Select(item => new PermissionOverrideDto(item.PermissionKey, item.Effect))
            .ToListAsync(cancellationToken);
        return actor with { PermissionOverrides = overrides };
    }

    private static ProjectListActor ProjectScopeActor(ProjectWorkbookActor actor)
    {
        var canAccessAllProjects = actor.Roles.Any(role => role is
            SystemRoles.SystemAdministrator
            or SystemRoles.ApplicationAdministrator
            or SystemRoles.Finance
            or SystemRoles.QueryOnly
            or SystemRoles.EquipmentManager);
        return new ProjectListActor(actor.UserId, canAccessAllProjects);
    }
}
