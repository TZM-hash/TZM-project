$ErrorActionPreference = 'Stop'

$databaseName = 'EngineeringManager_Test'
$execute = $false
for ($index = 0; $index -lt $args.Count; $index++) {
    if ($args[$index] -ieq '-Execute') {
        $execute = $true
        continue
    }
    if ($args[$index] -ieq '-DatabaseName' -and $index + 1 -lt $args.Count) {
        $databaseName = [string]$args[++$index]
        continue
    }
    throw "未知参数：$($args[$index])。仅支持 -DatabaseName <名称> 和 -Execute。"
}

if ($databaseName -notmatch '^[A-Za-z0-9_]+$' -or $databaseName -notmatch '_Test$') {
    throw '只允许清理名称以 _Test 结尾且仅包含字母、数字和下划线的测试数据库。'
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $projectRoot 'src\EngineeringManager.Web\appsettings.Development.json'
$settings = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$connectionString = [string]$settings.ConnectionStrings.DefaultConnection
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw 'Development 配置未提供 DefaultConnection。'
}

$connectionBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
$connectionBuilder.set_ConnectionString($connectionString)
$actualDatabase = if ($connectionBuilder.ContainsKey('Database')) {
    [string]$connectionBuilder['Database']
} elseif ($connectionBuilder.ContainsKey('Initial Catalog')) {
    [string]$connectionBuilder['Initial Catalog']
} else {
    throw 'Development 连接字符串未声明数据库名。'
}
$actualServer = if ($connectionBuilder.ContainsKey('Server')) {
    [string]$connectionBuilder['Server']
} elseif ($connectionBuilder.ContainsKey('Data Source')) {
    [string]$connectionBuilder['Data Source']
} else {
    throw 'Development 连接字符串未声明服务器。'
}

if ($actualDatabase -ne $databaseName) {
    throw "显式数据库名 $databaseName 与 Development 连接目标 $actualDatabase 不一致。"
}
if ($actualServer -notlike 'localhost*' -and $actualServer -notlike '.\SQLEXPRESS') {
    throw "只允许清理本机 SQL Server 测试库，当前服务器为 $actualServer。"
}

$connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)

function Get-QueryRows([System.Data.SqlClient.SqlConnection]$SqlConnection, [string]$Sql) {
    $command = $SqlConnection.CreateCommand()
    $command.CommandText = $Sql
    $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($command)
    $table = [System.Data.DataTable]::new()
    [void]$adapter.Fill($table)
    $rows = foreach ($row in $table.Rows) {
        $object = [ordered]@{}
        foreach ($column in $table.Columns) {
            $value = $row[$column.ColumnName]
            if ($value -is [DBNull]) {
                $value = $null
            }
            $object[$column.ColumnName] = $value
        }
        [pscustomobject]$object
    }
    return @($rows)
}

function Get-Scalar([System.Data.SqlClient.SqlConnection]$SqlConnection, [string]$Sql) {
    $command = $SqlConnection.CreateCommand()
    $command.CommandText = $Sql
    return $command.ExecuteScalar()
}

