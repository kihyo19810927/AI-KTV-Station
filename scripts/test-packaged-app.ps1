param(
    [Parameter(Mandatory)][string]$Package,
    [ValidateSet('Station.Desktop.exe', 'Station.Tray.exe')][string]$ExecutableName = 'Station.Tray.exe',
    [switch]$RequireSeedDatabase
)

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
    $executable = Get-ChildItem -LiteralPath $extractRoot -Filter $ExecutableName -File -Recurse | Select-Object -First 1
    if (-not $executable) { throw "$ExecutableName is missing from extracted package." }
    $seedDatabase = Join-Path $executable.DirectoryName 'data\station.db'
    if ($RequireSeedDatabase -and -not (Test-Path -LiteralPath $seedDatabase -PathType Leaf)) {
        throw 'Packaged seed database is missing before the application starts.'
    }

    $settings = @{
        Server = @{ BindAddress = '127.0.0.1'; Port = $port }
        Storage = @{ DataDirectory = 'data' }
        Player = @{ ExecutablePath = ''; CommandTimeoutSeconds = 10 }
    } | ConvertTo-Json -Depth 4
    $settings | Set-Content -LiteralPath (Join-Path $settingsRoot 'settings.json') -Encoding utf8
    $env:AI_KTV_STATION_SETTINGS_ROOT = $settingsRoot
    $arguments = if ($ExecutableName -eq 'Station.Tray.exe') { '--smoke-exit-after=8' } else { '' }
    $process = Start-Process -FilePath $executable.FullName -ArgumentList $arguments -WorkingDirectory $executable.DirectoryName -WindowStyle Hidden -PassThru

    $startupTimeoutSeconds = if ($RequireSeedDatabase) { 90 } else { 20 }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($startupTimeoutSeconds)
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
    if (-not (Test-Path -LiteralPath $seedDatabase)) { throw 'Packaged desktop did not initialize its isolated database.' }
    # A tray/server cold start may restore its database, but it must not revive
    # a historical queue or launch mpv before the host explicitly presses Play.
    $packagePlayer = Get-Process mpv -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and $_.Path.StartsWith($executable.DirectoryName, [StringComparison]::OrdinalIgnoreCase)
    }
    if ($packagePlayer) { throw 'Tray cold start unexpectedly launched the packaged mpv player.' }
    Write-Output 'PACKAGE_COLD_START_IDLE=passed'
    if ($ExecutableName -eq 'Station.Desktop.exe') {
        # /health may respond before XAML is instantiated; validate that the desktop really opened.
        $windowDeadline = [DateTimeOffset]::UtcNow.AddSeconds(10)
        do {
            Start-Sleep -Milliseconds 200
            $process.Refresh()
        } while (-not $process.HasExited -and $process.MainWindowHandle -eq 0 -and [DateTimeOffset]::UtcNow -lt $windowDeadline)
        if ($process.HasExited -or $process.MainWindowHandle -eq 0) { throw 'Desktop window did not open after service startup.' }
        if (Test-Path -LiteralPath (Join-Path $settingsRoot 'startup-error.txt')) { throw 'Desktop reported a startup error.' }
        if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(10000)) { throw 'Desktop did not release its process after a normal window close.' }
        Write-Output 'PACKAGE_WINDOW=passed'
    }
    else {
        if (-not $process.WaitForExit(15000)) { throw 'Tray launcher did not exit through its controlled smoke path.' }
        Write-Output 'PACKAGE_TRAY_LIFECYCLE=passed'
    }
    if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) { throw 'Desktop process remained after normal shutdown.' }
    Write-Output 'PACKAGE_PROCESS_CLEANUP=passed'
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
