using NetStrata.Core.Models;

namespace NetStrata.Core.Tui;

public sealed record LayerCard(string Layer, string State, string BorderColor);

public sealed record DashboardViewModel
{
    public required string Overall { get; init; }
    public required string Headline { get; init; }
    public required string Meta { get; init; }
    public required string ProxySummary { get; init; }
    public string? AlertsSummary { get; init; }
    public IReadOnlyList<LayerCard> Layers { get; init; } = [];
    public bool HasData { get; init; }
}

public static class DashboardMapper
{
    public static DashboardViewModel FromState(DaemonState? state)
    {
        if (state?.Latest?.Verdict is not { } verdict)
        {
            return new DashboardViewModel
            {
                Overall = "—",
                Headline = "等待 Daemon 写入 state.json…",
                Meta = state is null ? "无 state.json" : $"周期 #{state.Cycle}",
                ProxySummary = "",
                HasData = false
            };
        }

        var sample = state.Latest;
        var pc = sample.ProxyConfig;
        var sp = pc.SystemProxy;

        return new DashboardViewModel
        {
            Overall = verdict.Overall,
            Headline = verdict.Headline,
            Meta = $"周期 #{state.Cycle} · {sample.T} · {sample.CycleMs:F0}ms",
            ProxySummary = BuildProxySummary(pc, sp),
            AlertsSummary = BuildAlerts(state.RecentAlerts),
            Layers = verdict.Layers
                .Select(l => new LayerCard(l.Layer, l.State, BorderColor(l.State)))
                .ToList(),
            HasData = true
        };
    }

    private static string BuildAlerts(IReadOnlyList<Alert> alerts)
    {
        if (alerts.Count == 0)
            return null!;
        return string.Join(" · ", alerts.TakeLast(3).Select(a => a.Message));
    }

    private static string BuildProxySummary(ProxyConfig pc, SystemProxySettings sp)
    {
        var bits = new List<string>();
        if (!string.IsNullOrWhiteSpace(pc.ProxyUrl))
            bits.Add($"proxy {pc.ProxyUrl}");
        bits.Add(pc.Listening ? "listening" : "not listening");
        if (!string.IsNullOrWhiteSpace(pc.ListenerProcess))
            bits.Add(pc.ListenerProcess);

        var sys = new List<string>();
        if (sp.HttpEnable && sp.HttpProxy is not null)
            sys.Add($"http {sp.HttpProxy}:{sp.HttpPort ?? 80}");
        if (sp.HttpsEnable && sp.HttpsProxy is not null)
            sys.Add($"https {sp.HttpsProxy}:{sp.HttpsPort ?? 443}");
        if (sp.SocksEnable && sp.SocksProxy is not null)
            sys.Add($"socks {sp.SocksProxy}:{sp.SocksPort ?? 1080}");

        if (sys.Count > 0)
            bits.Add("system " + string.Join(", ", sys));
        return string.Join(" · ", bits);
    }

    public static string BorderColor(string state) => state switch
    {
        "ok" => "#34A853",
        "fail" => "#EA4335",
        "degraded" => "#FBBC04",
        "skipped" => "#5F6368",
        _ => "#2D323C"
    };
}
