param([string]$Output = (Join-Path $PSScriptRoot '..\tests\fixtures\Unicode 测试\演示歌曲.mkv'))
$PSNativeCommandArgumentPassing = 'Standard'
$ffmpeg = (Get-Command ffmpeg.exe -ErrorAction Stop).Source
$dir = Split-Path -Parent $Output; New-Item -ItemType Directory -Force $dir | Out-Null
$srt = Join-Path $dir '测试字幕.srt'; Set-Content -LiteralPath $srt -Encoding utf8 -Value "1`n00:00:01,000 --> 00:00:10,000`nAI-KTV Station 测试字幕`n"
& $ffmpeg -y -t 10 -f lavfi -i color=c=blue:s=320x240:r=25:d=10 -f lavfi -i sine=frequency=440:duration=10 -f lavfi -i sine=frequency=880:duration=10 -i $srt -map 0:v -map 1:a -map 2:a -map 3:0 -map_metadata -1 '-metadata:s:a:0=title=伴奏' '-metadata:s:a:1=title=原唱' '-metadata:s:s:0=title=测试字幕' -c:v libx264 -pix_fmt yuv420p -c:a aac -c:s srt -t 10 '-metadata:s:s:0=language=zho' $Output
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Output (Resolve-Path $Output)
