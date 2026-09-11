param(
    [string]$Version = '0.1.0-dev',
    [string]$OutputDirectory = 'artifacts'
)

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') { throw "Invalid version: $Version" }

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory, $root)
$artifactName = "AI-KTV-Station-$Version-win-x64"
$zipPath = Join-Path $outputRoot "$artifactName.zip"
$checksumPath = "$zipPath.sha256"
$stagingRoot = Join-Path $outputRoot ".staging-$([Guid]::NewGuid().ToString('N'))"
$publishRoot = Join-Path $stagingRoot $artifactName

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
try {
    & npm.cmd ci --prefix (Join-Path $root 'src\Station.Web')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & npm.cmd run build --prefix (Join-Path $root 'src\Station.Web')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $project = Join-Path $root 'src\Station.Desktop\Station.Desktop.csproj'
    & $dotnet restore $project --runtime win-x64 --locked-mode
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & $dotnet publish $project --configuration Release --runtime win-x64 --self-contained true --no-restore --output $publishRoot -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Copy-Item -LiteralPath (Join-Path $root 'docs\project\WINDOWS-INSTALLATION.md') -Destination (Join-Path $publishRoot 'INSTALL.md')
    Copy-Item -LiteralPath (Join-Path $root 'docs\project\THIRD-PARTY-NOTICES.md') -Destination (Join-Path $publishRoot 'THIRD-PARTY-NOTICES.md')
    $initialLibrary = Join-Path $root 'artifacts\import-validation\ktv_songs_index.jsonl'
    if (Test-Path -LiteralPath $initialLibrary) {
        $initialLibraryRoot = Join-Path $publishRoot 'initial-library'
        New-Item -ItemType Directory -Path $initialLibraryRoot -Force | Out-Null
        Copy-Item -LiteralPath $initialLibrary -Destination (Join-Path $initialLibraryRoot 'ktv_songs_index.jsonl')
    }

    $forbidden = @('mpv.exe', 'ffmpeg.exe', 'ffprobe.exe', 'station.db', 'settings.json')
    $packagedNames = Get-ChildItem -LiteralPath $publishRoot -File -Recurse | ForEach-Object Name
    foreach ($name in $forbidden) {
        if ($packagedNames -contains $name) { throw "Forbidden file in package: $name" }
    }

    $manifest = Get-ChildItem -LiteralPath $publishRoot -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            [pscustomobject]@{
                Path = [IO.Path]::GetRelativePath($publishRoot, $_.FullName).Replace('\', '/')
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

    & (Join-Path $PSScriptRoot 'verify-release-package.ps1') -Package $zipPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output "PACKAGE=$zipPath"
    Write-Output "SHA256=$zipHash"
    Write-Output 'WINDOWS_PUBLISH=passed'
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
}
