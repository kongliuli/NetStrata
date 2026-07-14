using NetStrata.Core.Models;
using NetStrata.Core.Ui;

namespace NetStrata.Core.Flow;

public static class FlowTraceBuilder
{
    private static readonly string[] LayerKeys =
        ["wifi", "lan", "broadband", "overseas_direct", "proxy", "ai"];

    public static IReadOnlyList<FlowTrace> FromState(DaemonState? state, string? lang = null)
    {
        lang = LangResolver.Resolve(lang);
        return
        [
            BuildLayers(state, lang),
            BuildRoutes(state, lang),
            BuildTls(state, lang)
        ];
    }

    private static FlowTrace BuildLayers(DaemonState? state, string lang)
    {
        var sample = state?.Latest;
        var byLayer = sample?.Verdict?.Layers.ToDictionary(x => x.Layer, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, LayerVerdict>(StringComparer.OrdinalIgnoreCase);
        var nodes = LayerKeys.Select((key, index) =>
        {
            byLayer.TryGetValue(key, out var layer);
            var reasons = layer is null ? [] : UiStrings.Phrases(lang, layer.Reasons);
            return new FlowNode(
                key,
                UiStrings.LayerName(lang, key),
                MapState(layer?.State),
                null,
                reasons.Count == 0 ? UiStrings.T(lang, "暂无详细原因", "No detail available") : string.Join("; ", reasons),
                index < 3 ? index : index == 5 ? 4 : 3,
                key is "overseas_direct" ? "direct" : key is "proxy" ? "proxy" : null);
        }).ToList();

        return new FlowTrace(
            FlowTraceMode.Layers,
            lang,
            UiStrings.T(lang, "分层诊断", "Layered diagnosis"),
            UiStrings.T(lang, "诊断结果重放，非逐包抓取", "Diagnostic result replay, not packet capture"),
            sample?.T ?? "",
            nodes,
            [
                new("wifi", "lan"),
                new("lan", "broadband"),
                new("broadband", "overseas_direct", "direct"),
                new("broadband", "proxy", "proxy"),
                new("overseas_direct", "ai", "direct"),
                new("proxy", "ai", "proxy")
            ],
            sample?.Verdict is not null);
    }

    private static FlowTrace BuildRoutes(DaemonState? state, string lang)
    {
        var sample = state?.Latest;
        var direct = PickRoute(sample?.Https, "_direct");
        var proxy = PickRoute(sample?.Https, "_proxy");
        var directState = RouteState(sample?.Https, "_direct");
        var proxyState = RouteState(sample?.Https, "_proxy");
        var targetState = directState == FlowNodeState.Passed || proxyState == FlowNodeState.Passed
            ? FlowNodeState.Passed
            : directState == FlowNodeState.Failed && proxyState == FlowNodeState.Failed
                ? FlowNodeState.Failed
                : FlowNodeState.Unknown;
        var targetMs = new[] { direct, proxy }
            .Where(x => x is { Ok: true })
            .Select(x => (double?)x!.TotalMs)
            .OrderBy(x => x)
            .FirstOrDefault();

        var nodes = new List<FlowNode>
        {
            new("source", UiStrings.T(lang, "本机", "Device"), sample is null ? FlowNodeState.Unknown : FlowNodeState.Passed,
                null, UiStrings.T(lang, "探测请求入口", "Probe request source"), 0),
            new("direct", UiStrings.T(lang, "直连", "Direct"), directState, direct?.TotalMs,
                RouteDetail(direct, lang), 1, "direct"),
            new("proxy", UiStrings.T(lang, "代理", "Proxy"), proxyState, proxy?.TotalMs,
                RouteDetail(proxy, lang), 1, "proxy"),
            new("target", UiStrings.T(lang, "目标", "Target"), targetState, targetMs,
                UiStrings.T(lang, "对比直连与代理可达性", "Compare direct and proxy reachability"), 2)
        };

        return new FlowTrace(
            FlowTraceMode.Routes,
            lang,
            UiStrings.T(lang, "直连 / 代理", "Direct / proxy"),
            UiStrings.T(lang, "两条诊断路径对照，非并发请求回放", "Path comparison, not a concurrent request replay"),
            sample?.T ?? "",
            nodes,
            [
                new("source", "direct", "direct"),
                new("source", "proxy", "proxy"),
                new("direct", "target", "direct"),
                new("proxy", "target", "proxy")
            ],
            sample is not null && (direct is not null || proxy is not null));
    }

    private static FlowTrace BuildTls(DaemonState? state, string lang)
    {
        var sample = state?.Latest;
        var stack = sample?.TlsStack?.FirstOrDefault();
        var phases = new (string Id, string Label, TlsStackLayerResult? Result)[]
        {
            ("dns", "DNS", stack?.Dns),
            ("tcp", "TCP", stack?.Tcp),
            ("tls", "TLS", stack?.Tls),
            ("http", "HTTP", stack?.Http)
        };
        var stoppedIndex = Array.FindIndex(phases, p =>
            string.Equals(p.Id, stack?.StoppedAt, StringComparison.OrdinalIgnoreCase));
        var nodes = phases.Select((phase, index) => new FlowNode(
            phase.Id,
            phase.Label,
            PhaseState(phase.Result, stack is not null, stoppedIndex, index),
            phase.Result?.Ms,
            PhaseDetail(phase.Result, lang),
            index)).ToList();
        var target = stack is null ? "" : $" · {stack.Label} ({stack.Host}:{stack.Port})";

        return new FlowTrace(
            FlowTraceMode.Tls,
            lang,
            UiStrings.T(lang, "TLS 栈", "TLS stack") + target,
            UiStrings.T(lang, "真实顺序阶段，失败后停止", "Real sequential stages, stops on failure"),
            sample?.T ?? "",
            nodes,
            [new("dns", "tcp"), new("tcp", "tls"), new("tls", "http")],
            stack is not null);
    }

    private static FlowNodeState MapState(string? state) => state?.ToLowerInvariant() switch
    {
        "ok" => FlowNodeState.Passed,
        "degraded" => FlowNodeState.Degraded,
        "fail" => FlowNodeState.Failed,
        "skipped" => FlowNodeState.Skipped,
        _ => FlowNodeState.Unknown
    };

    private static HttpsResult? PickRoute(IReadOnlyList<HttpsResult>? results, string suffix) => results?
        .Where(x => x.Label.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(x => x.Ok)
        .ThenBy(x => x.TotalMs)
        .FirstOrDefault();

    private static FlowNodeState RouteState(IReadOnlyList<HttpsResult>? results, string suffix)
    {
        var route = results?.Where(x => x.Label.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)).ToList() ?? [];
        return route.Count == 0 ? FlowNodeState.Unknown
            : route.Any(x => x.Ok) ? FlowNodeState.Passed
            : FlowNodeState.Failed;
    }

    private static string RouteDetail(HttpsResult? result, string lang)
    {
        if (result is null)
            return UiStrings.T(lang, "没有该路径的探测结果", "No probe result for this path");
        if (result.Ok)
            return $"HTTP {result.HttpCode} · {result.Url}";
        return result.Err ?? UiStrings.T(lang, "请求失败", "Request failed");
    }

    private static FlowNodeState PhaseState(
        TlsStackLayerResult? result,
        bool hasStack,
        int stoppedIndex,
        int index)
    {
        if (result is not null)
            return result.Ok ? FlowNodeState.Passed : FlowNodeState.Failed;
        if (!hasStack)
            return FlowNodeState.Unknown;
        return stoppedIndex >= 0 && index > stoppedIndex ? FlowNodeState.Skipped : FlowNodeState.Unknown;
    }

    private static string PhaseDetail(TlsStackLayerResult? result, string lang)
    {
        if (result is null)
            return UiStrings.T(lang, "未执行或无数据", "Not executed or unavailable");
        if (!result.Ok)
            return result.Err ?? UiStrings.T(lang, "阶段失败", "Stage failed");
        if (result.HttpCode is not null)
            return $"HTTP {result.HttpCode}";
        if (result.Ips is { Count: > 0 })
            return string.Join(", ", result.Ips);
        return UiStrings.T(lang, "阶段通过", "Stage passed");
    }
}
