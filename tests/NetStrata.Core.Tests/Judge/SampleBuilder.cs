using NetStrata.Core.Models;
using NetStrata.Core.Probes;

namespace NetStrata.Core.Tests.Judge;

internal static class SampleBuilder
{
    public static Sample Healthy() => new()
    {
        T = "2026-07-09T04:00:00Z",
        CycleMs = 100,
        Wifi = new WifiInfo
        {
            Status = "connected",
            Rssi = -55,
            TxRate = 866,
            Ssid = "TestNet"
        },
        Iface = new InterfaceInfo
        {
            LinkType = "wifi",
            Gateway = "192.168.1.1",
            Ipv4 = "192.168.1.100"
        },
        Pings =
        [
            Ping("192.168.1.1", true, 2),
            Ping("223.5.5.5", true, 5),
            Ping("119.29.29.29", true, 6),
            Ping("1.1.1.1", true, 20),
            Ping("8.8.8.8", true, 22)
        ],
        Dns =
        [
            new DnsResult { Server = "223.5.5.5", Domain = "baidu.com", Ok = true, Ms = 10 }
        ],
        Https =
        [
            Https("baidu_direct", "direct", true),
            Https("taobao_direct", "direct", true),
            Https("google_direct", "direct", true),
            Https("cloudflare_direct", "direct", true),
            Https("github_site_direct", "direct", true),
            ..AiApiCatalog.Providers.Select(p => Https($"{p.Id}_direct", "direct", true))
        ],
        ProxyConfig = new ProxyConfig()
    };

    public static Sample WithProxy()
    {
        var baseSample = Healthy();
        return baseSample with
        {
            Https = baseSample.Https
                .Concat(
                [
                    Https("google_direct", "direct", false),
                    Https("cloudflare_direct", "direct", false),
                    Https("github_site_direct", "direct", false),
                    Https("google_proxy", "proxy", true),
                    Https("cloudflare_proxy", "proxy", true),
                    Https("github_site_proxy", "proxy", true),
                    ..AiApiCatalog.Providers.Select(p => Https($"{p.Id}_direct", "direct", false)),
                    ..AiApiCatalog.Providers.Select(p => Https($"{p.Id}_proxy", "proxy", true))
                ])
                .GroupBy(h => h.Label)
                .Select(g => g.Last())
                .ToList(),
            ProxyConfig = new ProxyConfig
            {
                ProxyUrl = "http://127.0.0.1:7890",
                ProxyPort = 7890,
                Listening = true,
                ListenerProcess = "verge-mihomo"
            },
            ProxyEgress = new ProxyEgress { Ok = true, Ip = "1.2.3.4", Ms = 500 }
        };
    }

    private static PingResult Ping(string target, bool ok, double avgMs) => new()
    {
        Target = target,
        Ok = ok,
        LossPct = ok ? 0 : 100,
        Sent = 3,
        Received = ok ? 3 : 0,
        AvgMs = avgMs
    };

    private static HttpsResult Https(string label, string via, bool ok) => new()
    {
        Label = label,
        Url = $"https://example.com/{label}",
        Via = via,
        Ok = ok,
        HttpCode = ok ? 200 : 0,
        TotalMs = 100
    };
}
