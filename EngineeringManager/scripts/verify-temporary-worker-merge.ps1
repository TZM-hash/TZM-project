param(
    [Parameter(Mandatory = $true)]
    [string]$DatabaseName
)

$ErrorActionPreference = 'Stop'

if ($DatabaseName -ne 'EngineeringManager_Test' -or $DatabaseName -notmatch '_Test$') {
    throw '只允许验证 EngineeringManager_Test 测试数据库。'
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $projectRoot 'src\EngineeringManager.Web\appsettings.Development.json'
$settings = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$connectionString = [string]$settings.ConnectionStrings.DefaultConnection
$connectionBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
$connectionBuilder.set_ConnectionString($connectionString)
$server = if ($connectionBuilder.ContainsKey('Server')) {
    [string]$connectionBuilder['Server']
} elseif ($connectionBuilder.ContainsKey('Data Source')) {
    [string]$connectionBuilder['Data Source']
} else {
    throw 'Development 连接字符串未声明 SQL Server。'
}
$actualDatabase = if ($connectionBuilder.ContainsKey('Database')) {
    [string]$connectionBuilder['Database']
} elseif ($connectionBuilder.ContainsKey('Initial Catalog')) {
    [string]$connectionBuilder['Initial Catalog']
} else {
    throw 'Development 连接字符串未声明数据库名。'
}
if ($actualDatabase -ne $DatabaseName) {
    throw "显式数据库名 $DatabaseName 与 Development 连接目标 $actualDatabase 不一致。"
}

$sqlcmd = (Get-Command sqlcmd -ErrorAction Stop).Source
$dotnetRoot = Join-Path $projectRoot '.dotnet'
$dotnetToolDirectory = Join-Path $projectRoot '.tools\dotnet-tools'
$dotnetEf = Join-Path $dotnetToolDirectory 'dotnet-ef.exe'
$infrastructureProject = Join-Path $projectRoot 'src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj'
$webProject = Join-Path $projectRoot 'src\EngineeringManager.Web\EngineeringManager.Web.csproj'
if (-not (Test-Path -LiteralPath $dotnetEf)) {
    throw "本地 EF 工具不存在：$dotnetEf"
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot$([IO.Path]::PathSeparator)$dotnetToolDirectory$([IO.Path]::PathSeparator)$env:PATH"

function Invoke-Ef {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $output = & $dotnetEf @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "EF 命令失败：$($Arguments -join ' ')`n$($output -join [Environment]::NewLine)"
    }
}

function Invoke-TestSql {
    param([Parameter(Mandatory = $true)][string]$Sql)

    $output = & $sqlcmd -S $server -d $DatabaseName -E -C -b -I -y 0 -Q $Sql 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "SQL 验证失败：`n$($output -join [Environment]::NewLine)"
    }
    $meaningfulLines = @($output | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
    if ($meaningfulLines.Count -eq 0) {
        return ''
    }
    return ([string]$meaningfulLines[-1]).Trim()
}

$commonEfArguments = @(
    '--project', $infrastructureProject,
    '--startup-project', $webProject
)
Invoke-Ef -Arguments (@('database', 'drop', '--force') + $commonEfArguments)
Invoke-Ef -Arguments (@('database', 'update', '20260718061345_ProjectOverviewStatusTaxEquipment') + $commonEfArguments)

$seedSql = @'
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @ProjectId uniqueidentifier = '20000000-0000-0000-0000-000000000001';
DECLARE @ExistingEmployeeId uniqueidentifier = '10000000-0000-0000-0000-000000000001';
DECLARE @ConvertedTemporaryId uniqueidentifier = '30000000-0000-0000-0000-000000000001';
DECLARE @NewTemporaryId uniqueidentifier = '30000000-0000-0000-0000-000000000002';

INSERT INTO [Projects]
    ([Id], [ProjectNumber], [Name], [Stage], [IsActive], [CreatedAt], [UpdatedAt], [ConcurrencyStamp], [AffiliationType], [ContractSigningStatus])
VALUES
    (@ProjectId, N'MIG-P-001', N'迁移验证项目', 2, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), NEWID(), 1, 1);

INSERT INTO [Employees]
    ([Id], [EmployeeNumber], [Name], [EmployeeType], [Phone], [IdentityNumber], [BankAccountNumber], [BankName],
     [HireDate], [LeaveDate], [PositionTitle], [DefaultLegalEntityId], [DefaultMonthlySalary], [DefaultDailyRate],
     [DefaultHourlyRate], [DefaultPieceworkRate], [IsActive], [CreatedAt], [UpdatedAt], [ConcurrencyStamp], [Notes])
VALUES
    (@ExistingEmployeeId, N'MIG-E-001', N'已转正员工', 1, NULL, N'ID-CONVERTED', N'KEEP-BANK', NULL,
     NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, '2025-01-01T00:00:00+08:00', '2025-01-01T00:00:00+08:00', NEWID(), NULL);

INSERT INTO [TemporaryWorkers]
    ([Id], [Name], [IdentityNumber], [Phone], [BankAccountNumber], [BankName], [Trade], [DefaultProjectId],
     [ConvertedEmployeeId], [Notes], [IsActive], [CreatedAt], [UpdatedAt], [ConcurrencyStamp])
