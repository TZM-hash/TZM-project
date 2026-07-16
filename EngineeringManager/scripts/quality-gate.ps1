$ErrorActionPreference = 'Stop'

& $PSScriptRoot\dotnet.ps1 restore (Join-Path (Split-Path -Parent $PSScriptRoot) 'EngineeringManager.sln')
& $PSScriptRoot\dotnet.ps1 build (Join-Path (Split-Path -Parent $PSScriptRoot) 'EngineeringManager.sln') --configuration Release --no-restore
& $PSScriptRoot\dotnet.ps1 test (Join-Path (Split-Path -Parent $PSScriptRoot) 'EngineeringManager.sln') --configuration Release --no-build
& $PSScriptRoot\publish-release.ps1

Write-Output 'QUALITY_GATE=PASS'
