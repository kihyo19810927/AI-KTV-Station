$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$url = 'http://127.0.0.1:15090'
$project = Join-Path $root 'src\Station.Server\Station.Server.csproj'
$process = Start-Process -FilePath $dotnet -ArgumentList @('run', '--project', $project, '--no-build', '--configuration', 'Release', '--urls', $url) -PassThru -WindowStyle Hidden
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        try { $response = Invoke-RestMethod "$url/health" -TimeoutSec 2; break } catch { Start-Sleep -Milliseconds 250 }
    } while ([DateTime]::UtcNow -lt $deadline -and -not $process.HasExited)
    if ($process.HasExited) { throw "Station.Server exited with code $($process.ExitCode)." }
    if ($response.status -ne 'ok') { throw 'Health endpoint did not return ok.' }
    Write-Output 'SERVER_SMOKE=passed'
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit() }
    $process.Dispose()
}
