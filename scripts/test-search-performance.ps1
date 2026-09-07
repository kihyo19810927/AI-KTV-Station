$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source }
& $dotnet test (Join-Path $root 'tests\Station.Core.Tests\Station.Core.Tests.csproj') --configuration Release -p:UseSharedCompilation=false --no-restore --filter 'Category=Performance' --logger 'console;verbosity=normal'
exit $LASTEXITCODE
