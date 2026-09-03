param([ValidateSet('Server', 'Desktop')][string]$Target = 'Server')
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source }
$project = if ($Target -eq 'Desktop') { 'src\Station.Desktop\Station.Desktop.csproj' } else { 'src\Station.Server\Station.Server.csproj' }
& $dotnet run --project (Join-Path $root $project)
exit $LASTEXITCODE
