$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$allowed = @{
    'Station.Domain' = @()
    'Station.Application' = @('Station.Domain')
    'Station.Infrastructure' = @('Station.Application')
    'Station.Server' = @('Station.Application', 'Station.Infrastructure')
    'Station.Desktop' = @('Station.Application', 'Station.Infrastructure', 'Station.Server')
}
foreach ($projectName in $allowed.Keys) {
    $project = Join-Path $root "src\$projectName\$projectName.csproj"
    [xml]$xml = Get-Content -LiteralPath $project -Raw
    $references = @($xml.Project.ItemGroup.ProjectReference.Include | Where-Object { $_ } | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) })
    $unexpected = @($references | Where-Object { $_ -notin $allowed[$projectName] })
    if ($unexpected.Count) { throw "$projectName has forbidden project references: $($unexpected -join ', ')" }
}
$domainFiles = Get-ChildItem (Join-Path $root 'src\Station.Domain') -Filter *.cs -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
$forbidden = $domainFiles | Select-String -Pattern 'System\.IO|Microsoft\.EntityFrameworkCore|System\.Windows|Microsoft\.AspNetCore|mpv' -CaseSensitive
if ($forbidden) { throw "Domain contains forbidden dependency usage: $($forbidden.Path -join ', ')" }
Write-Output 'PROJECT_DEPENDENCIES=passed'
