using NetStrata.Core.Models;

namespace NetStrata.Core.Judge;

public sealed class VerdictEngine
{
    private static readonly HashSet<string> DomesticDirectLabels =
    [
        "baidu_direct", "taobao_direct", "anthropic_direct", "openai_direct"
    ];

    public Verdict Judge(Sample sample)
    {
        var layers = new List<LayerVerdict>();

        var linkType = sample.Iface?.LinkType ?? "unknown";
        var wifiReasons = new List<string>();
        var wifiState = LayerState.Ok;
        var w = sample.Wifi;

        if (linkType is "ethernet" or "other")
        {
            wifiState = LayerState.Skipped;
            var port = sample.Iface?.HardwarePort;
            wifiReasons.Add(port is not null
                ? $"primary link is {linkType} ({port})"
                : $"primary link is {linkType}");
        }
        else if (w is null || w.Status == "not_wifi")
        {
            wifiState = LayerState.Skipped;
            wifiReasons.Add("primary link is not Wi-Fi");
        }
        else if (w.Status == "no_interface")
        {
            wifiState = LayerState.Skipped;
            wifiReasons.Add("no Wi-Fi interface");
        }
        else if (w.Status != "connected")
        {
            wifiState = LayerState.Fail;
            wifiReasons.Add($"status={w.Status}");
        }
        else
        {
            if (w.Rssi is not null)
            {
                if (w.Rssi <= -80)
                {
                    wifiState = LayerState.Fail;
                    wifiReasons.Add($"weak signal {w.Rssi}dBm");
                }
                else if (w.Rssi <= -70)
                {
                    wifiState = LayerState.Degraded;
                    wifiReasons.Add($"marginal signal {w.Rssi}dBm");
                }
            }

            if (w.TxRate is not null && w.TxRate < 50)
            {
                wifiState = Worse(wifiState, LayerState.Degraded);
                wifiReasons.Add($"low tx rate {w.TxRate}Mbps");
            }
        }

        layers.Add(new LayerVerdict
        {
            Layer = "wifi",
            State = ToStateString(wifiState),
            Reasons = wifiReasons,
            Metrics = w is not null
                ? new Dictionary<string, object?>
                {
                    ["rssi"] = w.Rssi,
                    ["noise"] = w.Noise,
                    ["channel"] = w.Channel,
                    ["txRate"] = w.TxRate,
                    ["ssid"] = w.SsidRedacted ? " " : w.Ssid,
                    ["linkType"] = linkType
                }
                : new Dictionary<string, object?> { ["linkType"] = linkType }
        });

        var gwPing = sample.Pings.FirstOrDefault(p => p.Target == sample.Iface?.Gateway);
        var lanReasons = new List<string>();
        var lanState = LayerState.Ok;

        if (sample.Iface?.Gateway is null)
        {
            lanState = LayerState.Unknown;
            lanReasons.Add("no gateway");
        }
        else if (gwPing is null)
        {
            lanState = LayerState.Unknown;
            lanReasons.Add("no gateway ping result");
        }
        else if (!gwPing.Ok)
        {
            lanState = LayerState.Fail;
            lanReasons.Add($"gw ping loss={gwPing.LossPct}%");
        }
        else if ((gwPing.AvgMs ?? 0) > 30)
        {
            lanState = LayerState.Degraded;
            lanReasons.Add($"gw rtt {gwPing.AvgMs:F1}ms");
        }

        layers.Add(new LayerVerdict
        {
            Layer = "lan",
            State = ToStateString(lanState),
            Reasons = lanReasons,
            Metrics = new Dictionary<string, object?>
            {
                ["gateway"] = sample.Iface?.Gateway,
                ["avgMs"] = gwPing?.AvgMs,
                ["loss"] = gwPing?.LossPct
            }
        });

        var bbReasons = new List<string>();
        var bbState = LayerState.Ok;

        if (lanState == LayerState.Fail)
        {
            bbState = LayerState.Skipped;
            bbReasons.Add("LAN fail → cannot judge");
        }
        else
        {
            var aliPing = sample.Pings.FirstOrDefault(p => p.Target == "223.5.5.5");
            var baiduHttps = sample.Https.FirstOrDefault(h => h.Label == "baidu_direct");
            var baiduDns = sample.Dns.FirstOrDefault(d =>
                d.Domain == "baidu.com" && d.Server == "223.5.5.5");

            if (aliPing is null || !aliPing.Ok)
            {
                bbState = LayerState.Fail;
                bbReasons.Add("ping 223.5.5.5 fail");
            }

            if (baiduDns is not null && !baiduDns.Ok)
            {
                bbState = Worse(bbState, LayerState.Fail);
                bbReasons.Add("dig baidu via 223.5.5.5 fail");
            }

            if (baiduHttps is not null && !baiduHttps.Ok)
            {
                bbState = Worse(bbState, LayerState.Fail);
                bbReasons.Add($"baidu https fail: {baiduHttps.Err}");
            }
            else if (baiduHttps is not null && baiduHttps.TotalMs > 1500)
            {
                bbState = Worse(bbState, LayerState.Degraded);
                bbReasons.Add($"baidu slow {baiduHttps.TotalMs:F0}ms");
            }
        }

        layers.Add(new LayerVerdict
        {
            Layer = "broadband",
            State = ToStateString(bbState),
            Reasons = bbReasons,
            Metrics = new Dictionary<string, object?>()
        });

        var overseasReasons = new List<string>();
        var overseasState = LayerState.Ok;

        if (bbState is LayerState.Fail or LayerState.Skipped)
        {
            overseasState = LayerState.Skipped;
            overseasReasons.Add("broadband fail → cannot judge");
        }
        else
        {
            var probes = sample.Https
                .Where(h => h.Via == "direct" && !DomesticDirectLabels.Contains(h.Label))
                .ToList();
            var okCount = probes.Count(h => h.Ok);

            if (probes.Count == 0)
                overseasState = LayerState.Unknown;
            else if (okCount == 0)
            {
                overseasState = LayerState.Fail;
                overseasReasons.Add("all direct overseas fail (blocked)");
            }
            else if (okCount < probes.Count)
            {
                overseasState = LayerState.Degraded;
                overseasReasons.Add($"{okCount}/{probes.Count} direct overseas ok");
            }
        }

        layers.Add(new LayerVerdict
        {
            Layer = "overseas_direct",
            State = ToStateString(overseasState),
            Reasons = overseasReasons,
            Metrics = new Dictionary<string, object?>()
        });

        var proxyReasons = new List<string>();
        var proxyState = LayerState.Ok;
        var proxyConfigured = !string.IsNullOrEmpty(sample.ProxyConfig.ProxyUrl);

        if (!proxyConfigured)
        {
            proxyState = LayerState.Skipped;
            proxyReasons.Add("no proxy configured");
        }
        else if (!sample.ProxyConfig.Listening)
        {
            proxyState = LayerState.Fail;
            var port = sample.ProxyConfig.ProxyPort;
            proxyReasons.Add(port is not null
                ? $"proxy port {port} not listening"
                : "proxy not listening");
        }
        else
        {
            var viaProxy = sample.Https.Where(h => h.Via == "proxy").ToList();
            var okCount = viaProxy.Count(h => h.Ok);

            if (viaProxy.Count == 0)
            {
                proxyState = LayerState.Unknown;
                proxyReasons.Add("no proxy HTTPS probes");
            }
            else if (okCount == 0)
            {
                proxyState = LayerState.Fail;
                proxyReasons.Add("all proxy HTTPS fail");
            }
            else if (okCount < viaProxy.Count)
            {
                proxyState = LayerState.Degraded;
                proxyReasons.Add($"{okCount}/{viaProxy.Count} proxy targets ok");
            }

            if (sample.ProxyEgress is { Ok: false })
            {
                proxyState = Worse(proxyState, LayerState.Degraded);
                proxyReasons.Add($"egress fetch failed: {sample.ProxyEgress.Err ?? "unknown"}");
            }

            var sp = sample.ProxyConfig.SystemProxy;
            var expectedPort = sample.ProxyConfig.ProxyPort;
            if (sp.HttpEnable && sp.HttpProxy == "127.0.0.1" && expectedPort is not null &&
                sp.HttpPort != expectedPort)
            {
                proxyState = Worse(proxyState, LayerState.Degraded);
                proxyReasons.Add($"system HTTP proxy port {sp.HttpPort} ≠ active {expectedPort}");
            }
        }

        layers.Add(new LayerVerdict
        {
            Layer = "proxy",
            State = ToStateString(proxyState),
            Reasons = proxyReasons,
            Metrics = new Dictionary<string, object?>
            {
                ["configured"] = proxyConfigured,
                ["proxyUrl"] = sample.ProxyConfig.ProxyUrl,
                ["listening"] = sample.ProxyConfig.Listening,
                ["egressIp"] = sample.ProxyEgress?.Ip,
                ["listenerProcess"] = sample.ProxyConfig.ListenerProcess
            }
        });

        var aiReasons = new List<string>();
        var antD = sample.Https.FirstOrDefault(h => h.Label == "anthropic_direct");
        var antP = sample.Https.FirstOrDefault(h => h.Label == "anthropic_proxy");
        var oaiD = sample.Https.FirstOrDefault(h => h.Label == "openai_direct");
        var oaiP = sample.Https.FirstOrDefault(h => h.Label == "openai_proxy");

        var proxyHits = new[] { ProxyOk(antP), ProxyOk(oaiP) }.Count(x => x);
        var directHits = new[] { DirectOk(antD), DirectOk(oaiD) }.Count(x => x);
        var noProxy = string.IsNullOrEmpty(sample.ProxyConfig.ProxyUrl);

        AiState aiState;
        string aiHeadline;

        if (noProxy)
        {
            if (directHits == 2)
            {
                aiState = AiState.DirectOnly;
                aiHeadline = "Anthropic & OpenAI reachable directly (no proxy in use)";
            }
            else if (directHits == 1)
            {
                aiState = AiState.Degraded;
                var okName = DirectOk(antD) ? "Anthropic" : "OpenAI";
                var failName = DirectOk(antD) ? "OpenAI" : "Anthropic";
                aiHeadline = $"Only {okName} reachable; {failName} direct failed (no proxy configured)";
                aiReasons.Add($"{failName} direct: {(DirectOk(antD) ? oaiD : antD)?.Err ?? "unknown"}");
            }
            else
            {
                aiState = AiState.Fail;
                aiHeadline =
                    "Anthropic & OpenAI both unreachable directly; no proxy configured to fall back on";
            }
        }
        else if (proxyState == LayerState.Fail && !DirectOk(antD) && !DirectOk(oaiD))
        {
            aiState = AiState.Skipped;
            aiHeadline = "代理挂了且直连也不通，无法判断";
        }
        else if (proxyHits == 2 && directHits >= 1)
        {
            aiState = AiState.Ok;
            aiHeadline =
                $"Anthropic & OpenAI 均可达（代理稳定，部分直连也通：{directHits}/2）";
        }
        else if (proxyHits == 2)
        {
            aiState = AiState.ProxyOnly;
            aiHeadline = "Anthropic & OpenAI 通过代理可达，直连均被屏蔽";
        }
        else if (proxyHits == 1)
        {
            aiState = AiState.Degraded;
            var okName = ProxyOk(antP) ? "Anthropic" : "OpenAI";
            var failName = ProxyOk(antP) ? "OpenAI" : "Anthropic";
            aiHeadline = $"仅 {okName} 代理可达；{failName} 代理失败";
            aiReasons.Add($"{failName} via proxy: {(ProxyOk(antP) ? oaiP : antP)?.Err ?? "unknown"}");
        }
        else if (directHits > 0)
        {
            aiState = AiState.DirectOnly;
            aiHeadline = "代理路径失败，但仍有部分直连可达 — 代理 App 异常";
        }
        else
        {
            aiState = AiState.Fail;
            aiHeadline = "Anthropic 与 OpenAI 均不可达（代理 & 直连都失败）";
        }

        layers.Add(new LayerVerdict
        {
            Layer = "ai",
            State = AiStateToLayerState(aiState),
            Reasons = aiReasons,
            Metrics = new Dictionary<string, object?>
            {
                ["anthropicProxyOk"] = ProxyOk(antP),
                ["openaiProxyOk"] = ProxyOk(oaiP),
                ["anthropicDirectOk"] = DirectOk(antD),
                ["openaiDirectOk"] = DirectOk(oaiD)
            }
        });

        string overall;
        string headline;

        if (wifiState == LayerState.Fail)
        {
            overall = "wifi_bad";
            headline = string.Join("; ", wifiReasons);
        }
        else if (lanState == LayerState.Fail)
        {
            overall = "lan_bad";
            headline = string.Join("; ", lanReasons);
        }
        else if (bbState == LayerState.Fail)
        {
            overall = "broadband_bad";
            headline = string.Join("; ", bbReasons);
        }
        else if (proxyState == LayerState.Fail)
        {
            overall = "proxy_bad";
            headline = string.Join("; ", proxyReasons);
        }
        else if (overseasState == LayerState.Fail && proxyState == LayerState.Ok)
        {
            overall = "direct_blocked_proxy_ok";
            headline = "direct overseas blocked, proxy works";
        }
        else if (new[] { wifiState, lanState, bbState, proxyState }.Any(s => s == LayerState.Degraded))
        {
            overall = "degraded";
            headline = layers
                .Where(l => l.State == "degraded" && l.Layer != "ai")
                .SelectMany(l => l.Reasons)
                .DefaultIfEmpty("some checks slow")
                .Aggregate((a, b) => $"{a}; {b}");
        }
        else
        {
            overall = "healthy";
            headline = "all green";
        }

        return new Verdict
        {
            Overall = overall,
            Headline = headline,
            Layers = layers,
            Ai = new AiVerdict
            {
                State = ToAiStateString(aiState),
                Headline = aiHeadline
            }
        };
    }

