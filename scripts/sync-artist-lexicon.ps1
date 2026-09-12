param(
    [Parameter(Mandatory = $true)][string]$Source,
    [string]$Destination = (Join-Path $PSScriptRoot '..\src\Station.Infrastructure\Search\artists.json')
)

$data = Get-Content -LiteralPath $Source -Raw | ConvertFrom-Json -AsHashtable
$ordered = [ordered]@{}
foreach ($name in ($data.Keys | Sort-Object)) {
    $entry = $data[$name]
    $ordered[$name.Trim()] = [ordered]@{
        group = [string]$entry.group
        popularity = [int]$entry.popularity
        imageUrl = [string]$entry.imageUrl
    }
}
$directory = Split-Path -Parent $Destination
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$ordered | ConvertTo-Json -Depth 4 -Compress | Set-Content -LiteralPath $Destination -Encoding utf8NoBOM
Write-Output "ARTIST_LEXICON_COUNT=$($ordered.Count)"
