$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 (pwsh.exe) is required for reliable Unicode native arguments.' }
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$media = Join-Path $root 'tests\fixtures\Unicode 测试\演示歌曲.mkv'
& (Join-Path $PSScriptRoot 'generate-test-media.ps1') -Output $media | Out-Null
$mpv = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Filter mpv.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
if (-not $mpv) { $mpv = (Get-Command mpv.exe -ErrorAction Stop).Source }
$env:KTV_STATION_MPV = $mpv
$env:KTV_STATION_MEDIA_FIXTURE = $media
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$env:MSBUILDDISABLENODEREUSE = '1'
& $dotnet test (Join-Path $root 'tests\Station.Core.Tests\Station.Core.Tests.csproj') --configuration Release -m:1 -p:UseSharedCompilation=false --no-restore --filter 'FullyQualifiedName~MpvPlayerAdapterTests&Category=External' --logger 'console;verbosity=normal'
exit $LASTEXITCODE
