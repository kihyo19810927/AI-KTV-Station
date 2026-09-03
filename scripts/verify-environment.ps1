$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Run with PowerShell 7 (pwsh.exe).' }
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { throw ".NET SDK not found at $dotnet" }
$mpv = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Filter mpv.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
if (-not $mpv) { $mpv = (Get-Command mpv.exe -ErrorAction Stop).Source }
$ffmpeg = (Get-Command ffmpeg.exe -ErrorAction Stop).Source
$ffprobe = (Get-Command ffprobe.exe -ErrorAction Stop).Source
$node = (Get-Command node.exe -ErrorAction Stop).Source
$npm = (Get-Command npm.cmd -ErrorAction Stop).Source
$media = Join-Path $root 'tests\fixtures\Unicode 测试\演示歌曲.mkv'
& (Join-Path $PSScriptRoot 'generate-test-media.ps1') -Output $media | Out-Null
$probe = & $ffprobe -v error -show_entries 'format=duration:stream=index,codec_type:stream_tags=title,language' -of json $media | ConvertFrom-Json
$audio = @($probe.streams | Where-Object codec_type -eq 'audio')
$subs = @($probe.streams | Where-Object codec_type -eq 'subtitle')
if ($audio.Count -ne 2 -or $subs.Count -ne 1) { throw 'Generated media track count is invalid.' }
if (@($audio.tags.title) -notcontains '伴奏' -or @($audio.tags.title) -notcontains '原唱') { throw 'Generated media audio titles are invalid.' }
$duration = [double]$probe.format.duration
if ($duration -lt 9.5 -or $duration -gt 10.5) { throw "Generated media duration is $duration seconds." }
$env:Path = 'C:\Program Files\dotnet;' + $env:Path
& (Join-Path $PSScriptRoot 'run-player-spike.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Player spike failed.' }
Write-Output "DOTNET=$(& $dotnet --version)"
Write-Output "NODE=$(& $node --version)"
Write-Output "NPM=$(& $npm --version)"
Write-Output "FFMPEG=$((& $ffmpeg -version)[0])"
Write-Output "FFPROBE=$((& $ffprobe -version)[0])"
$mpvVersion = (Get-Item -LiteralPath $mpv).VersionInfo.ProductVersion
if (-not $mpvVersion) { $mpvVersion = 'installed (version metadata unavailable)' }
Write-Output "MPV=$mpvVersion"
Write-Output "FIXTURE_DURATION=$duration"
Write-Output 'ENVIRONMENT_BASELINE=passed'
