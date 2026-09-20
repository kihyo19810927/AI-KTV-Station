param(
    [Parameter(Mandatory = $true)][string]$Source,
    [string]$Destination = (Join-Path $PSScriptRoot '..\src\Station.Infrastructure\Search\artists.json'),
    [string]$Overrides = (Join-Path $PSScriptRoot 'artist-lexicon-overrides.json')
)

$ErrorActionPreference = 'Stop'

function Get-StableArtistId([hashtable]$Entry, [string]$Name, [string]$Country, [string]$Group) {
    if (-not [string]::IsNullOrWhiteSpace([string]$Entry.id)) { return ([string]$Entry.id).Trim() }
    $source = ([string]$Entry.source).Trim().ToLowerInvariant()
    $sourceArtistId = ([string]$Entry.sourceArtistId).Trim()
    if ($source -and $sourceArtistId) { return "$source`:$sourceArtistId" }
    $identity = "$Name|$Country|$Group".ToLowerInvariant()
    $hash = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($identity))
    return 'legacy:' + [Convert]::ToHexString($hash).ToLowerInvariant().Substring(0, 20)
}

function Get-Country([hashtable]$Entry, [string]$Group) {
    $country = ([string]$Entry.country).Trim()
    if ($country) { return $country }
    switch ($Group) {
        '日本歌手' { return '日本' }
        '韩国歌手' { return '韩国' }
        '华语男歌手' { return '华语地区' }
        '华语女歌手' { return '华语地区' }
        '华语组合' { return '华语地区' }
        '欧美歌手' { return '欧美地区' }
        default { return '未知' }
    }
}

function Convert-Artist([hashtable]$Entry, [string]$LegacyName) {
    $name = if ([string]::IsNullOrWhiteSpace([string]$Entry.name)) { $LegacyName.Trim() } else { ([string]$Entry.name).Trim() }
    $group = ([string]$Entry.group).Trim()
    if (-not $name) { throw 'Artist name is required.' }
    if (-not $group) { throw "Artist '$name' is missing group." }
    $popularity = [int]$Entry.popularity
    if ($popularity -lt 0 -or $popularity -gt 100) { throw "Artist '$name' popularity must be between 0 and 100." }
    $country = Get-Country $Entry $group
    $aliases = @($Entry.aliases | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ } | Sort-Object -Unique)
    $source = ([string]$Entry.source).Trim()
    $sourceArtistId = ([string]$Entry.sourceArtistId).Trim()
    $imageUrl = ([string]$Entry.imageUrl).Trim()
    return [ordered]@{
        id = Get-StableArtistId $Entry $name $country $group
        name = $name
        aliases = $aliases
        country = $country
        group = $group
        popularity = $popularity
        imageUrl = if ($imageUrl) { $imageUrl } else { $null }
        source = if ($source) { $source } else { 'legacy' }
        sourceArtistId = if ($sourceArtistId) { $sourceArtistId } else { $null }
    }
}

$data = Get-Content -LiteralPath $Source -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$artists = [System.Collections.Generic.List[object]]::new()
if ($data -is [System.Collections.IList]) {
    foreach ($entry in $data) { $artists.Add((Convert-Artist $entry '')) }
} elseif ($data.ContainsKey('artists')) {
    foreach ($entry in $data.artists) { $artists.Add((Convert-Artist $entry '')) }
} else {
    foreach ($name in $data.Keys) { $artists.Add((Convert-Artist $data[$name] ([string]$name))) }
}

if ($Overrides -and (Test-Path -LiteralPath $Overrides)) {
    $overrideData = Get-Content -LiteralPath $Overrides -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
    foreach ($entry in $overrideData.artists) {
        $converted = Convert-Artist $entry ''
        if ($entry.replaceByName) {
            for ($index = $artists.Count - 1; $index -ge 0; $index--) {
                if ([string]::Equals([string]$artists[$index]['name'], [string]$converted['name'], [StringComparison]::OrdinalIgnoreCase)) {
                    $artists.RemoveAt($index)
                }
            }
        }
        $artists.Add($converted)
    }
}
$duplicateIds = $artists | Group-Object { $_['id'] } | Where-Object Count -gt 1
if ($duplicateIds) { throw "Duplicate artist id(s): $($duplicateIds.Name -join ', ')" }
$output = [ordered]@{ schemaVersion = 2; artists = @($artists | Sort-Object { $_['name'] }, { $_['country'] }, { $_['id'] }) }
$directory = Split-Path -Parent $Destination
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$output | ConvertTo-Json -Depth 8 -Compress | Set-Content -LiteralPath $Destination -Encoding utf8NoBOM
Write-Output 'ARTIST_LEXICON_SCHEMA=2'
Write-Output "ARTIST_LEXICON_COUNT=$($artists.Count)"
