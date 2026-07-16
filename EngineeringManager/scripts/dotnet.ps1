$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $root '.dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) {
    $localDotnet
} else {
    (Get-Command dotnet -ErrorAction SilentlyContinue).Source
}

if ([string]::IsNullOrWhiteSpace($dotnet)) {
    throw 'dotnet executable was not found. Install .NET 10 SDK or restore the local .dotnet folder.'
}

& $dotnet @args
if ($LASTEXITCODE -ne 0) {
    $exitCode = $LASTEXITCODE
    throw "dotnet command failed with exit code $exitCode."
}