$countSql = @'
SELECT 'DemoLegalEntities' AS Item, COUNT_BIG(*) AS [Count] FROM dbo.LegalEntities WHERE Code LIKE N'DEMO-COMP-%'
UNION ALL SELECT 'DemoEquipment', COUNT_BIG(*) FROM dbo.Equipment WHERE EquipmentNumber LIKE N'DEMO-EQ-%'
UNION ALL SELECT 'DemoBusinessPartners', COUNT_BIG(*) FROM dbo.BusinessPartners WHERE PartnerNumber LIKE N'DEMO-BP-%'
UNION ALL SELECT 'DemoEmployees', COUNT_BIG(*) FROM dbo.Employees WHERE EmployeeNumber LIKE N'DEMO-E-%'
UNION ALL SELECT 'DemoProjects', COUNT_BIG(*) FROM dbo.Projects WHERE ProjectNumber LIKE N'DEMO-P-%'
UNION ALL SELECT 'DemoUsers', COUNT_BIG(*) FROM dbo.AspNetUsers WHERE UserName LIKE N'demo-%'
UNION ALL SELECT 'DemoCertificates', COUNT_BIG(*) FROM dbo.EmployeeCertificates WHERE CertificateNumber LIKE N'DEMO-%'
UNION ALL SELECT 'DemoCompanyCertificates', COUNT_BIG(*) FROM dbo.CompanyCertificates WHERE CertificateNumber LIKE N'DEMO-%'
UNION ALL SELECT 'DemoPayrollBatches', COUNT_BIG(*) FROM dbo.PayrollBatches WHERE BatchNumber LIKE N'DEMO-%'
UNION ALL SELECT 'DemoReminders', COUNT_BIG(*) FROM dbo.ReminderItems WHERE DeduplicationKey LIKE N'demo-%'
UNION ALL SELECT 'DemoAccounts', COUNT_BIG(*) FROM dbo.FinancialAccounts WHERE AccountName LIKE N'演示%'
UNION ALL SELECT 'DemoBusinessYears', COUNT_BIG(*) FROM dbo.BusinessYears WHERE Name LIKE N'演示年度总账%'
UNION ALL SELECT 'DemoAuditLogs', COUNT_BIG(*) FROM dbo.AuditLogs WHERE BeforeJson LIKE N'%DEMO-%' OR AfterJson LIKE N'%DEMO-%'
'@

$cleanupSql = @'
SET XACT_ABORT ON;
BEGIN TRANSACTION;

