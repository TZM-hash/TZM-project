$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$output = [System.IO.Path]::GetFullPath((Join-Path $root 'artifacts\publish'))
$resolvedRoot = [System.IO.Path]::GetFullPath($root)
if (-not $output.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Publish path escaped the workspace.' }
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
& (Join-Path $PSScriptRoot 'dotnet.ps1') publish (Join-Path $root 'src\EngineeringManager.Web\EngineeringManager.Web.csproj') --configuration Release --no-restore --output $output
if ($LASTEXITCODE -ne 0) { throw "Release publish failed with exit code $LASTEXITCODE." }
$required = @('EngineeringManager.Web.dll', 'web.config', 'wwwroot\manifest.webmanifest', 'wwwroot\service-worker.js')
foreach ($item in $required) { if (-not (Test-Path -LiteralPath (Join-Path $output $item))) { throw "Publish output missing: $item" } }
Write-Output 'PUBLISH_RELEASE=PASS'
Write-Output "PUBLISH_PATH=$output"
