using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using EngineeringManager.Application.Backups;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Backups;

public sealed class BackupService(
    ApplicationDbContext db,
    IDatabaseBackupExecutor databaseBackupExecutor,
    string attachmentRoot,
    string backupRoot) : IBackupService
{
    private static readonly SemaphoreSlim FullBackupLock = new(1, 1);
    private static readonly JsonSerializerOptions SettingsJsonOptions = new() { WriteIndented = true };
    private static readonly string[] SettingsFiles = ["settings/settings.json"];

    public Task<BackupTaskDto> CreateBackupAsync(string requestedByUserId, CancellationToken cancellationToken) =>
        CreateBackupAsync(requestedByUserId, BackupKind.Full, cancellationToken);

    public async Task<BackupTaskDto> CreateBackupAsync(string requestedByUserId, BackupKind kind, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedByUserId)) throw new ArgumentException("备份任务必须记录操作用户。", nameof(requestedByUserId));
        if (!await FullBackupLock.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken)) throw new InvalidOperationException("同一备份类型已有任务正在执行。");
        try
        {
            Directory.CreateDirectory(attachmentRoot);
            Directory.CreateDirectory(backupRoot);
            var suffix = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
            var task = new BackupTask
            {
                RequestedByUserId = requestedByUserId.Trim(),
                Kind = kind,
                Status = DataExchangeTaskStatus.Running,
                StartedAt = DateTimeOffset.UtcNow,
                LocalStatus = BackupTargetStatus.Pending
            };
            db.BackupTasks.Add(task);
            await db.SaveChangesAsync(cancellationToken);
            try
            {
                if (kind == BackupKind.Settings)
                {
                    task.PackagePath = Path.GetFullPath(Path.Combine(backupRoot, $"EngineeringManager_Settings_{suffix}.zip"));
                    await CreateSettingsPackageAsync(task.PackagePath, cancellationToken);
                }
                else
                {
                    task.DatabaseBackupPath = Path.GetFullPath(Path.Combine(backupRoot, $"EngineeringManager_{suffix}.bak"));
                    task.AttachmentArchivePath = Path.GetFullPath(Path.Combine(backupRoot, $"EngineeringManager_Attachments_{suffix}.zip"));
                    task.PackagePath = Path.GetFullPath(Path.Combine(backupRoot, $"EngineeringManager_Full_{suffix}.zip"));
                    await databaseBackupExecutor.ExecuteAsync(task.DatabaseBackupPath, cancellationToken);
                    ZipFile.CreateFromDirectory(attachmentRoot, task.AttachmentArchivePath, CompressionLevel.Fastest, includeBaseDirectory: false);
                    await CreateFullPackageAsync(task, cancellationToken);
                }

                var schedule = await db.BackupSchedules.SingleOrDefaultAsync(item => item.Kind == kind, cancellationToken);
                if (!string.IsNullOrWhiteSpace(schedule?.LocalTargetDirectory))
                {
                    Directory.CreateDirectory(schedule.LocalTargetDirectory);
                    var localPath = Path.Combine(schedule.LocalTargetDirectory, Path.GetFileName(task.PackagePath));
                    var tempPath = localPath + ".tmp";
                    await using (var source = File.OpenRead(task.PackagePath))
                    await using (var target = File.Create(tempPath))
                    {
                        await source.CopyToAsync(target, cancellationToken);
                    }
                    File.Move(tempPath, localPath, overwrite: true);
                    task.PackagePath = localPath;
                }
                task.Sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(task.PackagePath!, cancellationToken)));
                task.LocalStatus = BackupTargetStatus.Succeeded;
                task.NasStatus = BackupTargetStatus.NotConfigured;
                if (!string.IsNullOrWhiteSpace(schedule?.NasTargetDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(schedule.NasTargetDirectory);
                        var nasPath = Path.Combine(schedule.NasTargetDirectory, Path.GetFileName(task.PackagePath));
                        var tempPath = nasPath + ".tmp";
                        await using (var source = File.OpenRead(task.PackagePath))
                        await using (var target = File.Create(tempPath))
                        {
                            await source.CopyToAsync(target, cancellationToken);
                        }
                        File.Move(tempPath, nasPath, overwrite: true);
                        task.NasStatus = BackupTargetStatus.Succeeded;
                    }
                    catch (Exception exception)
                    {
                        task.NasStatus = BackupTargetStatus.Failed;
                        task.ErrorMessage = $"本机备份成功，但 NAS 副本失败：{exception.Message}";
                    }
                }
                task.Status = task.NasStatus == BackupTargetStatus.Failed ? DataExchangeTaskStatus.Failed : DataExchangeTaskStatus.Completed;
                task.CompletedAt = DateTimeOffset.UtcNow;
                db.AuditLogs.Add(new AuditLog { UserId = task.RequestedByUserId, Action = "BackupCreated", EntityType = nameof(BackupTask), EntityId = task.Id.ToString(), Reason = task.Kind.ToString(), AfterJson = JsonSerializer.Serialize(new { task.Kind, task.Status, task.LocalStatus, task.NasStatus, task.Sha256 }) });
                if (schedule is not null)
                {
                    schedule.LastRunAt = task.CompletedAt;
                    schedule.NextRunAt = CalculateNextRun(schedule, task.CompletedAt.Value);
                }
                await db.SaveChangesAsync(cancellationToken);
                await ApplyRetentionAsync(schedule, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                task.Status = DataExchangeTaskStatus.Failed;
                task.LocalStatus = BackupTargetStatus.Failed;
                task.ErrorMessage = exception.Message[..Math.Min(2000, exception.Message.Length)];
                task.CompletedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            return ToDto(task);
        }
        finally
        {
            FullBackupLock.Release();
        }
    }

    public async Task<IReadOnlyList<BackupTaskDto>> ListAsync(CancellationToken cancellationToken)
    {
        var tasks = await db.BackupTasks.AsNoTracking().ToListAsync(cancellationToken);
        return tasks.OrderByDescending(item => item.CreatedAt).Select(ToDto).ToArray();
    }

    public async Task<IReadOnlyList<BackupScheduleDto>> ListSchedulesAsync(CancellationToken cancellationToken)
    {
        var schedules = await db.BackupSchedules.AsNoTracking().ToListAsync(cancellationToken);
        return schedules.OrderBy(item => item.Kind).Select(ToScheduleDto).ToArray();
    }

    public async Task<BackupScheduleDto> SaveScheduleAsync(SaveBackupScheduleRequest request, CancellationToken cancellationToken)
    {
        if (request.Mode == BackupScheduleMode.Interval && (!request.IntervalMinutes.HasValue || request.IntervalMinutes < 1)) throw new ArgumentException("间隔分钟数必须大于 0。", nameof(request));
        if (request.LocalRetentionCount < 1 || request.NasRetentionCount < 0) throw new ArgumentException("保留数量不符合要求。", nameof(request));
        var schedule = await db.BackupSchedules.SingleOrDefaultAsync(item => item.Kind == request.Kind, cancellationToken);
        if (schedule is null)
        {
            schedule = new BackupSchedule { Kind = request.Kind };
            db.BackupSchedules.Add(schedule);
        }
        schedule.Enabled = request.Enabled;
        schedule.Mode = request.Mode;
        schedule.IntervalMinutes = request.IntervalMinutes;
        schedule.FixedTime = request.FixedTime;
        schedule.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "Asia/Shanghai" : request.TimeZoneId.Trim();
        schedule.LocalTargetDirectory = NormalizePath(request.LocalTargetDirectory);
        schedule.NasTargetDirectory = NormalizePath(request.NasTargetDirectory);
        schedule.LocalRetentionCount = request.LocalRetentionCount;
        schedule.NasRetentionCount = request.NasRetentionCount;
        schedule.AlertOnFailure = request.AlertOnFailure;
        schedule.NextRunAt = schedule.Enabled ? CalculateNextRun(schedule, DateTimeOffset.UtcNow) : null;
        schedule.ConcurrencyStamp = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
        return ToScheduleDto(schedule);
    }

    public async Task<IReadOnlyList<BackupTaskDto>> RunDueSchedulesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var schedules = await db.BackupSchedules.Where(item => item.Enabled).ToListAsync(cancellationToken);
        var due = schedules.Where(item => item.NextRunAt.HasValue && item.NextRunAt <= now).ToArray();
        var results = new List<BackupTaskDto>(due.Length);
        foreach (var schedule in due)
        {
            results.Add(await CreateBackupAsync("scheduler", schedule.Kind, cancellationToken));
        }
        return results;
    }

    public async Task<SettingsRestorePreviewDto> PreviewSettingsAsync(byte[] content, CancellationToken cancellationToken)
    {
        var snapshot = ReadSettingsSnapshot(content);
        cancellationToken.ThrowIfCancellationRequested();
        var format = snapshot.RootElement.TryGetProperty("format", out var formatElement) ? formatElement.GetString() ?? "unknown" : "unknown";
        var settingCount = snapshot.RootElement.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.Array ? settings.GetArrayLength() : 0;
        var userCount = snapshot.RootElement.TryGetProperty("users", out var users) && users.ValueKind == JsonValueKind.Array ? users.GetArrayLength() : 0;
        return new SettingsRestorePreviewDto(format, ["settings", "organizations", "legalEntities", "businessYears", "users"], settingCount, userCount);
    }

    public async Task RestoreSettingsAsync(byte[] content, IReadOnlyCollection<string> categories, string requestedByUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedByUserId)) throw new ArgumentException("恢复任务必须记录操作用户。", nameof(requestedByUserId));
        using var snapshot = ReadSettingsSnapshot(content);
        if (!categories.Contains("settings", StringComparer.OrdinalIgnoreCase)) return;
        if (!snapshot.RootElement.TryGetProperty("settings", out var settings) || settings.ValueKind != JsonValueKind.Array) throw new InvalidDataException("设置备份缺少 settings 分类。");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var item in settings.EnumerateArray())
        {
            var key = item.GetProperty("Key").GetString();
            if (string.IsNullOrWhiteSpace(key)) continue;
            var value = item.TryGetProperty("Value", out var valueElement) ? valueElement.GetString() : null;
            var current = await db.SystemSettings.SingleOrDefaultAsync(setting => setting.Key == key, cancellationToken);
            if (current is null) db.SystemSettings.Add(new SystemSetting { Key = key, Value = value ?? string.Empty });
            else current.Value = value ?? string.Empty;
        }
        db.AuditLogs.Add(new AuditLog { UserId = requestedByUserId, Action = "SettingsRestored", EntityType = nameof(SystemSetting), EntityId = "settings", Reason = "设置备份恢复", AfterJson = JsonSerializer.Serialize(new { categories, count = settings.GetArrayLength() }) });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static JsonDocument ReadSettingsSnapshot(byte[] content)
    {
        if (content is null || content.Length == 0) throw new ArgumentException("设置备份文件不能为空。", nameof(content));
        using var input = new MemoryStream(content, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        var manifest = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("设置备份缺少 manifest.json。");
        using (var manifestReader = new StreamReader(manifest.Open()))
        {
            using var manifestJson = JsonDocument.Parse(manifestReader.ReadToEnd());
            if (!manifestJson.RootElement.TryGetProperty("type", out var type) || !string.Equals(type.GetString(), "settings", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("备份包不是设置备份。");
        }
        var settings = archive.GetEntry("settings/settings.json") ?? throw new InvalidDataException("设置备份缺少 settings/settings.json。");
        using var settingsReader = new StreamReader(settings.Open());
        return JsonDocument.Parse(settingsReader.ReadToEnd());
    }

    private async Task CreateSettingsPackageAsync(string path, CancellationToken cancellationToken)
    {
        var snapshot = new
        {
            format = "engineering-manager-settings-v1",
            exportedAt = DateTimeOffset.UtcNow,
            organizations = await db.OrganizationUnits.AsNoTracking().Select(item => new { item.Id, item.Name, item.ParentId, item.IsActive }).ToListAsync(cancellationToken),
            legalEntities = await db.LegalEntities.AsNoTracking().Select(item => new { item.Id, item.Code, item.Name, item.ShortName, item.IsActive }).ToListAsync(cancellationToken),
            businessYears = await db.BusinessYears.AsNoTracking().Select(item => new { item.Id, item.Name, item.StartDate, item.EndDate }).ToListAsync(cancellationToken),
            settings = await db.SystemSettings.AsNoTracking().Select(item => new { item.Key, item.Value }).ToListAsync(cancellationToken),
            users = await db.Users.AsNoTracking().Select(item => new { item.Id, item.UserName, item.DisplayName, item.Email, item.EmailConfirmed, item.LockoutEnd }).ToListAsync(cancellationToken)
        };
        var json = JsonSerializer.Serialize(snapshot, SettingsJsonOptions);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "settings/settings.json", json);
        var manifest = JsonSerializer.Serialize(new { type = "settings", version = "1", createdAt = DateTimeOffset.UtcNow, files = SettingsFiles });
        WriteEntry(archive, "manifest.json", manifest);
        WriteEntry(archive, "checksums.sha256", $"{Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))}  settings/settings.json{Environment.NewLine}{Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(manifest)))}  manifest.json");
        WriteEntry(archive, "restore-readme.txt", "设置恢复不会覆盖业务数据、密码、连接字符串、TLS 或服务器密钥。恢复前请先校验清单。");
    }

    private static async Task CreateFullPackageAsync(BackupTask task, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.Open(task.PackagePath!, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(task.DatabaseBackupPath!, "database/EngineeringManager.bak", CompressionLevel.Fastest);
        archive.CreateEntryFromFile(task.AttachmentArchivePath!, "attachments/attachments.zip", CompressionLevel.Fastest);
        var manifest = JsonSerializer.Serialize(new { type = "full", version = "1", createdAt = DateTimeOffset.UtcNow, database = "database/EngineeringManager.bak", attachments = "attachments/attachments.zip" });
        WriteEntry(archive, "manifest.json", manifest);
        WriteEntry(archive, "checksums.sha256", $"{Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(task.DatabaseBackupPath!, cancellationToken)))}  database/EngineeringManager.bak{Environment.NewLine}{Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(task.AttachmentArchivePath!, cancellationToken)))}  attachments/attachments.zip{Environment.NewLine}{Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(manifest)))}  manifest.json");
        WriteEntry(archive, "restore-readme.txt", "完整恢复需使用独立恢复工具，并在新服务器单独配置连接字符串、TLS 和服务器密钥。");
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task ApplyRetentionAsync(BackupSchedule? schedule, CancellationToken cancellationToken)
    {
        if (schedule is null || string.IsNullOrWhiteSpace(schedule.LocalTargetDirectory)) return;
        if (!Directory.Exists(schedule.LocalTargetDirectory)) return;
        var files = Directory.EnumerateFiles(schedule.LocalTargetDirectory, "EngineeringManager_*.zip").OrderByDescending(File.GetCreationTimeUtc).ToArray();
        foreach (var file in files.Skip(schedule.LocalRetentionCount))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(file);
        }
        await Task.CompletedTask;
    }

    private static DateTimeOffset? CalculateNextRun(BackupSchedule schedule, DateTimeOffset from)
    {
        if (!schedule.Enabled || schedule.Mode == BackupScheduleMode.Disabled) return null;
        if (schedule.Mode == BackupScheduleMode.Interval) return from.AddMinutes(schedule.IntervalMinutes ?? 60);
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.Local; }
        var local = TimeZoneInfo.ConvertTime(from, zone);
        var target = local.Date + (schedule.FixedTime ?? new TimeOnly(2, 0)).ToTimeSpan();
        if (target <= local.DateTime) target = target.AddDays(1);
        return new DateTimeOffset(target, local.Offset).ToUniversalTime();
    }

    private static string? NormalizePath(string? value) => string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value.Trim());
    private static void WriteEntry(ZipArchive archive, string path, string content) { using var writer = new StreamWriter(archive.CreateEntry(path).Open()); writer.Write(content); }
    private static BackupTaskDto ToDto(BackupTask task) => new(task.Id, task.RequestedByUserId, task.Status, task.DatabaseBackupPath, task.AttachmentArchivePath, task.ErrorMessage, task.CreatedAt, task.StartedAt, task.CompletedAt, task.Kind, task.PackagePath, task.Sha256, task.LocalStatus, task.NasStatus, task.IsRetained);
    private static BackupScheduleDto ToScheduleDto(BackupSchedule item) => new(item.Id, item.Kind, item.Enabled, item.Mode, item.IntervalMinutes, item.FixedTime, item.TimeZoneId, item.LocalTargetDirectory, item.NasTargetDirectory, item.LocalRetentionCount, item.NasRetentionCount, item.AlertOnFailure, item.LastRunAt, item.NextRunAt);
}
