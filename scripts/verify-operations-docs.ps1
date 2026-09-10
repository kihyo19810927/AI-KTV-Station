$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$required = @(
    'docs\project\OPERATIONS-GUIDE.md',
    'docs\project\WINDOWS-INSTALLATION.md',
    'docs\project\DATABASE-UPGRADE.md',
    'docs\project\USER-ACCEPTANCE-TEST.md',
    'docs\project\RELEASE-CHECKLIST.md',
    'docs\project\THIRD-PARTY-NOTICES.md'
)
foreach ($relative in $required) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required operations document is missing: $relative" }
}

$guide = Get-Content -Raw -LiteralPath (Join-Path $root 'docs\project\OPERATIONS-GUIDE.md')
foreach ($section in @('安装与目录', '配置', '启动、运行与停止', '备份与恢复', '曲库运维', '故障排除', '诊断、安全与升级节奏')) {
    if (-not $guide.Contains($section, [StringComparison]::Ordinal)) { throw "Operations section is missing: $section" }
}
foreach ($forbidden in @('删除正式 `station.db`', '扫描整个挂载根目录', '开放公网')) {
    if (-not $guide.Contains($forbidden, [StringComparison]::Ordinal)) { throw "Safety warning is missing: $forbidden" }
}
Write-Output "OPERATIONS_DOCUMENTS=$($required.Count)"
Write-Output 'OPERATIONS_DOCS=passed'
