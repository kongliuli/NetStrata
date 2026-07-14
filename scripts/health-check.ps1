param(
    [switch]$Probe,
    [switch]$FailOnDegraded
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

Write-Host "==> dotnet test"
dotnet test (Join-Path $root 'NetStrata.slnx') -v q --filter "Category!=Integration"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $Probe) { exit 0 }

$trayProj = Join-Path $root 'src\NetStrata.Tray\NetStrata.Tray.csproj'
Write-Host "==> NetStrata --once"
$json = & dotnet run --project $trayProj -- --once 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$sample = $json | ConvertFrom-Json
$overall = $sample.verdict.overall
Write-Host "overall=$overall"

if ($FailOnDegraded -and $overall -in @('degraded', 'fail')) { exit 1 }
exit 0
