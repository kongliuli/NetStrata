# NetStrata WPF dynamic loop — self-paced W6 steps
$intervalSec = 300
$prompt = @'
NetStrata WPF loop: (1) Continue docs/WPF-ROADMAP.md next unfinished step (W6b..W6h); (2) dotnet test + dotnet build NetStrata.Tray; (3) commit+push if step complete; (4) Report step id and status.
'@

while ($true) {
    Start-Sleep -Seconds $intervalSec
    $json = @{ prompt = $prompt.Trim() } | ConvertTo-Json -Compress
    Write-Output "AGENT_LOOP_WAKE_netstrata_wpf $json"
}
