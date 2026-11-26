# Build the standalone Windows desktop application. Windows PowerShell 5.1 or newer.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\Quaygate\Quaygate.csproj'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& dotnet build $project -c Release --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw "dotnet build ($LASTEXITCODE)" }
$source = Join-Path $PSScriptRoot 'src\Quaygate\bin\Release\net48\Quaygate.exe'
$dist = Join-Path $PSScriptRoot 'dist'
[void][IO.Directory]::CreateDirectory($dist)
Copy-Item -LiteralPath $source -Destination $dist -Force
Get-Item -LiteralPath (Join-Path $dist 'Quaygate.exe') | Select-Object Name, Length
