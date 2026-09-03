$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source }
Write-Output "dotnet=$(& $dotnet --version)"
Write-Output "node=$(& node.exe --version)"
Write-Output "npm=$(& npm.cmd --version)"
& $dotnet restore (Join-Path $root 'AI-KTV-Station.slnx') --locked-mode
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$webLock = Join-Path $root 'src\Station.Web\package-lock.json'
if (Test-Path -LiteralPath $webLock) { & npm.cmd ci --prefix (Split-Path $webLock -Parent); if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }
Write-Output 'BOOTSTRAP=passed'
