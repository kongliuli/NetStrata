using NetStrata.Core.Judge;
using NetStrata.Core.Models;

namespace NetStrata.Core.Tests.Judge;

public sealed class RouteWatchTests
{
    [Fact]
    public void Compare_GatewayChanged_EmitsAlert()
    {
        var prev = BaseSample() with { Iface = Iface(gateway: "192.168.1.1") };
        var curr = BaseSample() with { Iface = Iface(gateway: "192.168.0.1") };

        var alerts = RouteWatch.Compare(prev, curr);
        Assert.Contains(alerts, a => a.Type == "gateway_changed");
    }

    [Fact]
    public void Compare_EgressIpChanged_EmitsAlert()
    {
        var prev = BaseSample() with { ProxyEgress = Egress("1.2.3.4") };
        var curr = BaseSample() with { ProxyEgress = Egress("5.6.7.8") };

        var alerts = RouteWatch.Compare(prev, curr);
        Assert.Contains(alerts, a => a.Type == "egress_changed");
        Assert.Equal("1.2.3.4", alerts[0].Prev);
        Assert.Equal("5.6.7.8", alerts[0].Curr);
    }

    [Fact]
    public void Compare_NoChange_Empty()
    {
        var sample = BaseSample();
        Assert.Empty(RouteWatch.Compare(sample, sample with { T = "2026-07-09T05:00:00Z" }));
    }

    [Fact]
    public void DetectPatterns_ProxyDown_ThreeCycles()
    {
        var samples = Enumerable.Range(0, 3).Select(i => BaseSample() with
        {
            T = $"2026-07-09T04:00:{i:D2}Z",
            ProxyConfig = new ProxyConfig
            {
                ProxyUrl = "http://127.0.0.1:7890",
                ProxyPort = 7890,
                Listening = false
            }
        }).ToList();

        var alerts = RouteWatch.DetectPatterns(samples);
        Assert.Contains(alerts, a => a.Type == "proxy_down");
    }

    [Fact]
    public void DetectPatterns_EgressFlapping_FourChanges()
    {
        var samples = new List<Sample>
        {
            SampleAt("2026-07-09T04:00:00Z", "1.1.1.1"),
            SampleAt("2026-07-09T04:01:00Z", "2.2.2.2"),
            SampleAt("2026-07-09T04:02:00Z", "3.3.3.3"),
            SampleAt("2026-07-09T04:03:00Z", "4.4.4.4")
        };

        var alerts = RouteWatch.DetectPatterns(samples);
        Assert.Contains(alerts, a => a.Type == "egress_flapping");
    }

    private static Sample SampleAt(string t, string egressIp) => BaseSample() with
    {
        T = t,
        ProxyEgress = Egress(egressIp)
    };

    private static Sample BaseSample() => new()
    {
        T = "2026-07-09T04:00:00Z",
        CycleMs = 100,
        ProxyConfig = new ProxyConfig(),
        Iface = Iface()
    };

    private static InterfaceInfo Iface(string? gateway = "192.168.1.1", string? ipv4 = "192.168.1.100") => new()
    {
        Gateway = gateway,
        Ipv4 = ipv4,
        PrimaryDevice = "Ethernet",
        LinkType = "ethernet"
    };

    private static ProxyEgress Egress(string ip) => new() { Ok = true, Ip = ip, Ms = 100 };
}