SELECT Id INTO #DemoLegalEntities FROM dbo.LegalEntities WHERE Code LIKE N'DEMO-COMP-%' AND Notes LIKE N'演示数据，仅用于 EngineeringManager_Test。';
SELECT Id INTO #DemoEquipment FROM dbo.Equipment WHERE EquipmentNumber LIKE N'DEMO-EQ-%' AND Notes = N'演示设备';
SELECT Id INTO #DemoBusinessPartners FROM dbo.BusinessPartners WHERE PartnerNumber LIKE N'DEMO-BP-%' AND Notes = N'演示合作单位';
SELECT Id INTO #DemoEmployees FROM dbo.Employees WHERE EmployeeNumber LIKE N'DEMO-E-%' AND Notes LIKE N'%演示%';
SELECT Id INTO #DemoProjects FROM dbo.Projects WHERE ProjectNumber LIKE N'DEMO-P-%' AND Notes LIKE N'%EngineeringManager_Test%';
SELECT Id INTO #DemoUsers FROM dbo.AspNetUsers WHERE UserName LIKE N'demo-%';
SELECT Id INTO #DemoContracts FROM dbo.Contracts WHERE ProjectId IN (SELECT Id FROM #DemoProjects);
SELECT Id INTO #DemoContractLineItems FROM dbo.ContractLineItems WHERE ContractId IN (SELECT Id FROM #DemoContracts);
SELECT Id INTO #DemoUsages FROM dbo.EquipmentProjectUsages WHERE EquipmentId IN (SELECT Id FROM #DemoEquipment) OR ProjectId IN (SELECT Id FROM #DemoProjects);
SELECT Id INTO #DemoSettlements FROM dbo.EquipmentSettlements WHERE UsageId IN (SELECT Id FROM #DemoUsages);
SELECT Id INTO #DemoStageResults FROM dbo.StageResults WHERE Title LIKE N'DEMO-RESULT-%';
SELECT Id INTO #DemoPayrollBatches FROM dbo.PayrollBatches WHERE BatchNumber LIKE N'DEMO-%';
SELECT Id INTO #DemoPayrollItems FROM dbo.PayrollItems WHERE PayrollBatchId IN (SELECT Id FROM #DemoPayrollBatches);
SELECT Id INTO #DemoPayrollPayments FROM dbo.PayrollPayments WHERE PayrollBatchId IN (SELECT Id FROM #DemoPayrollBatches);
SELECT Id INTO #DemoExpenses FROM dbo.ExpenseRecords WHERE EmployeeId IN (SELECT Id FROM #DemoEmployees) AND Description = N'演示员工报销';
SELECT Id INTO #DemoReceivables FROM dbo.ReceivableEntries WHERE ProjectId IN (SELECT Id FROM #DemoProjects) AND Description LIKE N'DEMO-RCV-%';
SELECT Id INTO #DemoCollections FROM dbo.CollectionEntries WHERE ProjectId IN (SELECT Id FROM #DemoProjects) AND Notes LIKE N'DEMO-COL-%';
SELECT Id INTO #DemoPayables FROM dbo.PayableEntries WHERE ProjectId IN (SELECT Id FROM #DemoProjects) AND Description LIKE N'DEMO-PAYABLE-%';
SELECT Id INTO #DemoPayments FROM dbo.PaymentEntries WHERE ProjectId IN (SELECT Id FROM #DemoProjects) AND Notes LIKE N'DEMO-PMT-%';
SELECT Id INTO #DemoInvoices FROM dbo.InvoiceEntries WHERE ProjectId IN (SELECT Id FROM #DemoProjects) AND InvoiceNumber LIKE N'DEMO-INV-%';
SELECT Id INTO #DemoAccounts FROM dbo.FinancialAccounts WHERE AccountName LIKE N'演示%' AND LegalEntityId IN (SELECT Id FROM #DemoLegalEntities);
SELECT Id INTO #DemoBusinessYears FROM dbo.BusinessYears WHERE Name LIKE N'演示年度总账%';
SELECT Id INTO #DemoWorkers FROM dbo.ConstructionWorkers WHERE Name IN (N'演示班组工人甲', N'演示班组工人乙') AND Notes LIKE N'%演示%';
SELECT Id INTO #DemoAdjustments FROM dbo.EmployeeFinancialAdjustments WHERE EmployeeId IN (SELECT Id FROM #DemoEmployees) AND (BusinessYearId IN (SELECT Id FROM #DemoBusinessYears) OR Notes LIKE N'演示年度总账%');
SELECT Id INTO #DemoWageEntries FROM dbo.EmployeeWageEntries WHERE EmployeeId IN (SELECT Id FROM #DemoEmployees) AND (BusinessYearId IN (SELECT Id FROM #DemoBusinessYears) OR Notes LIKE N'演示年度总账%');
SELECT Id INTO #DemoReceipts FROM dbo.EmployeeReceipts WHERE EmployeeId IN (SELECT Id FROM #DemoEmployees) AND (BusinessYearId IN (SELECT Id FROM #DemoBusinessYears) OR Notes LIKE N'演示年度总账%');
SELECT Id INTO #DemoAdvances FROM dbo.EmployeeAdvances WHERE EmployeeId IN (SELECT Id FROM #DemoEmployees) AND Description = N'演示员工借支';
SELECT Id INTO #DemoOtherPayments FROM dbo.EmployeeOtherPayments WHERE EmployeeId IN (SELECT Id FROM #DemoEmployees) AND Description LIKE N'演示%';
SELECT Id INTO #DemoCertificateAttachments FROM dbo.Attachments WHERE Id IN (SELECT AttachmentId FROM dbo.EmployeeCertificates WHERE CertificateNumber LIKE N'DEMO-%' AND AttachmentId IS NOT NULL) OR Id IN (SELECT AttachmentId FROM dbo.CompanyCertificates WHERE CertificateNumber LIKE N'DEMO-%' AND AttachmentId IS NOT NULL);

