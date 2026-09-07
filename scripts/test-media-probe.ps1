$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$media = Join-Path $root 'tests\fixtures\Unicode 测试\演示歌曲.mkv'
& (Join-Path $PSScriptRoot 'generate-test-media.ps1') -Output $media | Out-Null
$env:KTV_STATION_FFPROBE = (Get-Command ffprobe.exe -ErrorAction Stop).Source
$env:KTV_STATION_MEDIA_FIXTURE = $media
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
& $dotnet test (Join-Path $root 'tests\Station.Core.Tests\Station.Core.Tests.csproj') --configuration Release -p:UseSharedCompilation=false --no-restore --filter 'Category=External' --logger 'console;verbosity=normal'
exit $LASTEXITCODE