VALUES
    (@ConvertedTemporaryId, N'已转正历史临时人员', N'ID-CONVERTED', N'13800000001', N'LEGACY-BANK', N'迁移银行', N'钢筋工', @ProjectId,
     @ExistingEmployeeId, N'转换档案备注', 1, '2025-02-01T00:00:00+08:00', '2025-02-02T00:00:00+08:00', NEWID()),
    (@NewTemporaryId, N'未转正临时人员', N'ID-NEW-TEMP', N'13800000002', N'NEW-BANK', N'新银行', N'杂工', @ProjectId,
     NULL, N'新建特殊人员备注', 0, '2025-03-01T00:00:00+08:00', '2025-03-02T00:00:00+08:00', NEWID());

INSERT INTO [PayrollBatches]
    ([Id], [BatchNumber], [Name], [BatchType], [StartDate], [EndDate], [ProjectId], [LegalEntityId],
     [StageOrMilestoneName], [Status], [Notes], [CreatedAt], [ConcurrencyStamp], [AccountId], [AccountTransactionId],
     [ActualAmount], [IsUnifiedDisbursement], [PaymentDate], [PaymentMethod], [ReviewedAt], [ReviewedByUserId], [UpdatedAt], [VoucherNumber])
VALUES
    ('40000000-0000-0000-0000-000000000001', N'MIG-B-001', N'转换人员历史工资', 5, '2025-02-01', '2025-02-28', @ProjectId, NULL,
     NULL, 3, N'保留批次一', '2025-02-28T00:00:00+08:00', NEWID(), NULL, NULL,
     1234.56, 1, '2025-02-28', 1, NULL, NULL, '2025-02-28T00:00:00+08:00', N'V-001'),
    ('40000000-0000-0000-0000-000000000002', N'MIG-B-002', N'未转换人员历史工资', 5, '2025-03-01', '2025-03-31', @ProjectId, NULL,
     NULL, 3, N'保留批次二', '2025-03-31T00:00:00+08:00', NEWID(), NULL, NULL,
     2345.67, 1, '2025-03-31', 1, NULL, NULL, '2025-03-31T00:00:00+08:00', N'V-002');

INSERT INTO [PayrollPayments]
    ([Id], [PayrollBatchId], [EmployeeId], [AccountId], [PaymentDate], [Amount], [PaymentMethod], [PayeeType], [PayeeName],
     [PayeeBusinessPartnerId], [Notes], [AccountTransactionId], [ConcurrencyStamp], [BankAccountSnapshot],
     [ConstructionWorkerId], [CreatedAt], [CrewBusinessPartnerId], [CrewNameSnapshot], [IdentityNumberSnapshot],
     [PhoneSnapshot], [RecipientKey], [RecipientNameSnapshot], [RecipientType], [TemporaryWorkerId], [TradeSnapshot])
VALUES
    ('50000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000001', NULL, NULL, '2025-02-28', 1234.56, 1, 3, N'已转正历史临时人员',
     NULL, N'付款备注一', '60000000-0000-0000-0000-000000000001', NEWID(), N'SNAPSHOT-BANK-1',
     NULL, '2025-02-28T00:00:00+08:00', NULL, NULL, N'ID-CONVERTED',
     N'13800000001', N'temporary-worker:30000000000000000000000000000001', N'已转正历史临时人员', 3, @ConvertedTemporaryId, N'钢筋工'),
    ('50000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000002', NULL, NULL, '2025-03-31', 2345.67, 1, 3, N'未转正临时人员',
     NULL, N'付款备注二', '60000000-0000-0000-0000-000000000002', NEWID(), N'SNAPSHOT-BANK-2',
     NULL, '2025-03-31T00:00:00+08:00', NULL, NULL, N'ID-NEW-TEMP',
     N'13800000002', N'temporary-worker:30000000000000000000000000000002', N'未转正临时人员', 3, @NewTemporaryId, N'杂工');

COMMIT TRANSACTION;
'@
Invoke-TestSql -Sql $seedSql | Out-Null

$beforeSql = @'
SET NOCOUNT ON;
SELECT
    (SELECT COUNT(*) FROM [TemporaryWorkers]) AS [TemporaryWorkerCount],
    (SELECT COUNT(*) FROM [PayrollPayments] WHERE [RecipientType] = 3) AS [TemporaryPaymentCount],
    (SELECT SUM([Amount]) FROM [PayrollPayments] WHERE [RecipientType] = 3) AS [TemporaryPaymentAmount],
    (SELECT COUNT(*) FROM [PayrollPayments] WHERE [AccountTransactionId] IS NOT NULL) AS [AccountTransactionReferenceCount],
    (SELECT SUM([Amount]) FROM [PayrollPayments] WHERE YEAR([PaymentDate]) = 2025) AS [AnnualPayrollAmount]
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
'@
$before = Invoke-TestSql -Sql $beforeSql | ConvertFrom-Json

Invoke-Ef -Arguments (@('database', 'update') + $commonEfArguments)

