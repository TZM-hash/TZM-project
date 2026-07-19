param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$TargetDatabase,
    [Parameter(Mandatory = $true)][string]$SqlServer,
    [Parameter(Mandatory = $true)][string]$AttachmentRoot,
    [string]$WorkingDirectory = (Join-Path $PSScriptRoot '..\App_Data\restore-work'),
    [string]$MaintenanceFile = (Join-Path $PSScriptRoot '..\App_Data\maintenance.flag')
)

$ErrorActionPreference = 'Stop'

if ($TargetDatabase -notmatch '^[A-Za-z0-9_-]+$' -or $TargetDatabase -notmatch '(^|[_-])Test([_-]|$)') { throw '完整恢复工具只允许目标数据库名使用安全字符且包含 Test。' }
$resolvedPackage = [System.IO.Path]::GetFullPath($PackagePath)
if (-not (Test-Path -LiteralPath $resolvedPackage)) { throw "备份包不存在：$resolvedPackage" }
$resolvedWork = [System.IO.Path]::GetFullPath($WorkingDirectory)
New-Item -ItemType Directory -Path $resolvedWork -Force | Out-Null
$resolvedMaintenance = [System.IO.Path]::GetFullPath($MaintenanceFile)
$maintenanceDirectory = Split-Path -Parent $resolvedMaintenance
New-Item -ItemType Directory -Path $maintenanceDirectory -Force | Out-Null
$extract = Join-Path $resolvedWork ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $extract -Force | Out-Null
$restoreSucceeded = $false
try {
    Set-Content -LiteralPath $resolvedMaintenance -Value "RESTORE_STARTED=$(Get-Date -Format o)" -Encoding UTF8
    Expand-Archive -LiteralPath $resolvedPackage -DestinationPath $extract -Force
    $manifestPath = Join-Path $extract 'manifest.json'
    $checksumsPath = Join-Path $extract 'checksums.sha256'
    if (-not (Test-Path -LiteralPath $manifestPath) -or -not (Test-Path -LiteralPath $checksumsPath)) { throw '备份包缺少 manifest.json 或 checksums.sha256。' }
    foreach ($line in Get-Content -LiteralPath $checksumsPath -Encoding UTF8) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split 's{2,}', 2
        if ($parts.Count -ne 2) { throw "哈希清单格式错误：$line" }
        $file = Join-Path $extract $parts[1]
        if (-not (Test-Path -LiteralPath $file)) { throw "备份包缺少文件：$($parts[1])" }
        $actual = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        if ($actual -ne $parts[0]) { throw "文件哈希不匹配：$($parts[1])" }
    }
    $databaseBackup = Join-Path $extract 'database\EngineeringManager.bak'
    $attachmentArchive = Join-Path $extract 'attachments\attachments.zip'
    if (-not (Test-Path -LiteralPath $databaseBackup)) { throw '完整备份缺少数据库备份。' }
    $escapedBackup = $databaseBackup.Replace("'", "''")
    $preRestore = (Join-Path $resolvedWork "$TargetDatabase.pre-restore.bak").Replace("'", "''")
    $sql = "IF DB_ID(N'$TargetDatabase') IS NOT NULL BEGIN BACKUP DATABASE [$TargetDatabase] TO DISK=N'$preRestore' WITH INIT, CHECKSUM; ALTER DATABASE [$TargetDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$TargetDatabase]; END; RESTORE DATABASE [$TargetDatabase] FROM DISK=N'$escapedBackup' WITH REPLACE, CHECKSUM, RECOVERY;"
    & sqlcmd -S $SqlServer -E -C -b -Q $sql
    if ($LASTEXITCODE -ne 0) { throw "数据库恢复失败，退出码：$LASTEXITCODE" }
    $healthSql = "IF DB_ID(N'$TargetDatabase') IS NULL THROW 52000, '恢复后数据库不存在。', 1; IF OBJECT_ID(N'$TargetDatabase.dbo.__EFMigrationsHistory') IS NULL THROW 52001, '恢复后缺少迁移历史。', 1;"
    & sqlcmd -S $SqlServer -E -C -b -Q $healthSql
    if ($LASTEXITCODE -ne 0) { throw "恢复后健康检查失败，退出码：$LASTEXITCODE" }
    if (Test-Path -LiteralPath $attachmentArchive) {
        $staging = Join-Path $resolvedWork 'attachments-staging'
        if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
        Expand-Archive -LiteralPath $attachmentArchive -DestinationPath $staging -Force
        $resolvedAttachments = [System.IO.Path]::GetFullPath($AttachmentRoot)
        $backupBeforeRestore = "$resolvedAttachments.pre-restore"
        if (Test-Path -LiteralPath $backupBeforeRestore) { Remove-Item -LiteralPath $backupBeforeRestore -Recurse -Force }
        if (Test-Path -LiteralPath $resolvedAttachments) { Move-Item -LiteralPath $resolvedAttachments -Destination $backupBeforeRestore }
        Move-Item -LiteralPath $staging -Destination $resolvedAttachments
    }
    $restoreSucceeded = $true
    Write-Output 'RESTORE_BACKUP=PASS'
    Write-Output "TARGET_DATABASE=$TargetDatabase"
}
finally {
    if (Test-Path -LiteralPath $extract) { Remove-Item -LiteralPath $extract -Recurse -Force }
    if ($restoreSucceeded -and (Test-Path -LiteralPath $resolvedMaintenance)) { Remove-Item -LiteralPath $resolvedMaintenance -Force }
    elseif (-not $restoreSucceeded) { Write-Warning "恢复失败，维护标记已保留：$resolvedMaintenance" }
}
