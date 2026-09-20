[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$MountRoot,

    [string]$OutputJsonl = (Join-Path $PSScriptRoot '..\artifacts\import-validation\ktv_songs_index.jsonl'),
    [string]$OutputJson = (Join-Path $PSScriptRoot '..\artifacts\import-validation\ktv_songs_index.json'),
    [string[]]$IncludeYearFolder = @('16年', '17年', '18年', '19年', '20年', '21年', '22年', '23年', '24年', '25年'),
    [string[]]$ExcludeDirectoryName = @('2008-2015年'),
    [string[]]$Extensions = @('.mkv')
)

$ErrorActionPreference = 'Stop'

function Remove-TrailingPart([System.Collections.Generic.List[string]]$Parts) {
    $value = $Parts[$Parts.Count - 1]
    $Parts.RemoveAt($Parts.Count - 1)
    return $value
}

function Parse-SongName([string]$FileName) {
    $stem = [IO.Path]::GetFileNameWithoutExtension($FileName).Normalize([Text.NormalizationForm]::FormKC).Trim()
    $parts = [regex]::Split($stem, '\s*[-－–—]\s*') |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_.Trim() }
    $parts = [System.Collections.Generic.List[string]]::new([string[]]$parts)

    $languages = @('国语', '粤语', '英语', '日语', '韩语', '闽南语', '客家语', '纯音乐')
    $categories = @('流行', '流行歌曲', '合唱', '儿歌', '民歌', '摇滚', '经典', '舞曲', 'DJ', '古典', '轻音乐', '影视', '电子', '朋克', '嘻哈', '民谣')
    $language = $null
    $category = $null

    while ($parts.Count -gt 1) {
        $last = $parts[$parts.Count - 1]
        if ($null -eq $language -and $languages -contains $last) {
            $language = Remove-TrailingPart $parts
            continue
        }
        if ($null -eq $category -and $categories -contains $last) {
            $category = Remove-TrailingPart $parts
            continue
        }
        break
    }

    $core = ($parts -join '-').Trim()
    $separator = $core.IndexOf('-')
    if ($separator -gt 0 -and $separator -lt ($core.Length - 1)) {
        $artist = $core.Substring(0, $separator).Trim()
        $title = $core.Substring($separator + 1).Trim()
    }
    else {
        $artist = '未知歌手'
        $title = if ([string]::IsNullOrWhiteSpace($core)) { $stem } else { $core }
    }

    return [pscustomobject]@{
        Artist = $artist
        Title = $title
        Language = $language
        Category = $category
    }
}

$root = (Resolve-Path -LiteralPath $MountRoot).Path.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
$normalizedExtensions = @($Extensions | ForEach-Object { $_.ToLowerInvariant().Trim() })
$records = [System.Collections.Generic.List[object]]::new()
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($yearFolder in $IncludeYearFolder) {
    $yearPath = Join-Path $root $yearFolder
    if (-not (Test-Path -LiteralPath $yearPath -PathType Container)) {
        Write-Warning "跳过不存在的年度目录：$yearFolder"
        continue
    }

    Write-Host "扫描 $yearFolder ..."
    try {
        $files = Get-ChildItem -LiteralPath $yearPath -File -Recurse -Force -ErrorAction Stop |
            Where-Object { $normalizedExtensions -contains $_.Extension.ToLowerInvariant() }
        foreach ($file in $files) {
            $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
            $segments = $relative.Split('/')
            if ($segments | Where-Object { $ExcludeDirectoryName -contains $_ }) { continue }

            $parsed = Parse-SongName $file.Name
            $records.Add([ordered]@{
                relativePath = $relative
                fileName = $file.Name
                extension = $file.Extension.ToLowerInvariant()
                sizeBytes = [int64]$file.Length
                yearFolder = $segments[0]
                artist = $parsed.Artist
                title = $parsed.Title
                language = $parsed.Language
                category = $parsed.Category
                canonicalFileName = $file.Name
                durationMs = $null
                probeStatus = 'NotProbed'
            })
            if (($records.Count % 500) -eq 0) { Write-Host "已发现 $($records.Count) 首" }
        }
    }
    catch {
        $errors.Add("$yearFolder：$($_.Exception.Message)")
    }
}

$records = @($records | Sort-Object relativePath)
if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Warning $_ }
    throw '扫描存在错误，未写出不完整的索引。请确认 CloudDrive 挂载在线后重试。'
}
if ($records.Count -eq 0) { throw '没有找到可导出的媒体文件。' }

function Write-AtomicTextFile([string]$Path, [scriptblock]$Writer) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $directory = [IO.Path]::GetDirectoryName($fullPath)
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $temporaryPath = "$fullPath.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        $utf8 = [Text.UTF8Encoding]::new($false)
        $stream = [IO.StreamWriter]::new($temporaryPath, $false, $utf8)
        try { & $Writer $stream }
        finally { $stream.Dispose() }
        Move-Item -LiteralPath $temporaryPath -Destination $fullPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
    }
}

# PowerShell cannot reliably bind the generic one-argument JsonSerializer.Serialize<T>()
# overload for OrderedDictionary/PS objects on every pwsh/.NET runtime.
# Bind explicitly to Serialize(object, Type, JsonSerializerOptions) instead.
function ConvertTo-JsonSerializedLine([object]$Value) {
    if ($null -eq $Value) { return 'null' }

    return [Text.Json.JsonSerializer]::Serialize(
        [object]$Value,
        $Value.GetType(),
        [Text.Json.JsonSerializerOptions]$null
    )
}

Write-AtomicTextFile $OutputJsonl {
    param($stream)
    foreach ($record in $records) {
        $stream.WriteLine((ConvertTo-JsonSerializedLine $record))
    }
}

Write-AtomicTextFile $OutputJson {
    param($stream)
    $stream.WriteLine('[')
    for ($index = 0; $index -lt $records.Count; $index++) {
        $suffix = if ($index -lt ($records.Count - 1)) { ',' } else { '' }
        $stream.WriteLine(((ConvertTo-JsonSerializedLine $records[$index]) + $suffix))
    }
    $stream.WriteLine(']')
}

Write-Host "完成：$($records.Count) 首"
Write-Host "JSONL：$([IO.Path]::GetFullPath($OutputJsonl))"
Write-Host "JSON ：$([IO.Path]::GetFullPath($OutputJson))"
Write-Host '已排除：2008-2015年（不会读取该目录）'
