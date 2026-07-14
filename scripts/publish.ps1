# Publish NetStrata.exe (WPF tray + CLI) to artifacts/publish (win-x64 single-file)
$ErrorActionPreference = "Stop"
$out = Join-Path $PSScriptRoot "..\artifacts\publish"
New-Item -ItemType Directory -Force -Path $out | Out-Null

Write-Host "Publishing NetStrata.Tray (NetStrata.exe)..."
dotnet publish (Join-Path $PSScriptRoot "..\src\NetStrata.Tray\NetStrata.Tray.csproj") `
    -c Release -r win-x64 -o $out

Write-Host "Done:"
Get-ChildItem $out -Filter "*.exe" | ForEach-Object {
    $mb = [math]::Round($_.Length / 1MB, 1)
    Write-Host "  $($_.Name)  ${mb} MB"
}
