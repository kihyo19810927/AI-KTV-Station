param([Parameter(Mandatory)][string]$Package)

$ErrorActionPreference = 'Stop'
$packagePath = (Resolve-Path -LiteralPath $Package).Path
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) "ai-ktv-package-smoke-$([Guid]::NewGuid().ToString('N'))"
$extractRoot = Join-Path $temporaryRoot 'install'
$settingsRoot = Join-Path $temporaryRoot 'settings'
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$process = $null
$previousSettingsRoot = $env:AI_KTV_STATION_SETTINGS_ROOT

New-Item -ItemType Directory -Path $extractRoot, $settingsRoot -Force | Out-Null
try {
    Expand-Archive -LiteralPath $packagePath -DestinationPath $extractRoot
    $executable = Get-ChildItem -LiteralPath $extractRoot -Filter Station.Desktop.exe -File -Recurse | Select-Object -First 1
    if (-not $executable) { throw 'Station.Desktop.exe is missing from extracted package.' }

    $settings = @{
        Server = @{ BindAddress = '127.0.0.1'; Port = $port }
        Storage = @{ DataDirectory = 'data' }
        Player = @{ ExecutablePath = ''; CommandTimeoutSeconds = 10 }
    } | ConvertTo-Json -Depth 4
    $settings | Set-Content -LiteralPath (Join-Path $settingsRoot 'settings.json') -Encoding utf8
    $env:AI_KTV_STATION_SETTINGS_ROOT = $settingsRoot
    $process = Start-Process -FilePath $executable.FullName -WorkingDirectory $executable.DirectoryName -WindowStyle Hidden -PassThru

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(20)
    $healthy = $false
    while ([DateTimeOffset]::UtcNow -lt $deadline -and -not $process.HasExited) {
        try {
            $response = Invoke-RestMethod -Uri "http://127.0.0.1:$port/health" -TimeoutSec 1
            if ($response.status -eq 'ok') { $healthy = $true; break }
        }
        catch { Start-Sleep -Milliseconds 200 }
    }
    if (-not $healthy) {
        $exitDetail = if ($process.HasExited) { " Process exit code: $($process.ExitCode)." } else { '' }
        $logPath = Join-Path $settingsRoot 'logs\station.jsonl'
        $logDetail = if (Test-Path -LiteralPath $logPath) {
            " Diagnostic log: $((Get-Content -LiteralPath $logPath -Raw).Trim())"
        } else { ' No diagnostic log was created.' }
        throw "Packaged desktop did not expose a healthy embedded service.$exitDetail$logDetail"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $executable.DirectoryName 'data\station.db'))) { throw 'Packaged desktop did not initialize its isolated database.' }
    Write-Output "PACKAGE_SMOKE_PORT=$port"
    Write-Output 'PACKAGE_SMOKE=passed'
}
finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit(5000) | Out-Null
    }
    if ($null -eq $previousSettingsRoot) { Remove-Item Env:AI_KTV_STATION_SETTINGS_ROOT -ErrorAction SilentlyContinue }
    else { $env:AI_KTV_STATION_SETTINGS_ROOT = $previousSettingsRoot }
    if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
}
