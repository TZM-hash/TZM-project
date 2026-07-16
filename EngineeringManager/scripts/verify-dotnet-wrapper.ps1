$ErrorActionPreference = 'Stop'

$wrapper = Join-Path $PSScriptRoot 'dotnet.ps1'
$threw = $false

try {
    & $wrapper definitely-not-a-dotnet-command
}
catch {
    $threw = $true
}

if (-not $threw) {
    throw 'Expected scripts/dotnet.ps1 to throw when dotnet returns a non-zero exit code.'
}

Write-Output 'DOTNET_WRAPPER_FAILURE_PROPAGATION=PASS'
