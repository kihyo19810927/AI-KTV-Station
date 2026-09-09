$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source }

& $dotnet package list --project (Join-Path $root 'AI-KTV-Station.slnx') --include-transitive --format json --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Push-Location (Join-Path $root 'src\Station.Web')
try {
    & npm.cmd ls --all --omit=dev --json
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally { Pop-Location }
$ffmpeg = Get-Command ffmpeg.exe -ErrorAction SilentlyContinue
if ($ffmpeg) { & $ffmpeg.Source -version }
$mpv = Get-Command mpv.exe -ErrorAction SilentlyContinue
if ($mpv) { & $mpv.Source --version }
Write-Output 'DEPENDENCY_AUDIT=passed'
