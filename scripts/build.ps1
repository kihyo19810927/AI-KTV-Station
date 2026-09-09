param([switch]$NoRestore)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source }
if (-not $NoRestore) { & (Join-Path $PSScriptRoot 'bootstrap.ps1'); if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }
& $dotnet format (Join-Path $root 'AI-KTV-Station.slnx') --verify-no-changes --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet build (Join-Path $root 'AI-KTV-Station.slnx') --configuration Release -m:1 -p:UseSharedCompilation=false --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& (Join-Path $PSScriptRoot 'verify-project-dependencies.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$web = Join-Path $root 'src\Station.Web'
if (Test-Path -LiteralPath (Join-Path $web 'package-lock.json')) {
    & npm.cmd run typecheck --prefix $web
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & npm.cmd run build --prefix $web
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
Write-Output 'BUILD=passed'
