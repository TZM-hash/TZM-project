using System.Security.Claims;
using EngineeringManager.Application.Backups;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Backups;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator)]
public sealed class IndexModel(IBackupService backupService) : PageModel
{
    public IReadOnlyList<BackupTaskDto> Tasks { get; private set; } = [];
    public IReadOnlyList<BackupScheduleDto> Schedules { get; private set; } = [];

    [BindProperty] public BackupKind ScheduleKind { get; set; } = BackupKind.Full;
    [BindProperty] public bool ScheduleEnabled { get; set; }
    [BindProperty] public BackupScheduleMode ScheduleMode { get; set; } = BackupScheduleMode.Interval;
    [BindProperty] public int? IntervalMinutes { get; set; } = 1440;
    [BindProperty] public TimeOnly? FixedTime { get; set; } = new TimeOnly(2, 0);
    [BindProperty] public string TimeZoneId { get; set; } = "Asia/Shanghai";
    [BindProperty] public string? LocalTargetDirectory { get; set; }
    [BindProperty] public string? NasTargetDirectory { get; set; }
    [BindProperty] public int LocalRetentionCount { get; set; } = 10;
    [BindProperty] public int NasRetentionCount { get; set; } = 10;
    [BindProperty] public bool AlertOnFailure { get; set; } = true;
    [BindProperty] public IFormFile? SettingsRestoreFile { get; set; }
    public SettingsRestorePreviewDto? SettingsPreview { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Tasks = await backupService.ListAsync(cancellationToken);
        Schedules = await backupService.ListSchedulesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await backupService.CreateBackupAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSettingsAsync(CancellationToken cancellationToken)
    {
        await backupService.CreateBackupAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", BackupKind.Settings, cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveScheduleAsync(CancellationToken cancellationToken)
    {
        await backupService.SaveScheduleAsync(new SaveBackupScheduleRequest(ScheduleKind, ScheduleEnabled, ScheduleMode, IntervalMinutes, FixedTime, TimeZoneId, LocalTargetDirectory, NasTargetDirectory, LocalRetentionCount, NasRetentionCount, AlertOnFailure), cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPreviewSettingsRestoreAsync(CancellationToken cancellationToken)
    {
        if (SettingsRestoreFile is null || SettingsRestoreFile.Length == 0) { ModelState.AddModelError(string.Empty, "请选择设置备份包。"); await OnGetAsync(cancellationToken); return Page(); }
        await using var stream = new MemoryStream();
        await SettingsRestoreFile.CopyToAsync(stream, cancellationToken);
        SettingsPreview = await backupService.PreviewSettingsAsync(stream.ToArray(), cancellationToken);
        await OnGetAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRestoreSettingsAsync(CancellationToken cancellationToken)
    {
        if (SettingsRestoreFile is null || SettingsRestoreFile.Length == 0) return BadRequest("请选择设置备份包。");
        await using var stream = new MemoryStream();
        await SettingsRestoreFile.CopyToAsync(stream, cancellationToken);
        await backupService.RestoreSettingsAsync(stream.ToArray(), ["settings"], User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetDownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        var task = (await backupService.ListAsync(cancellationToken)).SingleOrDefault(item => item.Id == id) ?? throw new InvalidOperationException("备份任务不存在。");
        var path = task.PackagePath ?? task.DatabaseBackupPath ?? task.AttachmentArchivePath;
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path));
    }
}
