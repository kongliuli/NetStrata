using NetStrata.Core.Models;
using NetStrata.Core.Ui;

namespace NetStrata.Core.Flow;

/// <summary>One monitored target with its own expandable path animation.</summary>
public sealed record TargetPathBlock(
    string Id,
    string Title,
    /// <summary>Short egress caption, e.g. 出口：代理 / Egress: direct.</summary>
    string EgressLabel,
    /// <summary>One-line result: HTTP code, RTT, URL.</summary>
    string Summary,
    FlowNodeState Outcome,
    FlowTrace Trace,
    /// <summary>direct | proxy — which egress this target uses.</summary>
    string Lane)
{
    /// <summary>Stable content hash for skip-refresh while UI is playing / unchanged.</summary>
    public string Fingerprint =>
        $"{Id}|{Outcome}|{Lane}|{EgressLabel}|{Summary}|{Trace.CapturedAt}";
}

/// <summary>
/// Builds per-target path blocks: wifi→lan→broadband→(direct|proxy)→target.
/// Each monitor target has its own egress; expand/collapse is a UI concern.
/// </summary>
public static class MultiTargetFlowBuilder
{
    private static readonly string[] TrunkKeys = ["wifi", "lan", "broadband"];

    public static IReadOnlyList<TargetPathBlock> FromState(DaemonState? state, string? lang = null)
    {
        lang = LangResolver.Resolve(lang);
        var sample = state?.Latest;
        if (sample?.Verdict is null)
            return [];

        var byLayer = sample.Verdict.Layers.ToDictionary(x => x.Layer, StringComparer.OrdinalIgnoreCase);
        var proxyUrl = sample.ProxyConfig.ProxyUrl;
        var leaves = BuildTargets(sample, lang);
        // W9c: fail > degraded > proxy egress > direct/ok — anomalies first
        return leaves
            .Select(leaf => BuildBlock(leaf, byLayer, proxyUrl, sample.T, lang))
            .OrderBy(b => SortRank(b))
            .ThenBy(b => b.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int SortRank(TargetPathBlock b) => b.Outcome switch
    {
        FlowNodeState.Failed => 0,
        FlowNodeState.Degraded => 1,
        FlowNodeState.Unknown => 2,
        _ when b.Lane == "proxy" => 3,
        FlowNodeState.Passed => 4,
        _ => 5
    };

    private static TargetPathBlock BuildBlock(
        TargetLeaf leaf,
        IReadOnlyDictionary<string, LayerVerdict> byLayer,
        string? proxyUrl,
        string capturedAt,
        string lang)
    {
        var nodes = new List<FlowNode>();
        var stage = 0;
        foreach (var key in TrunkKeys)
        {
            byLayer.TryGetValue(key, out var layer);
            var reasons = layer is null ? [] : UiStrings.Phrases(lang, layer.Reasons);
            nodes.Add(new FlowNode(
                key,
                UiStrings.LayerName(lang, key),
                MapState(layer?.State),
                null,
                reasons.Count == 0 ? UiStrings.T(lang, "公共路径", "Shared path") : string.Join("; ", reasons),
                stage++));
        }

        var egressKey = leaf.Lane == "proxy" ? "proxy" : "overseas_direct";
        byLayer.TryGetValue(egressKey, out var egressLayer);
        var egressReasons = egressLayer is null ? [] : UiStrings.Phrases(lang, egressLayer.Reasons);
        var egressNodeId = leaf.Lane == "proxy" ? "proxy" : "direct";
        var egressTitle = leaf.Lane == "proxy"
            ? UiStrings.T(lang, "代理出口", "Proxy egress")
            : UiStrings.T(lang, "直连出口", "Direct egress");
        var egressDetail = leaf.Lane == "proxy"
            ? (string.IsNullOrEmpty(proxyUrl)
                ? (egressReasons.Count == 0 ? UiStrings.T(lang, "经代理", "via proxy") : string.Join("; ", egressReasons))
                : proxyUrl)
            : (egressReasons.Count == 0
                ? UiStrings.T(lang, "国际直连", "Overseas direct")
                : string.Join("; ", egressReasons));

        nodes.Add(new FlowNode(
            egressNodeId,
            egressTitle,
            MapState(egressLayer?.State),
            null,
            egressDetail,
            stage++,
            leaf.Lane));

        nodes.Add(new FlowNode(
            leaf.Id,
            leaf.Title,
            leaf.State,
            leaf.DurationMs,
            leaf.Detail,
            stage,
            leaf.Lane));

        var edges = new List<FlowEdge>
        {
            new("wifi", "lan"),
            new("lan", "broadband"),
            new("broadband", egressNodeId, leaf.Lane),
            new(egressNodeId, leaf.Id, leaf.Lane)
        };

        var egressLabel = leaf.Lane == "proxy"
            ? UiStrings.T(lang, "出口：代理", "Egress: proxy")
                + (string.IsNullOrEmpty(proxyUrl) ? "" : $" · {proxyUrl}")
            : UiStrings.T(lang, "出口：直连", "Egress: direct");

        var summary = leaf.State == FlowNodeState.Passed
            ? $"{leaf.DurationMs:F0} ms · {leaf.Detail}"
            : leaf.Detail;

        var trace = new FlowTrace(
            FlowTraceMode.Probe,
            lang,
            leaf.Title,
            egressLabel,
            capturedAt,
            nodes,
            edges,
            HasData: true,
            ActiveLane: leaf.Lane);

        return new TargetPathBlock(leaf.Id, leaf.Title, egressLabel, summary, leaf.State, trace, leaf.Lane);
    }

    public static IReadOnlyList<TargetLeaf> BuildTargets(Sample sample, string lang)
    {
        var groups = sample.Https
            .GroupBy(h => BaseLabel(h.Label), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var list = new List<TargetLeaf>();
        foreach (var g in groups)
        {
            var direct = g.FirstOrDefault(h => h.Via.Equals("direct", StringComparison.OrdinalIgnoreCase));
            var proxy = g.FirstOrDefault(h => h.Via.Equals("proxy", StringComparison.OrdinalIgnoreCase));
            var chosen = PickPath(direct, proxy);
            if (chosen is null)
                continue;

            var lane = chosen.Via.Equals("proxy", StringComparison.OrdinalIgnoreCase) ? "proxy" : "direct";
            var viaLabel = lane == "proxy"
                ? UiStrings.T(lang, "经代理", "via proxy")
                : UiStrings.T(lang, "直连", "direct");
            var detail = chosen.Ok
                ? $"HTTP {chosen.HttpCode} · {viaLabel} · {chosen.Url}"
                : (chosen.Err ?? UiStrings.T(lang, "不可达", "unreachable")) + $" · {viaLabel}";

            list.Add(new TargetLeaf(
                $"tgt:{g.Key}",
                DisplayTitle(g.Key, lang),
                lane,
                chosen.Ok ? FlowNodeState.Passed : FlowNodeState.Failed,
                chosen.TotalMs > 0 ? chosen.TotalMs : null,
                detail));
        }

        return list;
    }

    private static HttpsResult? PickPath(HttpsResult? direct, HttpsResult? proxy)
    {
        if (direct is { Ok: true } && proxy is { Ok: true })
            return direct.TotalMs <= proxy.TotalMs ? direct : proxy;
        if (direct is { Ok: true })
            return direct;
        if (proxy is { Ok: true })
            return proxy;
        return direct ?? proxy;
    }

    private static string BaseLabel(string label)
    {
        if (label.EndsWith("_direct", StringComparison.OrdinalIgnoreCase))
            return label[..^"_direct".Length];
        if (label.EndsWith("_proxy", StringComparison.OrdinalIgnoreCase))
            return label[..^"_proxy".Length];
        return label;
    }

    private static string DisplayTitle(string baseLabel, string lang) => baseLabel.ToLowerInvariant() switch
    {
        "openai" => "OpenAI",
        "anthropic" => "Anthropic",
        "cursor" => "Cursor",
        "google" or "gemini" => "Google",
        "github" => "GitHub",
        "cloudflare" or "cf" => "Cloudflare",
        "baidu" => UiStrings.T(lang, "百度", "Baidu"),
        "taobao" => UiStrings.T(lang, "淘宝", "Taobao"),
        _ => baseLabel
    };

    private static FlowNodeState MapState(string? state) => state?.ToLowerInvariant() switch
    {
        "ok" => FlowNodeState.Passed,
        "degraded" => FlowNodeState.Degraded,
        "fail" => FlowNodeState.Failed,
        "skipped" => FlowNodeState.Skipped,
        _ => FlowNodeState.Unknown
    };

    public sealed record TargetLeaf(
        string Id,
        string Title,
        string Lane,
        FlowNodeState State,
        double? DurationMs,
        string Detail);
}
