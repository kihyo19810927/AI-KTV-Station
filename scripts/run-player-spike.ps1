$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..'); $media = Join-Path $root 'tests\fixtures\Unicode 测试\演示歌曲.mkv'
& (Join-Path $PSScriptRoot 'generate-test-media.ps1') -Output $media
$mpv = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Filter mpv.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
if (-not $mpv) { $mpv = (Get-Command mpv.exe -ErrorAction Stop).Source }
& dotnet run --project (Join-Path $root 'src\Station.PlayerSpike\Station.PlayerSpike.csproj') -- $mpv $media
if ($LASTEXITCODE -ne 0) { throw "player spike failed: $LASTEXITCODE" }
