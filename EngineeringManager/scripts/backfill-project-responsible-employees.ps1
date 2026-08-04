[CmdletBinding()]
param(
    [string]$DatabaseName = 'EngineeringManager_Test',
    [string]$SourceWorkbook = 'D:\AI\TZM-project\old-data\旧资料项目导入模板_20260719.xlsx',
    [switch]$Preview
)

$ErrorActionPreference = 'Stop'

if ($DatabaseName -notmatch '^[A-Za-z0-9_]+_Test$') {
    throw '为避免误写正式库，DatabaseName 必须是以 _Test 结尾的数据库。'
}
if (-not (Test-Path -LiteralPath $SourceWorkbook -PathType Leaf)) {
    throw "找不到负责人来源工作簿：$SourceWorkbook"
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $projectRoot 'src\EngineeringManager.Web\appsettings.Development.json'
$settings = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$connectionString = [string]$settings.ConnectionStrings.DefaultConnection
$connectionBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
$connectionBuilder.set_ConnectionString($connectionString)
$server = if ($connectionBuilder.ContainsKey('Server')) { [string]$connectionBuilder['Server'] } else { [string]$connectionBuilder['Data Source'] }
$configuredDatabase = if ($connectionBuilder.ContainsKey('Database')) { [string]$connectionBuilder['Database'] } else { [string]$connectionBuilder['Initial Catalog'] }
if ([string]::IsNullOrWhiteSpace($server) -or [string]::IsNullOrWhiteSpace($configuredDatabase)) {
    throw 'Development 连接字符串缺少服务器或数据库名。'
}
if ($configuredDatabase -ne $DatabaseName) {
    throw "显式数据库名 $DatabaseName 与 Development 连接目标 $configuredDatabase 不一致。"
}

$sqlcmd = (Get-Command sqlcmd -ErrorAction Stop).Source
$python = (Get-Command python -ErrorAction Stop).Source
$backupDirectory = Join-Path $projectRoot 'src\EngineeringManager.Web\App_Data\backups'
$logDirectory = Join-Path $projectRoot 'src\EngineeringManager.Web\App_Data\logs'
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

function ConvertTo-SqlLiteral {
    param([AllowNull()][string]$Value)
    if ($null -eq $Value) { return 'NULL' }
    $escaped = $Value.Replace("'", "''")
    return "N'$escaped'"
}

function Invoke-SqlText {
    param([Parameter(Mandatory)][string]$Sql)

    $temporarySql = New-TemporaryFile
    try {
        [System.IO.File]::WriteAllText($temporarySql.FullName, $Sql, [System.Text.UTF8Encoding]::new($true))
        $output = & $sqlcmd -S $server -d $DatabaseName -E -C -b -I -f 65001 -y 0 -i $temporarySql.FullName 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw ("SQL 执行失败：" + [Environment]::NewLine + ($output -join [Environment]::NewLine))
        }
        return @($output)
    }
    finally {
        Remove-Item -LiteralPath $temporarySql.FullName -Force -ErrorAction SilentlyContinue
    }
}

$pythonCode = @'
import base64
import json
import re
import sys
from openpyxl import load_workbook

path = sys.argv[1]
workbook = load_workbook(path, read_only=True, data_only=True)
try:
    worksheet = workbook["项目导入"]
    rows = list(worksheet.iter_rows(values_only=True))
finally:
    workbook.close()

if not rows:
    raise ValueError("项目导入工作表没有表头")

def clean(value):
    if value is None:
        return ""
    return re.sub(r"\s+", " ", str(value).replace("\u00a0", " ")).strip()

headers = [clean(value) for value in rows[0]]
header_index = {header: index for index, header in enumerate(headers)}
for required in ("项目名称", "原始_项目经理"):
    if required not in header_index:
        raise ValueError(f"缺少负责人来源列：{required}")

