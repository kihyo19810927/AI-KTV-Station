param(
    [string]$Version = '0.1.0-dev',
    [string]$OutputDirectory = 'artifacts',
    [string]$SeedDatabasePath,
    [ValidateSet('Desktop', 'Tray')][string]$Target = 'Tray'
)

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') { throw "Invalid version: $Version" }
if (-not [string]::IsNullOrWhiteSpace($SeedDatabasePath)) {
    $SeedDatabasePath = (Resolve-Path -LiteralPath $SeedDatabasePath -ErrorAction Stop).Path
    if ([IO.Path]::GetFileName($SeedDatabasePath) -ine 'station.db') { throw 'Seed database must be named station.db.' }
    if ((Get-Item -LiteralPath $SeedDatabasePath).Length -eq 0) { throw 'Seed database must not be empty.' }

}

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source }
$outputRoot = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
}
$artifactName = "AI-KTV-Station-$Version-win-x64"
$zipPath = Join-Path $outputRoot "$artifactName.zip"
$checksumPath = "$zipPath.sha256"
$stagingRoot = Join-Path $outputRoot ".staging-$([Guid]::NewGuid().ToString('N'))"
$publishRoot = Join-Path $stagingRoot $artifactName

function Get-PortableRelativePath([string]$BasePath, [string]$Path) {
    # Windows PowerShell 5.1 runs on .NET Framework and has no Path.GetRelativePath.
    $baseUri = [Uri]::new(($BasePath.TrimEnd('\') + '\'))
    $pathUri = [Uri]::new($Path)
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()).Replace('/', '\')
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
try {
    & npm.cmd ci --prefix (Join-Path $root 'src\Station.Web')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & npm.cmd run build --prefix (Join-Path $root 'src\Station.Web')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if ($Target -eq 'Tray') {
        $project = Join-Path $root 'src\Station.Tray\Station.Tray.csproj'
        $executableName = 'Station.Tray.exe'
    }
    else {
        $project = Join-Path $root 'src\Station.Desktop\Station.Desktop.csproj'
        $executableName = 'Station.Desktop.exe'
    }
    & $dotnet restore $project --runtime win-x64 --locked-mode
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    # Some Windows SDK installations intermittently fail while MSBuild resolves
    # project references in parallel without reporting a usable diagnostic.
    # A release build is infrequent, so favor deterministic single-node publish.
    & $dotnet publish $project --configuration Release --runtime win-x64 --self-contained true --no-restore --output $publishRoot -m:1 -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Copy-Item -LiteralPath (Join-Path $root 'docs\project\WINDOWS-INSTALLATION.md') -Destination (Join-Path $publishRoot 'INSTALL.md')
    Copy-Item -LiteralPath (Join-Path $root 'docs\project\THIRD-PARTY-NOTICES.md') -Destination (Join-Path $publishRoot 'THIRD-PARTY-NOTICES.md')
    $initialLibrary = Join-Path $root 'artifacts\import-validation\ktv_songs_index.jsonl'
    if (Test-Path -LiteralPath $initialLibrary) {
        $initialLibraryRoot = Join-Path $publishRoot 'initial-library'
        New-Item -ItemType Directory -Path $initialLibraryRoot -Force | Out-Null
        Copy-Item -LiteralPath $initialLibrary -Destination (Join-Path $initialLibraryRoot 'ktv_songs_index.jsonl')
    }
    if (-not [string]::IsNullOrWhiteSpace($SeedDatabasePath)) {
        $dataRoot = Join-Path $publishRoot 'data'
        $packagedDatabase = Join-Path $dataRoot 'station.db'
        New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
        # SQLite's backup API creates a consistent read-only snapshot even when
        # the source has active WAL sidecars. Never copy the primary file alone.
        $catalogImportProject = Join-Path $root 'src\Station.CatalogImport\Station.CatalogImport.csproj'
        & $dotnet run --project $catalogImportProject --configuration Release --no-restore -- --snapshot-database $SeedDatabasePath $packagedDatabase
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        # Upgrade only the copied snapshot. This prebuilds the FTS index so a new
        # installation does not stall while opening a large existing library.
        & $dotnet run --project $catalogImportProject --configuration Release --no-restore -- --upgrade-database $packagedDatabase
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        # The upgrade may leave a WAL pair in the staging directory. Take a
        # second SQLite backup so the distribution contains one self-contained
        # database file and no sidecar recovery files.
        $normalizedDatabase = Join-Path $dataRoot 'station-final.db'
        & $dotnet run --project $catalogImportProject --configuration Release --no-restore -- --snapshot-database $packagedDatabase $normalizedDatabase
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        foreach ($stagingFile in @($packagedDatabase, "$packagedDatabase-wal", "$packagedDatabase-shm")) {
            if (Test-Path -LiteralPath $stagingFile) { Remove-Item -LiteralPath $stagingFile -Force }
        }
        Move-Item -LiteralPath $normalizedDatabase -Destination $packagedDatabase
        $stagingBackups = Join-Path $dataRoot 'backups'
        if (Test-Path -LiteralPath $stagingBackups) { Remove-Item -LiteralPath $stagingBackups -Recurse -Force }
    }

    $toolFiles = @(
        @{ Source = 'mpv\mpv.exe'; Destination = 'tools\mpv\mpv.exe' },
        @{ Source = 'mpv\vulkan-1.dll'; Destination = 'tools\mpv\vulkan-1.dll' },
        @{ Source = 'ffmpeg\ffmpeg.exe'; Destination = 'tools\ffmpeg\ffmpeg.exe' },
        @{ Source = 'ffmpeg\ffprobe.exe'; Destination = 'tools\ffmpeg\ffprobe.exe' }
    )
    foreach ($toolFile in $toolFiles) {
        $source = Join-Path $root ('common\' + $toolFile.Source)
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Bundled runtime tool is missing: $source"
        }
        $destination = Join-Path $publishRoot $toolFile.Destination
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
    $licenseSource = Join-Path $root 'common\licenses'
    if (-not (Test-Path -LiteralPath $licenseSource -PathType Container)) {
        throw "Bundled tool license notices are missing: $licenseSource"
    }
    $licenseDestination = Join-Path $publishRoot 'tools\licenses'
    New-Item -ItemType Directory -Path $licenseDestination -Force | Out-Null
    Get-ChildItem -LiteralPath $licenseSource -File | Copy-Item -Destination $licenseDestination -Force

    $forbidden = @('settings.json', 'station.db-wal', 'station.db-shm')
    $packagedNames = Get-ChildItem -LiteralPath $publishRoot -File -Recurse | ForEach-Object Name
    foreach ($name in $forbidden) {
        if ($packagedNames -contains $name) { throw "Forbidden file in package: $name" }
    }

    $manifest = Get-ChildItem -LiteralPath $publishRoot -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            [pscustomobject]@{
                Path = (Get-PortableRelativePath $publishRoot $_.FullName).Replace('\', '/')
                Size = $_.Length
                Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
    $manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $publishRoot 'manifest.json') -Encoding utf8

    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    if (Test-Path -LiteralPath $zipPath) { throw "Release artifact already exists: $zipPath" }
    Compress-Archive -LiteralPath $publishRoot -DestinationPath $zipPath -CompressionLevel Optimal
    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$zipHash  $([IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath $checksumPath -Encoding ascii

    & (Join-Path $PSScriptRoot 'verify-release-package.ps1') -Package $zipPath -ExecutableName $executableName -RequireSeedDatabase:(-not [string]::IsNullOrWhiteSpace($SeedDatabasePath))
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output "PACKAGE=$zipPath"
    Write-Output "SHA256=$zipHash"
    Write-Output 'WINDOWS_PUBLISH=passed'
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
}
