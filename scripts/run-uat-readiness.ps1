param([switch]$SkipExternalPlayer)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

& (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& (Join-Path $PSScriptRoot 'test.ps1') -NoRestore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipExternalPlayer) {
    & (Join-Path $PSScriptRoot 'verify-environment.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Output 'UAT_READINESS=passed'
