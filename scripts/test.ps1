param([switch]$NoRestore)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source }
$projects = Get-ChildItem (Join-Path $root 'tests') -Filter '*.csproj' -Recurse | Sort-Object FullName
if (-not $projects) { throw 'No test projects found.' }
foreach ($project in $projects) {
    $arguments = @('test', $project.FullName, '--configuration', 'Release', '-p:UseSharedCompilation=false', '--settings', (Join-Path $root 'tests\coverage.runsettings'), '--collect', 'XPlat Code Coverage', '--results-directory', (Join-Path $root 'TestResults'), '--logger', 'console;verbosity=normal')
    if ($NoRestore) { $arguments += '--no-restore' }
    & $dotnet @arguments
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
