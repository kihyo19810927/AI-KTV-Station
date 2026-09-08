param([ValidateRange(1, 100)][int]$Cycles = 20)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 (pwsh.exe) is required for reliable Unicode native arguments.' }
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$media = Join-Path $root 'tests\fixtures\Unicode 测试\演示歌曲.mkv'
$report = Join-Path $root 'TestResults\player-endurance-report.json'
& (Join-Path $PSScriptRoot 'generate-test-media.ps1') -Output $media | Out-Null
$mpv = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Filter mpv.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
if (-not $mpv) { $mpv = (Get-Command mpv.exe -ErrorAction Stop).Source }
$env:KTV_STATION_MPV = $mpv
$env:KTV_STATION_MEDIA_FIXTURE = $media
$env:KTV_STATION_ENDURANCE_CYCLES = $Cycles
$env:KTV_STATION_ENDURANCE_REPORT = $report
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
& $dotnet test (Join-Path $root 'tests\Station.Core.Tests\Station.Core.Tests.csproj') --configuration Release -p:UseSharedCompilation=false --no-restore --filter 'FullyQualifiedName~PlayerEnduranceTests' --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Get-Content -Raw $report
