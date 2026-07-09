param(
    [switch]$Probe,
    [switch]$FailOnDegraded
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

Write-Host "==> dotnet test"
dotnet test (Join-Path $root 'NetStrata.slnx') -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $Probe) { exit 0 }

$cli = Join-Path $root 'src\NetStrata.Cli\bin\Debug\net8.0\NetStrata.Cli.exe'
if (-not (Test-Path $cli)) {
    Write-Host "==> dotnet build (cli)"
    dotnet build (Join-Path $root 'src\NetStrata.Cli\NetStrata.Cli.csproj') -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$runner = if (Test-Path $cli) { $cli } else { 'dotnet' }
$args = if (Test-Path $cli) { @('--once') } else { @('run', '--project', (Join-Path $root 'src\NetStrata.Cli\NetStrata.Cli.csproj'), '--', '--once') }

Write-Host "==> netstrata --once"
$json = & $runner @args 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$sample = $json | ConvertFrom-Json
$overall = $sample.verdict.overall
Write-Host "overall=$overall"

if ($FailOnDegraded -and $overall -in @('degraded', 'fail')) { exit 1 }
exit 0
