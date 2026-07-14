using NetStrata.Core.Models;
using NetStrata.Core.Ui;

namespace NetStrata.Core.Tui;

public sealed record ChainRow(
    string LayerKey,
    string DisplayName,
    string State,
    string StateLabel,
    string BorderColor,
    IReadOnlyList<string> Reasons,
    string MetricsSummary);

public sealed record ChainViewModel
{
    public required string Overall { get; init; }
    public required string Headline { get; init; }
    public required string AiHeadline { get; init; }
    public required string Meta { get; init; }
    public IReadOnlyList<ChainRow> Rows { get; init; } = [];
    public bool HasData { get; init; }
}

public static class ChainMapper
{
    public static ChainViewModel FromState(DaemonState? state, string? lang = null)
    {
        lang = LangResolver.Resolve(lang);
        if (state?.Latest?.Verdict is not { } v)
        {
            return new ChainViewModel
            {
                Overall = "—",
                Headline = UiStrings.WaitingHeadline(lang),
                AiHeadline = "",
                Meta = state is null ? UiStrings.NoState(lang) : $"#{state.Cycle}",
                HasData = false
            };
        }

        var sample = state.Latest;
        var rows = v.Layers.Select(l => new ChainRow(
            l.Layer,
            UiStrings.LayerName(lang, l.Layer),
            l.State,
            UiStrings.StateName(lang, l.State),
            DashboardMapper.BorderColor(l.State),
            UiStrings.Phrases(lang, l.Reasons),
            FormatMetrics(lang, l.Metrics))).ToList();

        return new ChainViewModel
        {
            Overall = UiStrings.OverallName(lang, v.Overall),
            Headline = UiStrings.Phrase(lang, v.Headline),
            AiHeadline = UiStrings.Phrase(lang, v.Ai.Headline),
            Meta = UiStrings.CycleMeta(lang, state.Cycle, sample.T, sample.CycleMs.ToString("F0")),
            Rows = rows,
            HasData = true
        };
    }

    private static string FormatMetrics(string lang, IReadOnlyDictionary<string, object?> metrics)
    {
        if (metrics.Count == 0)
            return "";
        return string.Join(" · ", metrics
            .Where(kv => kv.Value is not null)
            .Take(6)
            .Select(kv => $"{UiStrings.MetricKey(lang, kv.Key)}={FormatMetricValue(lang, kv.Value)}"));
    }

    private static string FormatMetricValue(string lang, object? value) => value switch
    {
        bool b => b
            ? UiStrings.T(lang, "是", "true")
            : UiStrings.T(lang, "否", "false"),
        _ => value?.ToString() ?? ""
    };
}

public sealed record LocalNetViewModel
{
    public required string Title { get; init; }
    public IReadOnlyList<(string Label, string Value)> Rows { get; init; } = [];
    public bool HasData { get; init; }
}

public static class LocalNetMapper
{
    public static LocalNetViewModel FromState(DaemonState? state, string? lang = null)
    {
        lang = LangResolver.Resolve(lang);
        var title = UiStrings.T(lang, "本机网络", "Local network");
        var sample = state?.Latest;
        if (sample is null)
        {
            return new LocalNetViewModel
            {
                Title = title,
                Rows = [(UiStrings.T(lang, "状态", "Status"), UiStrings.WaitingHeadline(lang))],
                HasData = false
            };
        }

        var iface = sample.Iface;
        var wifi = sample.Wifi;
        var pc = sample.ProxyConfig;
        var rows = new List<(string, string)>
        {
            (UiStrings.T(lang, "本机 IPv4", "Local IPv4"), iface?.Ipv4 ?? "—"),
            (UiStrings.T(lang, "本机 IPv6", "Local IPv6"), iface?.Ipv6 ?? "—"),
            (UiStrings.T(lang, "网关", "Gateway"), iface?.Gateway ?? "—"),
            (UiStrings.T(lang, "链路类型", "Link"), iface?.LinkType ?? "—"),
            (UiStrings.T(lang, "网卡", "Adapter"), iface?.PrimaryDevice ?? iface?.HardwarePort ?? "—"),
            (UiStrings.T(lang, "DNS", "DNS"), iface?.DhcpDns is { Count: > 0 } d ? string.Join(", ", d) : "—"),
            (UiStrings.T(lang, "Wi‑Fi", "Wi‑Fi"), wifi is null
                ? "—"
                : $"{WifiStatus(lang, wifi.Status)} · {wifi.Ssid ?? "?"} · RSSI {wifi.Rssi?.ToString() ?? "?"}"),
            (UiStrings.T(lang, "系统代理", "System proxy"), SummarizeProxy(lang, pc)),
            (UiStrings.T(lang, "出口 IP", "Egress IP"), sample.ProxyEgress?.Ip ?? "—"),
            (UiStrings.T(lang, "Tailscale", "Tailscale"), sample.Tailscale is null
                ? "—"
                : $"{(sample.Tailscale.Installed ? UiStrings.T(lang, "已安装", "installed") : UiStrings.T(lang, "未安装", "missing"))} · {(sample.Tailscale.SignedIn ? UiStrings.T(lang, "已登录", "signed-in") : UiStrings.T(lang, "未登录", "signed-out"))} · {sample.Tailscale.Address ?? ""}"),
            (UiStrings.T(lang, "路由提示", "Route hints"), iface?.RouteHints is { Count: > 0 } h ? string.Join(", ", h) : "—")
        };

        return new LocalNetViewModel { Title = title, Rows = rows, HasData = true };
    }

    private static string WifiStatus(string lang, string? status) => status switch
    {
        "connected" => UiStrings.T(lang, "已连接", "connected"),
        "disconnected" => UiStrings.T(lang, "未连接", "disconnected"),
        "not_wifi" => UiStrings.T(lang, "非 Wi‑Fi", "not_wifi"),
        "no_interface" => UiStrings.T(lang, "无网卡", "no_interface"),
        _ => status ?? "—"
    };

    private static string SummarizeProxy(string lang, ProxyConfig pc)
    {
        if (!string.IsNullOrWhiteSpace(pc.ProxyUrl))
            return $"{pc.ProxyUrl} · {(pc.Listening ? UiStrings.T(lang, "监听中", "listening") : UiStrings.T(lang, "未监听", "not listening"))} · {pc.ListenerProcess ?? ""}".Trim(' ', '·');
        var sp = pc.SystemProxy;
        if (sp.HttpEnable && sp.HttpProxy is not null)
            return $"http {sp.HttpProxy}:{sp.HttpPort ?? 80}";
        return UiStrings.T(lang, "关闭", "off");
    }
}