def parse_manager(value):
    raw = clean(value)
    phone_match = re.search(r"(?<!\d)(1\d{10})(?!\d)", raw)
    phone = phone_match.group(1) if phone_match else None
    text = re.sub(r"(?<!\d)(1\d{10})(?!\d)", "", raw)
    text = re.sub(r"^(?:项目经理|负责人)\s*[:：]\s*", "", text)
    text = text.strip(" \t:：,，;；")
    text = text.replace("，", ",").replace("、", ",").replace("；", ",").replace(";", ",")
    text = text.replace("／", ",").replace("/", ",")
    pieces = [clean(piece) for piece in text.split(",") if clean(piece)]
    if len(pieces) == 1 and pieces[0] == "沈健马罗杰":
        pieces = ["沈健", "马罗杰"]
    names = []
    for piece in pieces:
        name = re.sub(r"(?:班组|挂靠)$", "", piece).strip()
        if name and name not in names:
            names.append(name)
    if not names:
        raise ValueError(f"无法从原始负责人文本解析人员：{raw}")
    return raw, phone, names

records = []
for source_row, row in enumerate(rows[1:], start=2):
    project_name = clean(row[header_index["项目名称"]])
    manager_value = row[header_index["原始_项目经理"]]
    if not project_name or not clean(manager_value):
        continue
    raw, phone, names = parse_manager(manager_value)
    for ordinal, name in enumerate(names):
        records.append({
            "sourceRow": source_row,
            "projectName": project_name,
            "rawManager": raw,
            "managerName": name,
            "phone": phone,
            "managerOrdinal": ordinal,
        })

print(base64.b64encode(json.dumps(records, ensure_ascii=False).encode("utf-8")).decode("ascii"))
'@

$encoded = (& $python -c $pythonCode $SourceWorkbook) -join ''
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($encoded)) {
    throw '负责人来源工作簿解析失败。'
}
$sourceRecords = @([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded)) | ConvertFrom-Json)
if ($sourceRecords.Count -eq 0) {
    throw '负责人来源工作簿没有可回填的记录。'
}

$sourceProjects = @($sourceRecords | Select-Object -ExpandProperty projectName -Unique)
$duplicateProjects = @(
    $sourceRecords | Group-Object projectName | Where-Object {
        @($_.Group | Select-Object -ExpandProperty sourceRow -Unique).Count -gt 1
    }
)
if ($duplicateProjects.Count -gt 0) {
    throw "来源工作簿存在重复项目：$($duplicateProjects.Name -join '、')"
}

$valueRows = foreach ($record in $sourceRecords) {
    $phoneLiteral = if ([string]::IsNullOrWhiteSpace([string]$record.phone)) { 'NULL' } else { ConvertTo-SqlLiteral ([string]$record.phone) }
    '({0}, {1}, {2}, {3}, {4}, {5})' -f [int]$record.sourceRow,
        (ConvertTo-SqlLiteral ([string]$record.projectName)),
        (ConvertTo-SqlLiteral ([string]$record.rawManager)),
        (ConvertTo-SqlLiteral ([string]$record.managerName)),
        $phoneLiteral,
        [int]$record.managerOrdinal
}
$sourceValues = $valueRows -join (',' + [Environment]::NewLine)

$sourceWorkbookName = Split-Path -Leaf $SourceWorkbook
$batchId = [Guid]::NewGuid()
$sourceWorkbookLiteral = ConvertTo-SqlLiteral $sourceWorkbookName
$reasonLiteral = ConvertTo-SqlLiteral "根据 $sourceWorkbookName 的“原始_项目经理”字段回填项目负责人；复合文本按来源顺序保留多人负责人。"
$previewLiteral = if ($Preview) { 1 } else { 0 }

if (-not $Preview) {
    $stamp = Get-Date -Format 'yyyyMMddHHmmss'
    $backupPath = Join-Path $backupDirectory ('EngineeringManager_ProjectResponsibleBackfill_' + $stamp + '_' + $batchId + '.bak')
    $backupLiteral = ConvertTo-SqlLiteral $backupPath
    Write-Host "正在创建 SQL Server 全量备份：$backupPath"
    Invoke-SqlText -Sql "BACKUP DATABASE [$DatabaseName] TO DISK = $backupLiteral WITH INIT, CHECKSUM, STATS = 10; RESTORE VERIFYONLY FROM DISK = $backupLiteral WITH CHECKSUM;" | Out-Null
}
else {
    $backupPath = $null
}

