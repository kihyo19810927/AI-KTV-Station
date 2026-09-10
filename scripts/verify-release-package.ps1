param([Parameter(Mandatory)][string]$Package)

$ErrorActionPreference = 'Stop'
$packagePath = (Resolve-Path -LiteralPath $Package).Path
if ([IO.Path]::GetExtension($packagePath) -ne '.zip') { throw 'Package must be a ZIP file.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $entries = @($archive.Entries | ForEach-Object FullName)
    foreach ($required in @('Station.Desktop.exe', 'wwwroot/index.html', 'INSTALL.md', 'THIRD-PARTY-NOTICES.md', 'manifest.json')) {
        if (-not ($entries | Where-Object { $_.Replace('\', '/').EndsWith("/$required", [StringComparison]::OrdinalIgnoreCase) })) {
            throw "Required package entry is missing: $required"
        }
    }
    foreach ($forbidden in @('mpv.exe', 'ffmpeg.exe', 'ffprobe.exe', 'station.db', 'settings.json')) {
        if ($entries | Where-Object { [IO.Path]::GetFileName($_) -ieq $forbidden }) {
            throw "Forbidden package entry exists: $forbidden"
        }
    }
}
finally { $archive.Dispose() }

$checksumPath = "$packagePath.sha256"
if (-not (Test-Path -LiteralPath $checksumPath)) { throw 'Package checksum file is missing.' }
$expected = ((Get-Content -LiteralPath $checksumPath -Raw) -split '\s+')[0]
$actual = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expected -ne $actual) { throw 'Package checksum does not match.' }
Write-Output "RELEASE_PACKAGE_ENTRIES=$($entries.Count)"
Write-Output 'RELEASE_PACKAGE=passed'
