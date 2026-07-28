$ErrorActionPreference = 'Stop'

$port = 5075
$startupTimeoutSeconds = 60
$configuration = 'Release'
for ($argumentIndex = 0; $argumentIndex -lt $args.Count; $argumentIndex++) {
    switch ($args[$argumentIndex]) {
        '-Port' {
            if ($argumentIndex + 1 -ge $args.Count -or -not [int]::TryParse([string]$args[++$argumentIndex], [ref]$port) -or $port -lt 1 -or $port -gt 65535) {
                throw '-Port 必须是 1 到 65535 之间的整数。'
            }
        }
        '-StartupTimeoutSeconds' {
            if ($argumentIndex + 1 -ge $args.Count -or -not [int]::TryParse([string]$args[++$argumentIndex], [ref]$startupTimeoutSeconds) -or $startupTimeoutSeconds -lt 5 -or $startupTimeoutSeconds -gt 180) {
                throw '-StartupTimeoutSeconds 必须是 5 到 180 之间的整数。'
            }
        }
        '-Configuration' {
            if ($argumentIndex + 1 -ge $args.Count) {
                throw '-Configuration 需要 Debug 或 Release。'
            }
            $configuration = [string]$args[++$argumentIndex]
            if ($configuration -notin @('Debug', 'Release')) {
                throw '-Configuration 仅支持 Debug 或 Release。'
            }
        }
        default {
            throw "未知参数：$($args[$argumentIndex])。"
        }
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $projectRoot 'src\EngineeringManager.Web\EngineeringManager.Web.csproj'
$dotnetScript = Join-Path $PSScriptRoot 'dotnet.ps1'
$pwshPath = Join-Path $projectRoot '.tools\pwsh\pwsh.exe'
$artifactsPath = Join-Path $projectRoot 'artifacts\local-web'
$stdoutPath = Join-Path $artifactsPath 'service.stdout.log'
$stderrPath = Join-Path $artifactsPath 'service.stderr.log'
$serviceUrl = "http://127.0.0.1:$Port"
$readyUrl = "$serviceUrl/health/ready"

if (-not (Test-Path -LiteralPath $pwshPath)) {
    throw "找不到项目内 PowerShell：$pwshPath"
}
if (-not (Test-Path -LiteralPath $dotnetScript)) {
    throw "找不到项目内 .NET 启动包装脚本：$dotnetScript"
}

New-Item -ItemType Directory -Path $artifactsPath -Force | Out-Null

function Test-ProjectWebProcess {
    param([Parameter(Mandatory)][object]$ProcessInfo)

    $executablePath = [string]$ProcessInfo.ExecutablePath
    $commandLine = [string]$ProcessInfo.CommandLine
    $isWebHost = $ProcessInfo.Name -eq 'EngineeringManager.Web.exe'
    $isDotnetWebHost = $ProcessInfo.Name -eq 'dotnet.exe' -and $commandLine -like '*EngineeringManager.Web*'
    $belongsToProject = $executablePath.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $commandLine.Contains($projectRoot, [StringComparison]::OrdinalIgnoreCase)

    return ($isWebHost -or $isDotnetWebHost) -and $belongsToProject
}

function Get-ProcessInfoById {
    param([Parameter(Mandatory)][int]$ProcessId)

    Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction SilentlyContinue
}

Write-Host "正在检查本项目旧服务和端口 $Port ..."

$listenerProcessIds = @(
    Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
)

foreach ($listenerProcessId in $listenerProcessIds) {
    $processInfo = Get-ProcessInfoById -ProcessId $listenerProcessId
    if ($null -eq $processInfo) {
        continue
    }
    if (-not (Test-ProjectWebProcess -ProcessInfo $processInfo)) {
        throw "端口 $Port 已被其他程序占用（PID $listenerProcessId，$($processInfo.Name)）。为避免误杀，启动已停止。"
    }
}

$projectWebProcesses = @(
    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object { Test-ProjectWebProcess -ProcessInfo $_ }
)

foreach ($processInfo in $projectWebProcesses) {
    Write-Host "正在停止旧服务 PID $($processInfo.ProcessId) ..."
    Stop-Process -Id $processInfo.ProcessId -Force -ErrorAction SilentlyContinue
    Wait-Process -Id $processInfo.ProcessId -Timeout 10 -ErrorAction SilentlyContinue
}

$listenerReleaseDeadline = [DateTime]::UtcNow.AddSeconds(10)
$remainingListener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
while ([DateTime]::UtcNow -lt $listenerReleaseDeadline) {
    if (-not $remainingListener) {
        break
    }

    Start-Sleep -Milliseconds 250
    $remainingListener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
}

if ($remainingListener) {
    $remainingProcessIds = ($remainingListener | Select-Object -ExpandProperty OwningProcess -Unique) -join ', '
    throw "端口 $Port 仍被 PID $remainingProcessIds 占用，无法安全启动。"
}

Write-Host "正在构建 $Configuration 版本 ..."
& $pwshPath -NoLogo -NoProfile -File $dotnetScript build $webProject --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Web 项目构建失败，退出代码：$LASTEXITCODE"
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$runArguments = @(
    '-NoLogo',
    '-NoProfile',
    '-File', $dotnetScript,
    'run',
    '--project', $webProject,
    '--configuration', $Configuration,
    '--no-build',
    '--no-launch-profile',
    '--urls', $serviceUrl
)

Write-Host "正在启动服务：$serviceUrl"
$serviceProcess = Start-Process -FilePath $pwshPath `
    -ArgumentList $runArguments `
    -WorkingDirectory $projectRoot `
    -WindowStyle Hidden `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -PassThru

$deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
$lastError = $null
while ([DateTime]::UtcNow -lt $deadline) {
    $serviceProcess.Refresh()
    if ($serviceProcess.HasExited) {
        $stdout = if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw -Encoding UTF8 } else { '' }
        $stderr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw -Encoding UTF8 } else { '' }
        throw "服务启动后立即退出（退出代码 $($serviceProcess.ExitCode)）。`n标准输出：`n$stdout`n错误输出：`n$stderr"
    }

    try {
        $response = Invoke-WebRequest -Uri $readyUrl -UseBasicParsing -TimeoutSec 3
        if ($response.StatusCode -eq 200) {
            Write-Host "服务启动成功。"
            [PSCustomObject]@{
                Url = $serviceUrl
                Ready = $response.StatusCode
                LauncherProcessId = $serviceProcess.Id
                StandardOutput = $stdoutPath
                StandardError = $stderrPath
            }
            exit 0
        }
    } catch {
        $lastError = $_.Exception.Message
    }

    Start-Sleep -Milliseconds 500
}

Stop-Process -Id $serviceProcess.Id -Force -ErrorAction SilentlyContinue
throw "服务在 $StartupTimeoutSeconds 秒内未就绪。最后一次健康检查错误：$lastError。日志：$stdoutPath；$stderrPath"
