param(
    [Parameter(Mandatory)][string]$CandidatePackage,
    [string]$ReleaseVersion = '1.0.0'
)

$ErrorActionPreference = 'Stop'
if ($ReleaseVersion -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid release version: $ReleaseVersion" }

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$packagePath = (Resolve-Path -LiteralPath $CandidatePackage).Path
$checksumPath = "$packagePath.sha256"
$notesPath = Join-Path $root "docs\project\RELEASE-NOTES-$ReleaseVersion.md"
$candidateRecordPath = Join-Path $root 'docs\project\CANDIDATE-VALIDATION.md'

if (-not (Test-Path -LiteralPath $checksumPath)) { throw "Missing checksum sidecar: $checksumPath" }
if (-not (Test-Path -LiteralPath $notesPath)) { throw "Missing release notes: $notesPath" }
if (-not (Test-Path -LiteralPath $candidateRecordPath)) { throw "Missing candidate validation record: $candidateRecordPath" }

& (Join-Path $PSScriptRoot 'verify-release-package.ps1') -Package $packagePath

$expectedHash = ((Get-Content -LiteralPath $checksumPath -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
$actualHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expectedHash -ne $actualHash) { throw 'Candidate checksum does not match the package.' }

$notes = Get-Content -LiteralPath $notesPath -Raw
foreach ($heading in @('主要功能', '安装与升级', '验证证据', '已知限制与待验收', '数据与隐私')) {
    if ($notes -notmatch [regex]::Escape("## $heading")) { throw "Release notes missing section: $heading" }
}
if ($notes -notmatch [regex]::Escape($actualHash)) { throw 'Release notes do not identify the validated candidate checksum.' }

$candidateName = [IO.Path]::GetFileName($packagePath)
if ($candidateName -notmatch '^AI-KTV-Station-(?<version>.+)-win-x64\.zip$') {
    throw "Candidate package name does not follow the release convention: $candidateName"
}
$candidateVersion = $Matches.version
$candidateRecord = Get-Content -LiteralPath $candidateRecordPath -Raw
if ($candidateRecord -notmatch [regex]::Escape($candidateVersion)) {
    throw 'Candidate validation record does not identify the validated candidate version.'
}
if ($candidateRecord -notmatch [regex]::Escape($actualHash)) {
    throw 'Candidate validation record does not identify the validated candidate checksum.'
}

$tag = "v$ReleaseVersion"
& git -C $root rev-parse --verify --quiet "refs/tags/$tag" *> $null
$tagExists = $LASTEXITCODE -eq 0

Write-Output "CANDIDATE_SHA256=$actualHash"
Write-Output "CANDIDATE_RECORD=$candidateRecordPath"
Write-Output "RELEASE_NOTES=$notesPath"
Write-Output 'RELEASE_MATERIALS=passed'
if ($tagExists) { Write-Output "RELEASE_TAG=$tag" }
else { Write-Output "RELEASE_TAG=pending:$tag" }
Write-Output 'FORMAL_RELEASE=blocked:hardware-license-owner-authorization'
