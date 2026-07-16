$ErrorActionPreference = 'Stop'

$Server = 'localhost\SQLEXPRESS'
$SourceDatabase = 'EngineeringManager_Test'
$TargetDatabase = 'EngineeringManager_RestoreVerification'
$root = Split-Path -Parent $PSScriptRoot
$backupDirectory = Join-Path $root 'src\EngineeringManager.Web\App_Data\backups'
$attachmentDirectory = Join-Path $root 'src\EngineeringManager.Web\App_Data\attachments'
$resolvedRoot = [System.IO.Path]::GetFullPath($root)
$resolvedBackupDirectory = [System.IO.Path]::GetFullPath($backupDirectory)
if (-not $resolvedBackupDirectory.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Backup path escaped the workspace.' }
New-Item -ItemType Directory -Path $resolvedBackupDirectory -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$backupFile = Join-Path $resolvedBackupDirectory "EngineeringManager_Test_$stamp.bak"
$attachmentsZip = Join-Path $resolvedBackupDirectory "EngineeringManager_Attachments_$stamp.zip"
$escapedBackup = $backupFile.Replace("'", "''")
$sql = @"
BACKUP DATABASE [$SourceDatabase] TO DISK = N'$escapedBackup' WITH INIT, CHECKSUM;
DECLARE @dataLogical sysname=(SELECT name FROM sys.master_files WHERE database_id=DB_ID(N'$SourceDatabase') AND type=0);
DECLARE @logLogical sysname=(SELECT name FROM sys.master_files WHERE database_id=DB_ID(N'$SourceDatabase') AND type=1);
DECLARE @dataPath nvarchar(4000)=CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath')) + N'$TargetDatabase.mdf';
DECLARE @logPath nvarchar(4000)=CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath')) + N'${TargetDatabase}_log.ldf';
IF DB_ID(N'$TargetDatabase') IS NOT NULL BEGIN ALTER DATABASE [$TargetDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$TargetDatabase]; END;
DECLARE @restore nvarchar(max)=N'RESTORE DATABASE [$TargetDatabase] FROM DISK=N''$escapedBackup'' WITH MOVE N''' + REPLACE(@dataLogical,'''','''''') + N''' TO N''' + REPLACE(@dataPath,'''','''''') + N''', MOVE N''' + REPLACE(@logLogical,'''','''''') + N''' TO N''' + REPLACE(@logPath,'''','''''') + N''', CHECKSUM, RECOVERY';
EXEC(@restore);
IF (SELECT COUNT(*) FROM [$TargetDatabase].dbo.__EFMigrationsHistory) < 1 THROW 51000, 'Migration history missing after restore.', 1;
IF (SELECT COUNT(*) FROM [$TargetDatabase].dbo.LegalEntities) < 1 THROW 51001, 'Core sample data missing after restore.', 1;
ALTER DATABASE [$TargetDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [$TargetDatabase];
"@
& sqlcmd -S $Server -E -C -b -Q $sql
if ($LASTEXITCODE -ne 0) { throw "SQL backup restore verification failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath $attachmentDirectory)) { New-Item -ItemType Directory -Path $attachmentDirectory -Force | Out-Null }
[System.IO.Compression.ZipFile]::CreateFromDirectory($attachmentDirectory, $attachmentsZip)
Write-Output 'BACKUP_RESTORE=PASS'
Write-Output "DATABASE_BACKUP=$backupFile"
Write-Output "ATTACHMENT_BACKUP=$attachmentsZip"
Write-Output "RESTORE_DATABASE_DROPPED=$TargetDatabase"