    private static bool ProxyOk(HttpsResult? h) => h is { Ok: true };

    private static bool DirectOk(HttpsResult? h) => h is { Ok: true };

    private static LayerState Worse(LayerState a, LayerState b)
    {
        var rank = new Dictionary<LayerState, int>
        {
            [LayerState.Ok] = 0,
            [LayerState.Degraded] = 1,
            [LayerState.Fail] = 2,
            [LayerState.Unknown] = 0,
            [LayerState.Skipped] = 0
        };
        return rank[b] > rank[a] ? b : a;
    }

    private static string ToStateString(LayerState state) => state switch
    {
        LayerState.Ok => "ok",
        LayerState.Degraded => "degraded",
        LayerState.Fail => "fail",
        LayerState.Skipped => "skipped",
        _ => "unknown"
    };

    private static string ToAiStateString(AiState state) => state switch
    {
        AiState.Ok => "ok",
        AiState.ProxyOnly => "proxy_only",
        AiState.DirectOnly => "direct_only",
        AiState.Degraded => "degraded",
        AiState.Fail => "fail",
        AiState.Skipped => "skipped",
        _ => "unknown"
    };

    private static string AiStateToLayerState(AiState state) => state switch
    {
        AiState.Ok or AiState.ProxyOnly or AiState.DirectOnly => "ok",
        AiState.Degraded => "degraded",
        AiState.Fail => "fail",
        AiState.Skipped => "skipped",
        _ => "unknown"
    };
}
