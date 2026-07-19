using System.IO.Compression;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using EngineeringManager.Application.DataExchange;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed record ProjectWorkbookArchiveAttachment(
    string OriginalFileName,
    string Path,
    long SizeBytes,
    string Sha256,
    string ContentType,
    string Category,
    string? ProjectNumber,
    string? ContractNumber,
    string? StageResultKey,
    string? Description,
    byte[] Content);

public sealed record ProjectWorkbookArchiveReadResult(
    byte[] Workbook,
    IReadOnlyList<ProjectWorkbookArchiveAttachment> Attachments,
    IReadOnlyList<ImportErrorDto> Errors);

public sealed class ProjectWorkbookArchive(IFileStore fileStore)
{
    private const int MaxEntryCount = 1000;
    private const long MaxEntryBytes = 50 * 1024 * 1024;
    private const long MaxTotalBytes = 200 * 1024 * 1024;
    private const long MaxWorkbookBytes = 50 * 1024 * 1024;
    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".txt", ".csv", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip"
    };

    public static ProjectWorkbookArchiveReadResult Read(byte[] content)
    {
        var errors = new List<ImportErrorDto>();
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        if (content.LongLength > MaxTotalBytes)
            return new ProjectWorkbookArchiveReadResult([], [], [new ImportErrorDto(1, "ZIP/大小", "ZIP 包大小超过限制。", content.LongLength.ToString(CultureInfo.InvariantCulture))]);

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false, Encoding.UTF8);
            if (archive.Entries.Count > MaxEntryCount)
            {
                errors.Add(new ImportErrorDto(1, "ZIP/条目数", "ZIP 条目数量超过限制。", archive.Entries.Count.ToString(CultureInfo.InvariantCulture)));
            }

            var totalBytes = 0L;
            foreach (var entry in archive.Entries.Take(MaxEntryCount + 1))
            {
                var path = entry.FullName.Replace('\\', '/');
                if (!IsSafeRelativePath(path))
                {
                    errors.Add(new ImportErrorDto(1, "ZIP/路径", "ZIP 包含不安全的相对路径。", entry.FullName));
                    continue;
                }
                if (files.ContainsKey(path))
                {
                    errors.Add(new ImportErrorDto(1, "ZIP/路径", "ZIP 包含重复路径。", path));
                    continue;
                }
                if (entry.Length > MaxEntryBytes || (path == "project-workbook.xlsx" && entry.Length > MaxWorkbookBytes))
                {
                    errors.Add(new ImportErrorDto(1, "ZIP/大小", "ZIP 条目大小超过限制。", path));
                    continue;
                }
                if (path.StartsWith("attachments/", StringComparison.OrdinalIgnoreCase)
                    && !AllowedAttachmentExtensions.Contains(Path.GetExtension(path)))
                {
                    errors.Add(new ImportErrorDto(1, "ZIP/附件类型", "附件扩展名不在允许范围内。", path));
                    continue;
                }

                using var input = entry.Open();
                using var output = new MemoryStream();
                var buffer = new byte[81920];
                var entryBytes = 0L;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    entryBytes += read;
                    totalBytes += read;
                    if (entryBytes > MaxEntryBytes || totalBytes > MaxTotalBytes)
                    {
                        errors.Add(new ImportErrorDto(1, "ZIP/大小", "ZIP 解压后的总大小超过限制。", path));
                        output.SetLength(0);
                        break;
                    }
                    output.Write(buffer, 0, read);
                }
                if (output.Length == 0 && entryBytes > 0) continue;
                if (entry.CompressedLength > 0 && entryBytes / (double)entry.CompressedLength > 1000d)
                {
                    errors.Add(new ImportErrorDto(1, "ZIP/压缩比", "ZIP 条目压缩比异常，已拒绝解析。", path));
                    continue;
                }
                files[path] = output.ToArray();
            }
        }
        catch (InvalidDataException exception)
        {
            errors.Add(new ImportErrorDto(1, "ZIP/格式", "ZIP 格式无效。", exception.Message));
        }

        if (!files.TryGetValue("project-workbook.xlsx", out var workbook))
        {
            errors.Add(new ImportErrorDto(1, "ZIP/工作簿", "ZIP 缺少 project-workbook.xlsx。", null));
            workbook = [];
        }
        if (!files.TryGetValue("checksums.sha256", out var checksumBytes))
        {
            errors.Add(new ImportErrorDto(1, "ZIP/SHA-256", "ZIP 缺少 checksums.sha256，无法校验 SHA-256。", null));
        }
        else
        {
            ValidateChecksums(checksumBytes, files, errors);
        }

        var attachmentItems = new List<ProjectWorkbookArchiveAttachment>();
        var manifestPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (files.TryGetValue("manifest.json", out var manifestBytes))
        {
            try
            {
                using var manifest = JsonDocument.Parse(manifestBytes);
                if (manifest.RootElement.TryGetProperty("workbookVersion", out var workbookVersion)
                    && !string.Equals(workbookVersion.GetString(), ProjectWorkbookVersions.Workbook, StringComparison.Ordinal))
                    errors.Add(new ImportErrorDto(1, "ZIP/manifest.json", "附件归档工作簿版本不受支持。", workbookVersion.GetString()));
                if (manifest.RootElement.TryGetProperty("attachments", out var attachments) && attachments.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in attachments.EnumerateArray())
                    {
                        var path = item.GetProperty("path").GetString() ?? string.Empty;
                        if (!IsSafeRelativePath(path) || !path.StartsWith("attachments/", StringComparison.OrdinalIgnoreCase) || !manifestPaths.Add(path))
                        {
                            errors.Add(new ImportErrorDto(1, "ZIP/manifest.json", "附件清单包含不安全、重复或非附件路径。", path));
                            continue;
                        }
                        var expectedSize = item.GetProperty("sizeBytes").GetInt64();
                        var expectedHash = item.GetProperty("sha256").GetString() ?? string.Empty;
                        var originalFileName = item.TryGetProperty("originalFileName", out var nameElement)
                            ? nameElement.GetString() ?? Path.GetFileName(path)
                            : Path.GetFileName(path);
                        if (!string.Equals(Path.GetFileName(path), originalFileName, StringComparison.Ordinal))
                            errors.Add(new ImportErrorDto(1, "ZIP/manifest.json", "附件原文件名与归档路径不一致。", path));
                        if (!files.TryGetValue(path, out var bytes))
                        {
                            errors.Add(new ImportErrorDto(1, "附件清单/相对路径", "附件清单指向的文件不存在。", path));
                            continue;
                        }
                        if (bytes.LongLength != expectedSize)
                            errors.Add(new ImportErrorDto(1, "附件清单/文件大小", "附件文件大小与清单不一致。", path));
                        var actualHash = Convert.ToHexString(SHA256.HashData(bytes));
                        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                            errors.Add(new ImportErrorDto(1, "附件清单/SHA-256", "附件 SHA-256 与清单不一致。", path));
                        attachmentItems.Add(new ProjectWorkbookArchiveAttachment(
                            originalFileName,
                            path, expectedSize, expectedHash,
                            item.TryGetProperty("contentType", out var typeElement) ? typeElement.GetString() ?? "application/octet-stream" : "application/octet-stream",
                            item.TryGetProperty("category", out var categoryElement) ? categoryElement.GetString() ?? "General" : "General",
                            TryString(item, "projectNumber"), TryString(item, "contractNumber"), TryString(item, "stageResultKey"),
                            item.TryGetProperty("description", out var descriptionElement) ? descriptionElement.GetString() : null,
                            bytes));
                    }
                }
                else
                {
                    errors.Add(new ImportErrorDto(1, "ZIP/manifest.json", "附件清单缺少 attachments 数组。", null));
                }
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                errors.Add(new ImportErrorDto(1, "ZIP/manifest.json", "附件清单格式无效。", exception.Message));
            }
        }
        else
        {
            errors.Add(new ImportErrorDto(1, "ZIP/manifest.json", "ZIP 缺少 manifest.json。", null));
        }

        foreach (var path in files.Keys.Where(path => path.StartsWith("attachments/", StringComparison.OrdinalIgnoreCase) && !manifestPaths.Contains(path)))
            errors.Add(new ImportErrorDto(1, "ZIP/manifest.json", "ZIP 附件未在 manifest 中登记。", path));

        if (workbook.LongLength > MaxWorkbookBytes)
            errors.Add(new ImportErrorDto(1, "ZIP/工作簿大小", "工作簿大小超过限制。", workbook.LongLength.ToString(CultureInfo.InvariantCulture)));
        return new ProjectWorkbookArchiveReadResult(workbook, attachmentItems, errors);
    }

    private static void ValidateChecksums(byte[] checksumBytes, IReadOnlyDictionary<string, byte[]> files, List<ImportErrorDto> errors)
    {
        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in Encoding.UTF8.GetString(checksumBytes).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOfAny([' ', '\t']);
            if (separator <= 0)
            {
                errors.Add(new ImportErrorDto(1, "ZIP/SHA-256", "checksums.sha256 格式无效。", line));
                continue;
            }
            var hash = line[..separator].Trim();
            var path = line[(separator + 1)..].TrimStart().TrimStart('*').Trim();
            if (hash.Length != 64 || !hash.All(Uri.IsHexDigit) || !IsSafeRelativePath(path) || !expected.TryAdd(path, hash))
            {
                errors.Add(new ImportErrorDto(1, "ZIP/SHA-256", "checksums.sha256 包含无效或重复条目。", line));
            }
        }

        foreach (var item in expected)
        {
            if (!files.TryGetValue(item.Key, out var bytes))
            {
                errors.Add(new ImportErrorDto(1, "ZIP/SHA-256", "checksums.sha256 指向的文件不存在。", item.Key));
                continue;
            }
            var actual = Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(actual, item.Value, StringComparison.OrdinalIgnoreCase))
                errors.Add(new ImportErrorDto(1, "ZIP/SHA-256", "文件 SHA-256 与 checksums.sha256 不一致。", item.Key));
        }

        if (expected.ContainsKey("checksums.sha256"))
            errors.Add(new ImportErrorDto(1, "ZIP/SHA-256", "checksums.sha256 不能校验自身。", "checksums.sha256"));

        foreach (var path in files.Keys.Where(path => path != "checksums.sha256")
                     .Where(path => !expected.ContainsKey(path)))
            errors.Add(new ImportErrorDto(1, "ZIP/SHA-256", "ZIP 文件未包含对应的 SHA-256 条目。", path));
    }

    public async Task<byte[]> CreateAsync(
        byte[] workbook,
        IReadOnlyList<Attachment> attachments,
        CancellationToken cancellationToken)
    {
        await using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            var workbookEntry = archive.CreateEntry("project-workbook.xlsx", CompressionLevel.Fastest);
            await using (var target = workbookEntry.Open())
            {
                await target.WriteAsync(workbook, cancellationToken);
            }

            var checksums = new List<string> { $"{Convert.ToHexString(SHA256.HashData(workbook))}  project-workbook.xlsx" };
            var attachmentManifest = new List<object>();
            foreach (var attachment in attachments)
            {
                var path = $"attachments/{attachment.Id:N}/{SafeName(attachment.OriginalFileName)}";
                var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
                await using var source = await fileStore.OpenReadAsync(attachment.StoredName, cancellationToken);
                await using var target = entry.Open();
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920];
                var size = 0L;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    hash.AppendData(buffer, 0, read);
                    size += read;
                }
                var sha256 = Convert.ToHexString(hash.GetHashAndReset());
                checksums.Add($"{sha256}  {path}");
                attachmentManifest.Add(new
                {
                    attachment.Id,
                    attachment.OriginalFileName,
                    path,
                    sizeBytes = size,
                    sha256,
                    attachment.ContentType,
                    category = attachment.Category.ToString(),
                    projectNumber = attachment.Project?.ProjectNumber ?? attachment.Contract?.Project?.ProjectNumber ?? attachment.StageResult?.Project?.ProjectNumber,
                    contractNumber = attachment.Contract?.ContractNumber ?? attachment.StageResult?.Contract?.ContractNumber,
                    stageResultKey = attachment.StageResult is null ? null : $"{attachment.StageResult.Title}|{attachment.StageResult.ResultDate:yyyy-MM-dd}",
                    attachment.Description
                });
            }

            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                workbookVersion = ProjectWorkbookVersions.Workbook,
                attachments = attachmentManifest
            });
            checksums.Add($"{Convert.ToHexString(SHA256.HashData(manifestBytes))}  manifest.json");
            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Fastest);
            await using (var manifestStream = manifestEntry.Open())
            {
                await manifestStream.WriteAsync(manifestBytes, cancellationToken);
            }
            var checksumEntry = archive.CreateEntry("checksums.sha256", CompressionLevel.Fastest);
            await using (var checksumStream = new StreamWriter(checksumEntry.Open(), new UTF8Encoding(false)))
            {
                await checksumStream.WriteAsync(string.Join(Environment.NewLine, checksums));
            }
        }
        return output.ToArray();
    }

    private static string SafeName(string name)
    {
        var safe = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(safe) || safe != name || safe.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException("附件文件名不安全。");
        }
        return safe;
    }

    private static bool IsSafeRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && !Path.IsPathRooted(path)
        && !path.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == "..")
        && !path.Contains(':');

    private static string? TryString(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