IF NOT EXISTS (SELECT 1 FROM #DemoLegalEntities) AND NOT EXISTS (SELECT 1 FROM #DemoEquipment) AND NOT EXISTS (SELECT 1 FROM #DemoBusinessPartners) AND NOT EXISTS (SELECT 1 FROM #DemoEmployees) AND NOT EXISTS (SELECT 1 FROM #DemoProjects)
    AND NOT EXISTS (SELECT 1 FROM dbo.AuditLogs WHERE BeforeJson LIKE N'%DEMO-%' OR AfterJson LIKE N'%DEMO-%')
    THROW 51000, N'未找到可识别的演示业务数据。', 1;

IF EXISTS (SELECT 1 FROM dbo.EquipmentSettlements WHERE Id IN (SELECT Id FROM #DemoSettlements) AND (FinanceSettlementId IS NOT NULL OR PayableEntryId IS NOT NULL))
    THROW 51001, N'演示设备存在已关联正式结算或应付记录，已停止清理。', 1;

IF EXISTS (SELECT 1 FROM dbo.PayrollCrewAllocations WHERE PayrollBatchId IN (SELECT Id FROM #DemoPayrollBatches) AND (FinanceSettlementId IS NOT NULL OR PayableEntryId IS NOT NULL))
    THROW 51002, N'演示工资批次存在已关联正式结算或应付记录，已停止清理。', 1;

IF EXISTS (SELECT 1 FROM dbo.FinanceCashAllocations WHERE ProjectId IN (SELECT Id FROM #DemoProjects) OR ContractId IN (SELECT Id FROM #DemoContracts) OR ContractLineItemId IN (SELECT Id FROM #DemoContractLineItems))
    OR EXISTS (SELECT 1 FROM dbo.FinanceInvoiceAllocations WHERE ProjectId IN (SELECT Id FROM #DemoProjects) OR ContractId IN (SELECT Id FROM #DemoContracts) OR ContractLineItemId IN (SELECT Id FROM #DemoContractLineItems))
    OR EXISTS (SELECT 1 FROM dbo.FinanceSettlements WHERE ProjectId IN (SELECT Id FROM #DemoProjects) OR ContractId IN (SELECT Id FROM #DemoContracts) OR ContractLineItemId IN (SELECT Id FROM #DemoContractLineItems))
    THROW 51003, N'演示项目存在中央账本关联，请先执行专项迁移清理。', 1;

DELETE FROM dbo.OfflineEquipmentAttachmentSyncs
WHERE OfflineEquipmentUsageSyncId IN (SELECT Id FROM dbo.OfflineEquipmentUsageSyncs WHERE EquipmentProjectUsageId IN (SELECT Id FROM #DemoUsages));
DELETE FROM dbo.OfflineEquipmentUsageSyncs WHERE EquipmentProjectUsageId IN (SELECT Id FROM #DemoUsages);
DELETE FROM dbo.EquipmentSettlementAdjustments WHERE SettlementId IN (SELECT Id FROM #DemoSettlements);
DELETE FROM dbo.EquipmentAdvancePayments WHERE UsageId IN (SELECT Id FROM #DemoUsages);
DELETE FROM dbo.EquipmentSettlements WHERE Id IN (SELECT Id FROM #DemoSettlements);
DELETE FROM dbo.EquipmentWorkPeriods WHERE UsageId IN (SELECT Id FROM #DemoUsages);
DELETE FROM dbo.EquipmentProjectUsages WHERE Id IN (SELECT Id FROM #DemoUsages);
DELETE FROM dbo.EquipmentLeaseAgreements WHERE EquipmentId IN (SELECT Id FROM #DemoEquipment);
DELETE FROM dbo.EquipmentMaintenanceRecords WHERE EquipmentId IN (SELECT Id FROM #DemoEquipment);
DELETE FROM dbo.EquipmentOwnershipHistories WHERE EquipmentId IN (SELECT Id FROM #DemoEquipment);
DELETE FROM dbo.Equipment WHERE Id IN (SELECT Id FROM #DemoEquipment);

DELETE FROM dbo.OfflineAttachmentSyncs
WHERE OfflineDraftSyncId IN (SELECT Id FROM dbo.OfflineDraftSyncs WHERE StageResultId IN (SELECT Id FROM #DemoStageResults) OR UserId IN (SELECT Id FROM #DemoUsers));
DELETE FROM dbo.OfflineDraftSyncs WHERE StageResultId IN (SELECT Id FROM #DemoStageResults) OR UserId IN (SELECT Id FROM #DemoUsers);
DELETE FROM dbo.Attachments WHERE StageResultId IN (SELECT Id FROM #DemoStageResults);
DELETE FROM dbo.StageResultLines WHERE StageResultId IN (SELECT Id FROM #DemoStageResults);
DELETE FROM dbo.StageResults WHERE Id IN (SELECT Id FROM #DemoStageResults);

DELETE FROM dbo.RefundOrReversalEntries WHERE CollectionEntryId IN (SELECT Id FROM #DemoCollections) OR ReceivableEntryId IN (SELECT Id FROM #DemoReceivables);
DELETE FROM dbo.InvoiceLineItemLinks WHERE InvoiceEntryId IN (SELECT Id FROM #DemoInvoices) OR ContractLineItemId IN (SELECT Id FROM #DemoContractLineItems);
DELETE FROM dbo.InvoiceReceivableLinks WHERE InvoiceEntryId IN (SELECT Id FROM #DemoInvoices) OR ReceivableEntryId IN (SELECT Id FROM #DemoReceivables);
DELETE FROM dbo.DeductionEntries WHERE PayableEntryId IN (SELECT Id FROM #DemoPayables);
DELETE FROM dbo.PaymentReversalEntries WHERE PaymentEntryId IN (SELECT Id FROM #DemoPayments);
DELETE FROM dbo.CollectionEntries WHERE Id IN (SELECT Id FROM #DemoCollections);
DELETE FROM dbo.PaymentEntries WHERE Id IN (SELECT Id FROM #DemoPayments);
DELETE FROM dbo.InvoiceEntries WHERE Id IN (SELECT Id FROM #DemoInvoices);
DELETE FROM dbo.PayableEntries WHERE Id IN (SELECT Id FROM #DemoPayables);
DELETE FROM dbo.ReceivableEntries WHERE Id IN (SELECT Id FROM #DemoReceivables);

DELETE FROM dbo.EmployeeWageEntries WHERE Id IN (SELECT Id FROM #DemoWageEntries);
DELETE FROM dbo.EmployeeReceipts WHERE Id IN (SELECT Id FROM #DemoReceipts);
DELETE FROM dbo.EmployeeFinancialAdjustments WHERE ReversalOfId IN (SELECT Id FROM #DemoAdjustments);
DELETE FROM dbo.EmployeeFinancialAdjustments WHERE Id IN (SELECT Id FROM #DemoAdjustments);
DELETE FROM dbo.ExpensePayments WHERE ExpenseRecordId IN (SELECT Id FROM #DemoExpenses);
DELETE FROM dbo.ExpenseRecords WHERE Id IN (SELECT Id FROM #DemoExpenses);
DELETE FROM dbo.EmployeeAdvances WHERE Id IN (SELECT Id FROM #DemoAdvances);
DELETE FROM dbo.EmployeeOtherPayments WHERE Id IN (SELECT Id FROM #DemoOtherPayments);

DELETE FROM dbo.Attachments WHERE PayrollPaymentId IN (SELECT Id FROM #DemoPayrollPayments);
DELETE FROM dbo.PayrollCostAllocations WHERE PayrollItemId IN (SELECT Id FROM #DemoPayrollItems);
DELETE FROM dbo.PayrollCrewAllocations WHERE PayrollBatchId IN (SELECT Id FROM #DemoPayrollBatches);
DELETE FROM dbo.PayrollPayments WHERE Id IN (SELECT Id FROM #DemoPayrollPayments);
DELETE FROM dbo.PayrollItems WHERE Id IN (SELECT Id FROM #DemoPayrollItems);
DELETE FROM dbo.PayrollBatches WHERE Id IN (SELECT Id FROM #DemoPayrollBatches);

DELETE FROM dbo.ConstructionCrewMemberships WHERE ConstructionWorkerId IN (SELECT Id FROM #DemoWorkers) OR CrewBusinessPartnerId IN (SELECT Id FROM #DemoBusinessPartners) AND Notes LIKE N'%演示%';
DELETE FROM dbo.ConstructionWorkers WHERE Id IN (SELECT Id FROM #DemoWorkers);
DELETE FROM dbo.EmployeeCertificates WHERE CertificateNumber LIKE N'DEMO-%' AND EmployeeId IN (SELECT Id FROM #DemoEmployees);
DELETE FROM dbo.CompanyCertificates WHERE CertificateNumber LIKE N'DEMO-%' AND LegalEntityId IN (SELECT Id FROM #DemoLegalEntities);
DELETE FROM dbo.Attachments WHERE Id IN (SELECT Id FROM #DemoCertificateAttachments);

DELETE FROM dbo.ReminderItems WHERE DeduplicationKey LIKE N'demo-%';
DELETE FROM dbo.AuditLogs
WHERE BeforeJson LIKE N'%DEMO-%' OR AfterJson LIKE N'%DEMO-%';
DELETE FROM dbo.AccountTransactions
WHERE SourceId IN (SELECT Id FROM #DemoCollections)
   OR SourceId IN (SELECT Id FROM #DemoPayments)
   OR SourceId IN (SELECT Id FROM #DemoPayrollBatches)
   OR Description LIKE N'DEMO-%'
   OR Description LIKE N'工资批次：DEMO-%';

DELETE FROM dbo.Attachments WHERE ContractId IN (SELECT Id FROM #DemoContracts) OR ContractLineItemId IN (SELECT Id FROM #DemoContractLineItems) OR ProjectId IN (SELECT Id FROM #DemoProjects);
DELETE FROM dbo.ContractLineItemLegalEntityAllocations WHERE ContractLineItemId IN (SELECT Id FROM #DemoContractLineItems);
DELETE FROM dbo.ContractLegalEntityAllocations WHERE ContractId IN (SELECT Id FROM #DemoContracts);
DELETE FROM dbo.ContractLineItems WHERE Id IN (SELECT Id FROM #DemoContractLineItems);
DELETE FROM dbo.ProjectPartners WHERE ProjectId IN (SELECT Id FROM #DemoProjects);
DELETE FROM dbo.ProjectLegalEntities WHERE ProjectId IN (SELECT Id FROM #DemoProjects);
DELETE FROM dbo.ProjectResponsibleEmployees WHERE ProjectId IN (SELECT Id FROM #DemoProjects) OR EmployeeId IN (SELECT Id FROM #DemoEmployees);
DELETE FROM dbo.ProjectAssignments WHERE ProjectId IN (SELECT Id FROM #DemoProjects) OR UserId IN (SELECT Id FROM #DemoUsers);
DELETE FROM dbo.ProjectMilestones WHERE ProjectId IN (SELECT Id FROM #DemoProjects);
DELETE FROM dbo.ProjectTaxConfigurations WHERE ProjectId IN (SELECT Id FROM #DemoProjects);
DELETE FROM dbo.Contracts WHERE Id IN (SELECT Id FROM #DemoContracts);
DELETE FROM dbo.Projects WHERE Id IN (SELECT Id FROM #DemoProjects);

DELETE FROM dbo.FinancialAccounts WHERE Id IN (SELECT Id FROM #DemoAccounts);
DELETE FROM dbo.BusinessYears WHERE Id IN (SELECT Id FROM #DemoBusinessYears);
DELETE FROM dbo.Employees WHERE Id IN (SELECT Id FROM #DemoEmployees);
DELETE FROM dbo.LegalEntities WHERE Id IN (SELECT Id FROM #DemoLegalEntities);
DELETE FROM dbo.BusinessPartners WHERE Id IN (SELECT Id FROM #DemoBusinessPartners);
DELETE FROM dbo.AspNetUsers WHERE Id IN (SELECT Id FROM #DemoUsers);

COMMIT TRANSACTION;
'@

try {
    $connection.Open()
    $before = @(Get-QueryRows $connection $countSql)
    $before | Format-Table -AutoSize

    $hasSampleData = [int64](@($before | Where-Object { $_.Count -gt 0 } | Measure-Object).Count) -gt 0
    $officialCompanyCount = [int64](Get-Scalar $connection "SELECT COUNT_BIG(*) FROM dbo.LegalEntities WHERE Notes LIKE N'正式资料，禁止按测试数据删除。%'")
    if ($officialCompanyCount -lt 1) {
        throw '未检测到正式自有公司保护标记，已停止清理。'
    }
    if (-not $hasSampleData) {
        Write-Output '未发现可识别的演示数据，未执行删除。'
        return
    }
    if (-not $execute) {
        Write-Output '当前为预览模式。确认以上演示数据范围后，请使用 -Execute 执行清理。'
        return
    }

    $attachmentSql = @'
SELECT StoredName FROM dbo.Attachments
WHERE ProjectId IN (SELECT Id FROM dbo.Projects WHERE ProjectNumber LIKE N'DEMO-P-%')
   OR StageResultId IN (SELECT Id FROM dbo.StageResults WHERE Title LIKE N'DEMO-RESULT-%')
   OR ContractId IN (SELECT Id FROM dbo.Contracts WHERE ProjectId IN (SELECT Id FROM dbo.Projects WHERE ProjectNumber LIKE N'DEMO-P-%'))
   OR ContractLineItemId IN (SELECT li.Id FROM dbo.ContractLineItems li JOIN dbo.Contracts c ON c.Id=li.ContractId WHERE c.ProjectId IN (SELECT Id FROM dbo.Projects WHERE ProjectNumber LIKE N'DEMO-P-%'))
   OR PayrollPaymentId IN (SELECT p.Id FROM dbo.PayrollPayments p JOIN dbo.PayrollBatches b ON b.Id=p.PayrollBatchId WHERE b.BatchNumber LIKE N'DEMO-%')
   OR Id IN (SELECT AttachmentId FROM dbo.EmployeeCertificates WHERE CertificateNumber LIKE N'DEMO-%' AND AttachmentId IS NOT NULL)
   OR Id IN (SELECT AttachmentId FROM dbo.CompanyCertificates WHERE CertificateNumber LIKE N'DEMO-%' AND AttachmentId IS NOT NULL);
'@
    $attachmentRows = @(Get-QueryRows $connection $attachmentSql)

    $backupDirectory = Join-Path $projectRoot 'src\EngineeringManager.Web\App_Data\backups'
    New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
    $suffix = "{0:yyyyMMddHHmmss}_{1}" -f (Get-Date), ([Guid]::NewGuid().ToString('N'))
    $backupPath = Join-Path $backupDirectory "EngineeringManager_SampleDataCleanup_$suffix.bak"
    $escapedBackupPath = $backupPath.Replace("'", "''")
    $backupCommand = $connection.CreateCommand()
    $backupCommand.CommandText = "BACKUP DATABASE [$actualDatabase] TO DISK = N'$escapedBackupPath' WITH COPY_ONLY, INIT"
    [void]$backupCommand.ExecuteNonQuery()
    $backupCommand.Dispose()

    $transaction = $connection.BeginTransaction()
    try {
        $cleanupCommand = $connection.CreateCommand()
        $cleanupCommand.Transaction = $transaction
        $cleanupCommand.CommandText = $cleanupSql
        $cleanupCommand.CommandTimeout = 180
        [void]$cleanupCommand.ExecuteNonQuery()
        $cleanupCommand.Dispose()
        $transaction.Commit()
    } catch {
        try { $transaction.Rollback() } catch { }
        throw
    }

    $attachmentDirectory = Join-Path $projectRoot 'src\EngineeringManager.Web\App_Data\attachments'
    $deletedAttachmentFiles = 0
    foreach ($row in $attachmentRows) {
        if ([string]::IsNullOrWhiteSpace([string]$row.StoredName)) {
            continue
        }
        $storedPath = Join-Path $attachmentDirectory ([string]$row.StoredName)
        if (Test-Path -LiteralPath $storedPath) {
            Remove-Item -LiteralPath $storedPath -Force
            $deletedAttachmentFiles++
        }
    }
    $credentialsPath = Join-Path $projectRoot 'src\EngineeringManager.Web\App_Data\local-test-credentials.txt'
    if (Test-Path -LiteralPath $credentialsPath) {
        Remove-Item -LiteralPath $credentialsPath -Force
    }

    $after = @(Get-QueryRows $connection $countSql)
    Write-Output "数据库备份：$backupPath"
    Write-Output "已删除演示附件文件：$deletedAttachmentFiles 个"
    $after | Format-Table -AutoSize
    Write-Output '演示数据清理完成。'
} finally {
    $connection.Dispose()
}
