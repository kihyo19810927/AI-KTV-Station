param(
    [Parameter(Mandatory)][string]$SampleDirectory,
    [Parameter(Mandatory)][string]$WorkDirectory
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (Test-Path -LiteralPath $WorkDirectory) { throw 'Use a new work directory to preserve previous samples and results.' }
$env:KTV_SCAN_SAMPLE_ROOT = (Resolve-Path -LiteralPath $SampleDirectory).Path
$env:KTV_SCAN_BENCHMARK_WORK = [IO.Path]::GetFullPath($WorkDirectory)
$env:KTV_STATION_FFPROBE = Join-Path $root 'common\ffmpeg\ffprobe.exe'
$env:MSBUILDDISABLENODEREUSE = '1'
& 'C:\Program Files\dotnet\dotnet.exe' test (Join-Path $root 'tests\Station.Core.Tests') -c Release --no-restore -m:1 -p:UseSharedCompilation=false --filter 'Category=ManualBenchmark' --logger 'console;verbosity=normal'
exit $LASTEXITCODE
