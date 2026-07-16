$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$watch = [System.Diagnostics.Stopwatch]::StartNew()
& (Join-Path $PSScriptRoot 'dotnet.ps1') test (Join-Path $root 'tests\EngineeringManager.Tests\EngineeringManager.Tests.csproj') --configuration Release --filter 'FullyQualifiedName~RepresentativeDataPerformanceTests' --no-restore
$watch.Stop()
if ($LASTEXITCODE -ne 0) { throw "Performance baseline failed with exit code $LASTEXITCODE." }
Write-Output "PERFORMANCE_BASELINE=PASS"
Write-Output "TOTAL_SECONDS=$([math]::Round($watch.Elapsed.TotalSeconds, 3))"
Write-Output 'DATASET=100 projects; 500 employees; 200 equipment; 10000 equipment usages'
Write-Output 'SCOPE=Local functional baseline, not production concurrency capacity'
