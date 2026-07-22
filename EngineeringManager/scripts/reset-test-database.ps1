$ErrorActionPreference = 'Stop'

$databaseName = 'EngineeringManager_Test'
$allowOfficialDataDeletion = $false
for ($index = 0; $index -lt $args.Count; $index++) {
    if ($args[$index] -eq '-AllowOfficialDataDeletion') {
        $allowOfficialDataDeletion = $true
        continue
    }
    if ($args[$index] -eq '-DatabaseName' -and $index + 1 -lt $args.Count) {
        $databaseName = [string]$args[++$index]
        continue
    }
    throw "未知参数：$($args[$index])。仅支持 -DatabaseName <名称> 和 -AllowOfficialDataDeletion。"
}

if ($databaseName -notmatch '_Test$') {
    throw '只允许重建名称以 _Test 结尾的测试数据库。'
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

if ($actualDatabase -ne $databaseName) {
    throw "显式数据库名 $databaseName 与 Development 连接目标 $actualDatabase 不一致。"
}
if ($actualDatabase -notmatch '_Test$') {
    throw 'Development 连接目标不是明确标识的测试数据库。'
}
if ($actualDatabase -notmatch '^[A-Za-z0-9_]+$') {
    throw 'Development 数据库名包含不允许的字符。'
}

if (-not $allowOfficialDataDeletion) {
    $masterBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $masterBuilder.set_ConnectionString($connectionString)
    $masterBuilder['Initial Catalog'] = 'master'
    $masterConnection = [System.Data.SqlClient.SqlConnection]::new($masterBuilder.ConnectionString)
    try {
        $masterConnection.Open()
        $existsCommand = $masterConnection.CreateCommand()
        try {
            $existsCommand.CommandText = 'SELECT CASE WHEN DB_ID(@databaseName) IS NULL THEN 0 ELSE 1 END'
            [void]$existsCommand.Parameters.AddWithValue('@databaseName', $actualDatabase)
            $databaseExists = [int]$existsCommand.ExecuteScalar() -eq 1
        } finally {
            $existsCommand.Dispose()
        }

        if ($databaseExists) {
            $officialCommand = $masterConnection.CreateCommand()
            try {
                $officialCommand.CommandText = "SELECT COUNT(*) FROM [$actualDatabase].dbo.LegalEntities WHERE Code LIKE N'OFFICIAL-%' OR Notes LIKE N'正式资料，禁止按测试数据删除。%'"
                $officialCount = [int]$officialCommand.ExecuteScalar()
            } finally {
                $officialCommand.Dispose()
            }
            if ($officialCount -gt 0) {
                throw "检测到正式自有公司资料（$officialCount 家），已拒绝重建测试库。只有明确接受删除正式资料时才可传入 -AllowOfficialDataDeletion。"
            }
        }
    } finally {
        $masterConnection.Dispose()
    }
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Identity__SeedRoles = 'true'
$env:DevelopmentSampleData__Enabled = 'true'

$dotnetScript = Join-Path $PSScriptRoot 'dotnet.ps1'
$localDotnetDirectory = Join-Path $projectRoot '.dotnet'
$localDotnetPath = Join-Path $localDotnetDirectory 'dotnet.exe'
if (-not (Test-Path -LiteralPath $localDotnetPath)) {
    throw "项目本地 .NET SDK 不存在：$localDotnetPath"
}
$dotnetToolDirectory = Join-Path $projectRoot '.tools\dotnet-tools'
$dotnetEfPath = Join-Path $dotnetToolDirectory 'dotnet-ef.exe'
if (-not (Test-Path -LiteralPath $dotnetEfPath)) {
    throw "本地 EF 工具不存在：$dotnetEfPath"
}
$env:DOTNET_ROOT = $localDotnetDirectory
$env:PATH = "$localDotnetDirectory$([IO.Path]::PathSeparator)$dotnetToolDirectory$([IO.Path]::PathSeparator)$env:PATH"
$infrastructureProject = Join-Path $projectRoot 'src\EngineeringManager.Infrastructure\EngineeringManager.Infrastructure.csproj'
$webProject = Join-Path $projectRoot 'src\EngineeringManager.Web\EngineeringManager.Web.csproj'

& $dotnetScript ef database drop --force --project $infrastructureProject --startup-project $webProject
& $dotnetScript ef database update --project $infrastructureProject --startup-project $webProject

$credentialPath = Join-Path $projectRoot 'src\EngineeringManager.Web\App_Data\local-test-credentials.txt'
if (Test-Path -LiteralPath $credentialPath) {
    Remove-Item -LiteralPath $credentialPath -Force
}

$artifactDirectory = Join-Path $projectRoot 'artifacts\test-database-reset'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$stdoutPath = Join-Path $artifactDirectory 'seed.stdout.log'
$stderrPath = Join-Path $artifactDirectory 'seed.stderr.log'
$pwshPath = Join-Path $projectRoot '.tools\pwsh\pwsh.exe'
$seedArguments = @(
    '-NoLogo',
    '-NoProfile',
    '-File', $dotnetScript,
    'run',
    '--project', $webProject,
    '--no-launch-profile',
    '--urls', 'http://127.0.0.1:5099'
)
$seedProcess = Start-Process -FilePath $pwshPath -ArgumentList $seedArguments -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath

try {
    $seedCompleted = $false
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        if (Test-Path -LiteralPath $credentialPath) {
            $seedCompleted = $true
            break
        }
        if ($seedProcess.HasExited) {
            break
        }
        Start-Sleep -Milliseconds 500
        $seedProcess.Refresh()
    }
    if (-not $seedCompleted) {
        $stderr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw -Encoding UTF8 } else { '' }
        throw "完整测试数据生成失败。$stderr"
    }
} finally {
    if (-not $seedProcess.HasExited) {
        Stop-Process -Id $seedProcess.Id -Force
        $seedProcess.WaitForExit()
    }
}

[PSCustomObject]@{
    Environment = $env:ASPNETCORE_ENVIRONMENT
    Database = $actualDatabase
    Credentials = $credentialPath
    Status = 'RecreatedAndSeeded'
} | ConvertTo-Json
