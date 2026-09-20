param(
    [Parameter(Mandatory)][string]$Package,
    [ValidateSet('Station.Desktop.exe', 'Station.Tray.exe')][string]$ExecutableName = 'Station.Tray.exe',
    [switch]$RequireSeedDatabase
)

$ErrorActionPreference = 'Stop'
$packagePath = (Resolve-Path -LiteralPath $Package).Path
if ([IO.Path]::GetExtension($packagePath) -ne '.zip') { throw 'Package must be a ZIP file.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $entries = @($archive.Entries | ForEach-Object FullName)
    foreach ($required in @(
        $ExecutableName,
        'wwwroot/index.html',
        'INSTALL.md',
        'THIRD-PARTY-NOTICES.md',
        'manifest.json',
        'tools/mpv/mpv.exe',
        'tools/mpv/vulkan-1.dll',
        'tools/ffmpeg/ffmpeg.exe',
        'tools/ffmpeg/ffprobe.exe',
        'tools/licenses/mpv-LICENSE.txt',
        'tools/licenses/ffmpeg-LICENSE.txt'
    )) {
        if (-not ($entries | Where-Object { $_.Replace('\', '/').EndsWith("/$required", [StringComparison]::OrdinalIgnoreCase) })) {
            throw "Required package entry is missing: $required"
        }
    }
    foreach ($forbidden in @('settings.json', 'station.db-wal', 'station.db-shm')) {
        if ($entries | Where-Object { [IO.Path]::GetFileName($_) -ieq $forbidden }) {
            throw "Forbidden package entry exists: $forbidden"
        }
    }
    $seedEntries = @($entries | Where-Object { $_.Replace('\', '/').EndsWith('/data/station.db', [StringComparison]::OrdinalIgnoreCase) })
    if ($RequireSeedDatabase -and $seedEntries.Count -ne 1) { throw 'Required seed database entry is missing: data/station.db' }
    if ($seedEntries.Count -gt 1) { throw 'Package contains duplicate seed database entries.' }
}
finally { $archive.Dispose() }

$checksumPath = "$packagePath.sha256"
if (-not (Test-Path -LiteralPath $checksumPath)) { throw 'Package checksum file is missing.' }
$expected = ((Get-Content -LiteralPath $checksumPath -Raw) -split '\s+')[0]
$actual = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expected -ne $actual) { throw 'Package checksum does not match.' }
Write-Output "RELEASE_PACKAGE_ENTRIES=$($entries.Count)"
Write-Output 'RELEASE_PACKAGE=passed'