$sql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Preview bit = $previewLiteral;
DECLARE @BatchId uniqueidentifier = '$batchId';
DECLARE @SourceWorkbook nvarchar(260) = $sourceWorkbookLiteral;
DECLARE @Reason nvarchar(500) = $reasonLiteral;
DECLARE @AuditUserName nvarchar(100) = N'codex-maintenance';
DECLARE @Now datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Projects', N'U') IS NULL THROW 51000, N'Projects 表不存在。', 1;
    IF OBJECT_ID(N'dbo.Employees', N'U') IS NULL THROW 51001, N'Employees 表不存在。', 1;
    IF OBJECT_ID(N'dbo.ProjectResponsibleEmployees', N'U') IS NULL THROW 51002, N'ProjectResponsibleEmployees 表不存在，请先应用迁移。', 1;

    CREATE TABLE #SourceAssignments
    (
        SourceRow int NOT NULL,
        ProjectName nvarchar(200) NOT NULL,
        RawManager nvarchar(500) NOT NULL,
        ManagerName nvarchar(100) NOT NULL,
        Phone nvarchar(50) NULL,
        ManagerOrdinal int NOT NULL
    );

    INSERT INTO #SourceAssignments (SourceRow, ProjectName, RawManager, ManagerName, Phone, ManagerOrdinal)
    VALUES
    $sourceValues;

    SELECT DISTINCT ProjectName INTO #SourceProjects FROM #SourceAssignments;
    SELECT ManagerName, MIN(SourceRow) AS FirstSourceRow, MAX(NULLIF(Phone, N'')) AS SourcePhone,
           COUNT(DISTINCT NULLIF(Phone, N'')) AS PhoneCount
    INTO #ManagerNames
    FROM #SourceAssignments
    GROUP BY ManagerName;

    IF EXISTS (SELECT 1 FROM #SourceProjects sp LEFT JOIN dbo.Projects p ON p.Name = sp.ProjectName GROUP BY sp.ProjectName HAVING COUNT(p.Id) <> 1)
        THROW 51003, N'来源项目名称无法唯一匹配当前项目。', 1;
    IF EXISTS (SELECT 1 FROM #ManagerNames WHERE PhoneCount > 1)
        THROW 51004, N'同一负责人在来源中出现多个不同手机号。', 1;
    IF EXISTS (SELECT 1 FROM #ManagerNames n INNER JOIN dbo.Employees e ON e.Name = n.ManagerName GROUP BY n.ManagerName HAVING COUNT(e.Id) > 1)
        THROW 51005, N'员工管理中存在同名员工，无法安全回填。', 1;

    DECLARE @NextEmployeeSequence int = COALESCE((
        SELECT MAX(TRY_CONVERT(int, SUBSTRING(EmployeeNumber, 3, 60)))
        FROM dbo.Employees
        WHERE EmployeeNumber LIKE N'YG%' AND TRY_CONVERT(int, SUBSTRING(EmployeeNumber, 3, 60)) IS NOT NULL
    ), 0) + 1;
    IF @NextEmployeeSequence + (SELECT COUNT(*) FROM #ManagerNames n WHERE NOT EXISTS (SELECT 1 FROM dbo.Employees e WHERE e.Name = n.ManagerName)) - 1 > 9999
        THROW 51006, N'YG 员工编号已超过四位序号范围。', 1;

    SELECT n.ManagerName, n.SourcePhone, n.FirstSourceRow,
           CONCAT(N'YG', RIGHT(CONCAT(N'0000', CONVERT(varchar(10), @NextEmployeeSequence + ROW_NUMBER() OVER (ORDER BY n.FirstSourceRow, n.ManagerName) - 1)), 4)) AS EmployeeNumber
    INTO #MissingEmployees
    FROM #ManagerNames n
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Employees e WHERE e.Name = n.ManagerName);

    INSERT INTO dbo.Employees
    (Id, EmployeeNumber, Name, EmployeeType, Phone, PositionTitle, Notes, IsActive, IsProjectResponsible, CreatedAt, UpdatedAt, ConcurrencyStamp)
    SELECT NEWID(), EmployeeNumber, ManagerName, 2, SourcePhone, N'项目负责人',
           CONCAT(N'历史负责人回填，来源文件：', @SourceWorkbook, N'，来源行：', CONVERT(nvarchar(20), FirstSourceRow)),
           1, 1, @Now, @Now, NEWID()
    FROM #MissingEmployees;
    DECLARE @CreatedEmployees int = @@ROWCOUNT;

    SELECT n.ManagerName, e.Id AS EmployeeId, e.EmployeeNumber, e.Phone,
           CAST(CASE WHEN m.ManagerName IS NULL THEN 0 ELSE 1 END AS bit) AS IsNew
    INTO #EmployeeMap
    FROM #ManagerNames n
    INNER JOIN dbo.Employees e ON e.Name = n.ManagerName
    LEFT JOIN #MissingEmployees m ON m.ManagerName = n.ManagerName;

    UPDATE e
    SET IsProjectResponsible = 1, UpdatedAt = @Now, ConcurrencyStamp = NEWID()
    FROM dbo.Employees e
    INNER JOIN #EmployeeMap m ON m.EmployeeId = e.Id
    WHERE e.IsProjectResponsible = 0;
    DECLARE @EnabledEmployees int = @@ROWCOUNT;

    UPDATE e
    SET Phone = n.SourcePhone, UpdatedAt = @Now, ConcurrencyStamp = NEWID()
    FROM dbo.Employees e
    INNER JOIN #ManagerNames n ON n.ManagerName = e.Name
    WHERE NULLIF(LTRIM(RTRIM(e.Phone)), N'') IS NULL AND NULLIF(n.SourcePhone, N'') IS NOT NULL;
    DECLARE @FilledPhones int = @@ROWCOUNT;

    SELECT p.Id AS ProjectId, p.ProjectNumber, p.Name AS ProjectName,
           a.SourceRow, a.RawManager, a.ManagerName, a.ManagerOrdinal, a.Phone,
           m.EmployeeId, m.EmployeeNumber
    INTO #ResolvedAssignments
    FROM #SourceAssignments a
    INNER JOIN dbo.Projects p ON p.Name = a.ProjectName
    INNER JOIN #EmployeeMap m ON m.ManagerName = a.ManagerName;

    IF EXISTS (SELECT ProjectId, EmployeeId FROM #ResolvedAssignments GROUP BY ProjectId, EmployeeId HAVING COUNT(*) > 1)
        THROW 51007, N'同一项目中出现重复负责人。', 1;

    SELECT p.Id, p.ProjectNumber, p.Name,
           p.ResponsibleEmployeeId AS BeforePrimaryEmployeeId,
           desired.EmployeeId AS DesiredPrimaryEmployeeId,
           CAST(CASE WHEN p.ResponsibleEmployeeId IS NULL OR p.ResponsibleEmployeeId <> desired.EmployeeId
                          OR EXISTS (SELECT 1 FROM dbo.ProjectResponsibleEmployees l WHERE l.ProjectId = p.Id AND NOT EXISTS (SELECT 1 FROM #ResolvedAssignments r WHERE r.ProjectId = l.ProjectId AND r.EmployeeId = l.EmployeeId))
                          OR EXISTS (SELECT 1 FROM #ResolvedAssignments r WHERE r.ProjectId = p.Id AND NOT EXISTS (SELECT 1 FROM dbo.ProjectResponsibleEmployees l WHERE l.ProjectId = r.ProjectId AND l.EmployeeId = r.EmployeeId))
                          OR EXISTS (SELECT 1 FROM dbo.ProjectResponsibleEmployees l INNER JOIN #ResolvedAssignments r ON r.ProjectId = l.ProjectId AND r.EmployeeId = l.EmployeeId WHERE l.ProjectId = p.Id AND (l.SortOrder <> r.ManagerOrdinal OR l.IsPrimary <> CASE WHEN r.ManagerOrdinal = 0 THEN 1 ELSE 0 END))
                     THEN 1 ELSE 0 END AS bit) AS Changed
    INTO #ProjectSnapshots
    FROM dbo.Projects p
    INNER JOIN #SourceProjects sp ON sp.ProjectName = p.Name
    CROSS APPLY (SELECT TOP (1) r.EmployeeId FROM #ResolvedAssignments r WHERE r.ProjectId = p.Id ORDER BY r.ManagerOrdinal, r.EmployeeId) desired;

    IF EXISTS (
        SELECT 1 FROM #ProjectSnapshots s
        WHERE s.BeforePrimaryEmployeeId IS NOT NULL
          AND s.BeforePrimaryEmployeeId <> s.DesiredPrimaryEmployeeId
          AND NOT EXISTS (SELECT 1 FROM #ResolvedAssignments r WHERE r.ProjectId = s.Id AND r.EmployeeId = s.BeforePrimaryEmployeeId)
    )
        THROW 51008, N'存在已有负责人且与历史负责人完全冲突的项目，已停止以避免覆盖现有数据。', 1;

    DECLARE @ProjectChanges int = (SELECT COUNT(*) FROM #ProjectSnapshots WHERE Changed = 1);

    DELETE l
    FROM dbo.ProjectResponsibleEmployees l
    INNER JOIN #ProjectSnapshots s ON s.Id = l.ProjectId
    WHERE NOT EXISTS (SELECT 1 FROM #ResolvedAssignments r WHERE r.ProjectId = l.ProjectId AND r.EmployeeId = l.EmployeeId);
    DECLARE @DeletedLinks int = @@ROWCOUNT;

    UPDATE l
    SET SortOrder = r.ManagerOrdinal,
        IsPrimary = CASE WHEN r.ManagerOrdinal = 0 THEN 1 ELSE 0 END,
        UpdatedAt = @Now,
        ConcurrencyStamp = NEWID()
    FROM dbo.ProjectResponsibleEmployees l
    INNER JOIN #ResolvedAssignments r ON r.ProjectId = l.ProjectId AND r.EmployeeId = l.EmployeeId
    WHERE l.SortOrder <> r.ManagerOrdinal OR l.IsPrimary <> CASE WHEN r.ManagerOrdinal = 0 THEN 1 ELSE 0 END;
    DECLARE @UpdatedLinks int = @@ROWCOUNT;

    INSERT INTO dbo.ProjectResponsibleEmployees
    (Id, ProjectId, EmployeeId, SortOrder, IsPrimary, CreatedAt, UpdatedAt, ConcurrencyStamp)
    SELECT NEWID(), r.ProjectId, r.EmployeeId, r.ManagerOrdinal,
           CASE WHEN r.ManagerOrdinal = 0 THEN 1 ELSE 0 END, @Now, @Now, NEWID()
    FROM #ResolvedAssignments r
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ProjectResponsibleEmployees l WHERE l.ProjectId = r.ProjectId AND l.EmployeeId = r.EmployeeId);
    DECLARE @InsertedLinks int = @@ROWCOUNT;

    UPDATE p
    SET ResponsibleEmployeeId = s.DesiredPrimaryEmployeeId,
        UpdatedAt = @Now,
        ConcurrencyStamp = NEWID()
    FROM dbo.Projects p
    INNER JOIN #ProjectSnapshots s ON s.Id = p.Id
    WHERE s.Changed = 1;

    DECLARE @AuditRows int = 0;
    IF @CreatedEmployees + @EnabledEmployees + @FilledPhones + @ProjectChanges + @DeletedLinks + @UpdatedLinks + @InsertedLinks > 0
    BEGIN
        DECLARE @AuditJson nvarchar(max) = (
            SELECT @BatchId AS batchId, @SourceWorkbook AS sourceWorkbook,
                   (SELECT COUNT(*) FROM #SourceAssignments) AS sourceAssignmentRows,
                   (SELECT COUNT(*) FROM #SourceProjects) AS sourceProjects,
                   (SELECT COUNT(*) FROM #ManagerNames) AS sourceManagers,
                   @CreatedEmployees AS createdEmployees, @EnabledEmployees AS enabledEmployees, @FilledPhones AS filledPhones,
                   @ProjectChanges AS changedProjects, @DeletedLinks AS deletedLinks, @UpdatedLinks AS updatedLinks, @InsertedLinks AS insertedLinks,
                   JSON_QUERY((SELECT ManagerName, EmployeeId, EmployeeNumber, Phone FROM #EmployeeMap WHERE IsNew = 1 FOR JSON PATH)) AS newEmployees,
                   JSON_QUERY((SELECT ProjectName, ManagerName, ManagerOrdinal, Phone, SourceRow FROM #ResolvedAssignments ORDER BY ProjectName, ManagerOrdinal FOR JSON PATH)) AS assignments
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
        );
        INSERT INTO dbo.AuditLogs
        (UserId, UserName, OccurredAt, Action, EntityType, EntityId, Reason, AfterJson, RequestId)
        VALUES (NULL, @AuditUserName, @Now, N'BackfillProjectResponsibleEmployees', N'Maintenance',
                CONVERT(nvarchar(100), @BatchId), @Reason, @AuditJson, CONVERT(nvarchar(100), @BatchId));
        SET @AuditRows = 1;
    END;

    DECLARE @Report nvarchar(max) = (
        SELECT @Preview AS preview, CONVERT(nvarchar(36), @BatchId) AS batchId,
               @SourceWorkbook AS sourceWorkbook,
               (SELECT COUNT(*) FROM #SourceAssignments) AS sourceAssignmentRows,
               (SELECT COUNT(*) FROM #SourceProjects) AS sourceProjects,
               (SELECT COUNT(*) FROM #ManagerNames) AS sourceManagers,
               @CreatedEmployees AS createdEmployees, @EnabledEmployees AS enabledEmployees, @FilledPhones AS filledPhones,
               @ProjectChanges AS changedProjects, @DeletedLinks AS deletedLinks, @UpdatedLinks AS updatedLinks, @InsertedLinks AS insertedLinks,
               @AuditRows AS auditRows,
               (SELECT COUNT(*) FROM #ResolvedAssignments WHERE ManagerOrdinal > 0) AS additionalResponsibleLinks,
               (SELECT COUNT(*) FROM #SourceProjects sp WHERE EXISTS (SELECT 1 FROM #ResolvedAssignments r WHERE r.ProjectName = sp.ProjectName AND r.ManagerOrdinal > 0)) AS multiResponsibleProjects
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    );

    IF @Preview = 1
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT @Report AS ReportJson;
        RETURN;
    END;

    COMMIT TRANSACTION;
    SELECT @Report AS ReportJson;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
"@

$operationMessage = if ($Preview) { '正在执行负责人回填预演（不会写入数据库）...' } else { '正在执行负责人回填...' }
Write-Host $operationMessage
$sqlOutput = Invoke-SqlText -Sql $sql
$jsonLine = @($sqlOutput | ForEach-Object { [string]$_ } | Where-Object {
    $_.TrimStart().StartsWith('{') -or $_.TrimStart().StartsWith('[')
} | Select-Object -Last 1)
if ($jsonLine.Count -ne 1) {
    throw ("SQL 已执行但未返回 JSON 报告：" + [Environment]::NewLine + ($sqlOutput -join [Environment]::NewLine))
}
$report = $jsonLine[0].Trim() | ConvertFrom-Json

$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$reportPath = Join-Path $logDirectory ('project-responsible-backfill-' + $stamp + '_' + $batchId + '.json')
$report | Add-Member -NotePropertyName database -NotePropertyValue $DatabaseName
$report | Add-Member -NotePropertyName server -NotePropertyValue $server
$report | Add-Member -NotePropertyName reportPath -NotePropertyValue $reportPath
$report | Add-Member -NotePropertyName backupPath -NotePropertyValue $backupPath
[System.IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 20), [System.Text.UTF8Encoding]::new($true))
$report | ConvertTo-Json -Depth 20