$afterSql = @'
SET NOCOUNT ON;
SELECT
    CASE WHEN OBJECT_ID(N'[TemporaryWorkers]', N'U') IS NULL THEN 1 ELSE 0 END AS [LegacyTableRemoved],
    CASE WHEN COL_LENGTH(N'PayrollPayments', N'TemporaryWorkerId') IS NULL THEN 1 ELSE 0 END AS [LegacyColumnRemoved],
    (SELECT COUNT(*) FROM [PersonnelMigrationMaps]) AS [MigrationMapCount],
    (SELECT COUNT(*) FROM [Employees] WHERE [EmployeeType] = 3) AS [NewTemporaryEmployeeCount],
    (SELECT COUNT(*) FROM [PayrollPayments] WHERE [RecipientType] = 1 AND [EmployeeId] IS NOT NULL) AS [EmployeePaymentCount],
    (SELECT SUM([Amount]) FROM [PayrollPayments]) AS [EmployeePaymentAmount],
    (SELECT COUNT(*) FROM [PayrollPayments] WHERE [AccountTransactionId] IS NOT NULL) AS [AccountTransactionReferenceCount],
    (SELECT SUM([Amount]) FROM [PayrollPayments] WHERE YEAR([PaymentDate]) = 2025) AS [AnnualPayrollAmount],
    (SELECT COUNT(*) FROM [EmployeeAffiliationHistories] WHERE [ProjectId] = '20000000-0000-0000-0000-000000000001') AS [AffiliationCount],
    (SELECT COUNT(*) FROM [PayrollPayments]
        WHERE [Notes] IN (N'付款备注一', N'付款备注二')
          AND [BankAccountSnapshot] IN (N'SNAPSHOT-BANK-1', N'SNAPSHOT-BANK-2')
          AND [AccountTransactionId] IS NOT NULL) AS [PreservedPaymentCount],
    (SELECT COUNT(*) FROM [Employees]
        WHERE [Id] = '10000000-0000-0000-0000-000000000001'
          AND [Phone] = N'13800000001'
          AND [BankAccountNumber] = N'KEEP-BANK'
          AND [BankName] = N'迁移银行'
          AND [PositionTitle] = N'钢筋工') AS [ConvertedEmployeeFilledSafely],
    (SELECT COUNT(*) FROM [Employees] AS [employee]
        INNER JOIN [PersonnelMigrationMaps] AS [map] ON [map].[EmployeeId] = [employee].[Id]
        WHERE [map].[LegacyTemporaryWorkerId] = '30000000-0000-0000-0000-000000000002'
          AND [employee].[EmployeeNumber] = N'TMP-30000000000000000000000000000002'
          AND [employee].[EmployeeType] = 3
          AND [employee].[IsActive] = 0) AS [NewEmployeeCreatedCorrectly]
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
'@
$after = Invoke-TestSql -Sql $afterSql | ConvertFrom-Json

if ($before.TemporaryWorkerCount -ne 2 -or $before.TemporaryPaymentCount -ne 2) {
    throw '迁移前夹具数量不符合预期。'
}
if ([decimal]($before.TemporaryPaymentAmount) -ne [decimal]3580.23) {
    throw '迁移前工资金额不符合预期。'
}
if ($after.LegacyTableRemoved -ne 1 -or $after.LegacyColumnRemoved -ne 1) {
    throw '旧临时人员表或工资外键列仍然存在。'
}
if ($after.MigrationMapCount -ne 2 -or $after.NewTemporaryEmployeeCount -ne 1) {
    throw '人员映射或新特殊临时员工数量不正确。'
}
if ($after.EmployeePaymentCount -ne 2 -or [decimal]($after.EmployeePaymentAmount) -ne [decimal]($before.TemporaryPaymentAmount)) {
    throw '历史工资付款数量或金额未完整迁移。'
}
if ($after.AccountTransactionReferenceCount -ne $before.AccountTransactionReferenceCount) {
    throw '工资付款的账户交易引用数量发生变化。'
}
if ([decimal]($after.AnnualPayrollAmount) -ne [decimal]($before.AnnualPayrollAmount)) {
    throw '年度工资金额发生变化。'
}
if ($after.AffiliationCount -ne 2 -or $after.PreservedPaymentCount -ne 2) {
    throw '项目归属或工资快照字段未完整保留。'
}
if ($after.ConvertedEmployeeFilledSafely -ne 1 -or $after.NewEmployeeCreatedCorrectly -ne 1) {
    throw '已转正员工复用或新特殊临时员工生成不正确。'
}

$artifactDirectory = Join-Path $projectRoot 'artifacts\temporary-worker-merge'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$reportPath = Join-Path $artifactDirectory 'verification-report.json'
[PSCustomObject]@{
    Database = $DatabaseName
    PreviousMigration = '20260718061345_ProjectOverviewStatusTaxEquipment'
    TargetMigration = '20260718155105_MergeTemporaryWorkersIntoEmployees'
    Before = $before
    After = $after
    VerifiedAt = [DateTimeOffset]::Now
    Status = 'Passed'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8
