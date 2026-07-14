using NetStrata.Core.Models;
using NetStrata.Core.Probes;
using NetStrata.Core.Ui;

namespace NetStrata.Core.Tui;

public sealed record LayerCard(string Layer, string State, string BorderColor, string DisplayName, string StateLabel);

public sealed record AiApiCard(
    string Id,
    string Name,
    string DirectState,
    string ProxyState,
    string BorderColor,
    string DirectMs,
    string ProxyMs,
    string Detail,
    string OpenUrl,
    string ProbeUrl);

public sealed record PingCard(
    string Target,
    string Label,
    string State,
    string BorderColor,
    string Detail,
    string? Url = null,
    string Kind = "ping");

public sealed record DashboardViewModel
{
    public required string Overall { get; init; }
    public required string Headline { get; init; }
    public required string AiHeadline { get; init; }
    public required string Meta { get; init; }
    public required string ProxySummary { get; init; }
    public string? AlertsSummary { get; init; }
    public IReadOnlyList<LayerCard> Layers { get; init; } = [];
    public IReadOnlyList<AiApiCard> AiApis { get; init; } = [];
    public IReadOnlyList<PingCard> CustomPings { get; init; } = [];
    public required string LayersTitle { get; init; }
    public required string AiTitle { get; init; }
    public required string PingTitle { get; init; }
    public required string RefreshLabel { get; init; }
    public bool HasData { get; init; }
    public bool HasCustomPings { get; init; }
}

public static class DashboardMapper
{
    public static DashboardViewModel FromState(DaemonState? state, string? lang = null)
    {
        lang = LangResolver.Resolve(lang);

        if (state?.Latest?.Verdict is not { } verdict)
        {
            return new DashboardViewModel
            {
                Overall = "—",
                Headline = UiStrings.WaitingHeadline(lang),
                AiHeadline = "",
                Meta = state is null ? UiStrings.NoState(lang) : UiStrings.CycleMeta(lang, state.Cycle, "—", "—"),
                ProxySummary = "",
                LayersTitle = UiStrings.SectionLayers(lang),
                AiTitle = UiStrings.SectionAi(lang),
                PingTitle = UiStrings.SectionPing(lang),
                RefreshLabel = UiStrings.Refresh(lang),
                HasData = false
            };
        }

        var sample = state.Latest;
        var pc = sample.ProxyConfig;
        var sp = pc.SystemProxy;
        var networkLayers = verdict.Layers
            .Where(l => l.Layer != "ai")
            .Select(l => new LayerCard(
                l.Layer,
                l.State,
                BorderColor(l.State),
                UiStrings.LayerName(lang, l.Layer),
                UiStrings.StateName(lang, l.State)))
            .ToList();

        return new DashboardViewModel
        {
            Overall = UiStrings.OverallName(lang, verdict.Overall),
            Headline = UiStrings.Phrase(lang, verdict.Headline),
            AiHeadline = UiStrings.Phrase(lang, verdict.Ai.Headline),
            Meta = UiStrings.CycleMeta(lang, state.Cycle, sample.T, sample.CycleMs.ToString("F0")),
            ProxySummary = BuildProxySummary(pc, sp),
            AlertsSummary = BuildAlerts(state.RecentAlerts),
            Layers = networkLayers,
            AiApis = BuildAiCards(sample, lang),
            CustomPings = BuildCustomTargetCards(sample, lang),
            LayersTitle = UiStrings.SectionLayers(lang),
            AiTitle = UiStrings.SectionAi(lang),
            PingTitle = UiStrings.SectionPing(lang),
            RefreshLabel = UiStrings.Refresh(lang),
            HasData = true,
            HasCustomPings = sample.Pings.Any(p => p.Custom)
                || sample.Https.Any(h => h.Label.StartsWith("user_", StringComparison.Ordinal))
        };
    }

    private static IReadOnlyList<AiApiCard> BuildAiCards(Sample sample, string lang)
    {
        return AiApiCatalog.Providers.Select(p =>
        {
            var d = sample.Https.FirstOrDefault(h => h.Label == $"{p.Id}_direct");
            var pr = sample.Https.FirstOrDefault(h => h.Label == $"{p.Id}_proxy");
            var dOk = d is { Ok: true };
            var pOk = pr is { Ok: true };
            var border = (dOk || pOk) ? BorderColor("ok")
                : d is null && pr is null ? BorderColor("skipped")
                : BorderColor("fail");
            var detail = dOk && pOk
                ? UiStrings.T(lang, "直连+代理均可", "direct + proxy OK")
                : dOk ? UiStrings.T(lang, "仅直连", "direct only")
                : pOk ? UiStrings.T(lang, "仅代理", "proxy only")
                : UiStrings.T(lang, "不可达", "unreachable");

            return new AiApiCard(
                p.Id,
                p.DisplayName,
                dOk ? UiStrings.StateName(lang, "ok") : (d is null ? "—" : UiStrings.StateName(lang, "fail")),
                pOk ? UiStrings.StateName(lang, "ok") : (pr is null ? "—" : UiStrings.StateName(lang, "fail")),
                border,
                UiStrings.Ms(lang, d?.TotalMs),
                UiStrings.Ms(lang, pr?.TotalMs),
                detail,
                p.OpenUrl,
                p.ProbeUrl);
        }).ToList();
    }

    private static IReadOnlyList<PingCard> BuildCustomTargetCards(Sample sample, string lang)
    {
        var cards = new List<PingCard>();
        foreach (var p in sample.Pings.Where(p => p.Custom))
        {
            var state = p.Ok ? "ok" : "fail";
            var name = string.IsNullOrWhiteSpace(p.Label) || p.Label == p.Target
                ? p.Target
                : $"{p.Label} ({p.Target})";
            var detail = p.Ok
                ? $"ping {p.AvgMs:F0} ms · loss {p.LossPct:F0}%"
                : (p.Err ?? UiStrings.StateName(lang, "fail"));
            var url = p.Label is not null && p.Label.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? p.Label
                : null;
            cards.Add(new PingCard(p.Target, name, state, BorderColor(state), detail, url, "ping"));
        }

        foreach (var h in sample.Https.Where(h => h.Label.StartsWith("user_", StringComparison.Ordinal)))
        {
            var state = h.Ok ? "ok" : "fail";
            var detail = h.Ok
                ? $"https {h.TotalMs:F0} ms · HTTP {h.HttpCode}"
                : (h.Err ?? UiStrings.StateName(lang, "fail"));
            cards.Add(new PingCard(h.Url, h.Url, state, BorderColor(state), detail, h.Url, "https"));
        }

        return cards;
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
