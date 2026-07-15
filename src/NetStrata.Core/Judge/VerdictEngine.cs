using NetStrata.Core.Config;
using NetStrata.Core.Models;
using NetStrata.Core.Probes;

namespace NetStrata.Core.Judge;

public sealed class VerdictEngine
{
    private readonly JudgeOptions _opts;
    private readonly HashSet<string> _domesticDirectLabels;

    public VerdictEngine(JudgeOptions? options = null)
    {
        _opts = options ?? JudgeOptions.Default;
        _domesticDirectLabels = new HashSet<string>(
            new[] { _opts.DomesticHttpsLabel, "taobao_direct" }
                .Concat(AiApiCatalog.Providers.Select(p => $"{p.Id}_direct")),
            StringComparer.OrdinalIgnoreCase);
    }

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
                if (w.Rssi <= _opts.WifiRssiFailDbm)
                {
                    wifiState = LayerState.Fail;
                    wifiReasons.Add($"weak signal {w.Rssi}dBm");
                }
                else if (w.Rssi <= _opts.WifiRssiDegradedDbm)
                {
                    wifiState = LayerState.Degraded;
                    wifiReasons.Add($"marginal signal {w.Rssi}dBm");
                }
            }

            if (w.TxRate is not null && w.TxRate < _opts.WifiTxRateDegradedMbps)
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
        else if ((gwPing.AvgMs ?? 0) > _opts.LanGatewayDegradedMs)
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
            var aliPing = sample.Pings.FirstOrDefault(p => p.Target == _opts.DomesticPingTarget);
            var baiduHttps = sample.Https.FirstOrDefault(h => h.Label == _opts.DomesticHttpsLabel);
            var baiduDns = sample.Dns.FirstOrDefault(d =>
                d.Domain == _opts.DomesticDnsDomain && d.Server == _opts.DomesticDnsServer);

            if (aliPing is null || !aliPing.Ok)
            {
                // ponytail: Windows ICMP blocked but HTTPS works → degraded, not fail
                if (baiduHttps is { Ok: true })
                {
                    bbState = Worse(bbState, LayerState.Degraded);
                    bbReasons.Add($"ping {_opts.DomesticPingTarget} fail (likely firewall), https ok");
                }
                else
                {
                    bbState = LayerState.Fail;
                    bbReasons.Add($"ping {_opts.DomesticPingTarget} fail");
                }
            }

            if (baiduDns is not null && !baiduDns.Ok)
            {
                if (aliPing is { Ok: true } && baiduHttps is { Ok: true })
                {
                    bbState = Worse(bbState, LayerState.Degraded);
                    bbReasons.Add($"dns_udp_blocked: dig {_opts.DomesticDnsServer} fail but ping/https ok");
                }
                else
                {
                    bbState = Worse(bbState, LayerState.Fail);
                    bbReasons.Add($"dig {_opts.DomesticDnsDomain} via {_opts.DomesticDnsServer} fail");
                }
            }

            if (baiduHttps is not null && !baiduHttps.Ok)
            {
                bbState = Worse(bbState, LayerState.Fail);
                bbReasons.Add($"{_opts.DomesticHttpsLabel} https fail: {baiduHttps.Err}");
            }
            else if (baiduHttps is not null && baiduHttps.TotalMs > _opts.BroadbandHttpsDegradedMs)
            {
                bbState = Worse(bbState, LayerState.Degraded);
                bbReasons.Add($"{_opts.DomesticHttpsLabel} slow {baiduHttps.TotalMs:F0}ms");
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
                .Where(h => h.Via == "direct" && !_domesticDirectLabels.Contains(h.Label))
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
        var providerCount = AiApiCatalog.Providers.Length;
        var directHits = 0;
        var proxyHits = 0;
        var metrics = new Dictionary<string, object?>();
        foreach (var p in AiApiCatalog.Providers)
        {
            var d = sample.Https.FirstOrDefault(h => h.Label == $"{p.Id}_direct");
            var pr = sample.Https.FirstOrDefault(h => h.Label == $"{p.Id}_proxy");
            var dOk = DirectOk(d);
            var pOk = ProxyOk(pr);
            if (dOk) directHits++;
            if (pOk) proxyHits++;
            metrics[$"{p.Id}DirectOk"] = dOk;
            metrics[$"{p.Id}ProxyOk"] = pOk;
            if (!dOk && d?.Err is { } derr)
                aiReasons.Add($"{p.DisplayName} direct: {derr}");
            if (!pOk && pr?.Err is { } perr)
                aiReasons.Add($"{p.DisplayName} proxy: {perr}");
        }

        var noProxy = string.IsNullOrEmpty(sample.ProxyConfig.ProxyUrl);
        AiState aiState;
        string aiHeadline;

        if (noProxy)
        {
            if (directHits == providerCount)
            {
                aiState = AiState.DirectOnly;
                aiHeadline = $"all {providerCount} AI APIs reachable direct (no proxy)";
            }
            else if (directHits > 0)
            {
                aiState = AiState.Degraded;
                aiHeadline = $"only {directHits}/{providerCount} AI APIs reachable direct (no proxy)";
            }
            else
            {
                aiState = AiState.Fail;
                aiHeadline = "all AI APIs unreachable direct, and no proxy configured";
            }
        }
        else if (proxyState == LayerState.Fail && directHits == 0)
        {
            aiState = AiState.Skipped;
            aiHeadline = "proxy down and direct also fail — cannot judge AI";
        }
        else if (proxyHits == providerCount && directHits >= 1)
        {
            aiState = AiState.Ok;
            aiHeadline = $"all AI APIs reachable via proxy (direct {directHits}/{providerCount})";
        }
        else if (proxyHits == providerCount)
        {
            aiState = AiState.ProxyOnly;
            aiHeadline = "all AI APIs reachable via proxy; direct blocked";
        }
        else if (proxyHits > 0 && proxyHits < providerCount)
        {
            aiState = AiState.Degraded;
            aiHeadline = $"only {proxyHits}/{providerCount} AI APIs reachable via proxy";
        }
        else if (directHits > 0)
        {
            aiState = AiState.DirectOnly;
            aiHeadline = "proxy path fail but some direct OK — proxy app issue";
        }
        else
        {
            aiState = AiState.Fail;
            aiHeadline = "all AI APIs unreachable (proxy and direct fail)";
        }

        layers.Add(new LayerVerdict
        {
            Layer = "ai",
            State = AiStateToLayerState(aiState),
            Reasons = aiReasons.Take(6).ToList(),
            Metrics = metrics
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

        var insights = new List<string>();
        if (bbReasons.Any(r => r.StartsWith("dns_udp_blocked", StringComparison.Ordinal)))
            insights.Add("dns_path_blocked: public DNS UDP 53 likely filtered; HTTP/proxy may still work");

        return new Verdict
        {
            Overall = overall,
            Headline = headline,
            Insights = insights,
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
