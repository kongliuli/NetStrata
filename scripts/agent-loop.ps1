# NetStrata agent loop: tick every 5 minutes
$intervalSec = 300
$prompt = @'
NetStrata loop: (1) Continue current ROADMAP phase with test-first; (2) End-of-round acceptance check vs ROADMAP and dotnet test; (3) If phase complete and there are changes: git add, commit, git push origin main via SSH; (4) Report phase name, pass/fail, and whether pushed.
'@

while ($true) {
    Start-Sleep -Seconds $intervalSec
    $json = @{ prompt = $prompt.Trim() } | ConvertTo-Json -Compress
    Write-Output "AGENT_LOOP_TICK_netstrata $json"
}
