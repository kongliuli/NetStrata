using NetStrata.Core.Judge;
using NetStrata.Core.Models;

namespace NetStrata.Core.Tests.Judge;

public sealed class VerdictEngineTests
{
    private readonly VerdictEngine _engine = new();

    [Fact]
    public void Judge_AllGreen_ReturnsHealthy()
    {
        var sample = SampleBuilder.Healthy();
        var verdict = _engine.Judge(sample);

        Assert.Equal("healthy", verdict.Overall);
        Assert.Equal("all green", verdict.Headline);
        Assert.All(verdict.Layers.Where(l => l.Layer is not "ai"), l =>
            Assert.True(l.State is "ok" or "skipped" or "fail"));
    }

    [Fact]
    public void Judge_Ethernet_SkipsWifi()
    {
        var sample = SampleBuilder.Healthy() with
        {
            Iface = SampleBuilder.Healthy().Iface! with { LinkType = "ethernet", HardwarePort = "Realtek" }
        };

        var wifi = _engine.Judge(sample).Layers.Single(l => l.Layer == "wifi");
        Assert.Equal("skipped", wifi.State);
        Assert.Contains("ethernet", wifi.Reasons[0]);
    }

    [Fact]
    public void Judge_GatewayPingFail_LanBad()
    {
        var baseSample = SampleBuilder.Healthy();
        var sample = baseSample with
        {
            Pings = baseSample.Pings
                .Select(p => p.Target == baseSample.Iface!.Gateway
                    ? p with { Ok = false, LossPct = 100 }
                    : p)
                .ToList()
        };

        var verdict = _engine.Judge(sample);
        Assert.Equal("lan_bad", verdict.Overall);
        Assert.Equal("fail", verdict.Layers.Single(l => l.Layer == "lan").State);
    }

    [Fact]
    public void Judge_AliPingFail_BroadbandBad()
    {
        var baseSample = SampleBuilder.Healthy();
        var sample = baseSample with
        {
            Pings = baseSample.Pings
                .Select(p => p.Target == "223.5.5.5" ? p with { Ok = false, LossPct = 100 } : p)
                .ToList(),
            Https = baseSample.Https
                .Select(h => h.Label == "baidu_direct" ? h with { Ok = false } : h)
                .ToList()
        };

        var verdict = _engine.Judge(sample);
        Assert.Equal("broadband_bad", verdict.Overall);
    }

    [Fact]
    public void Judge_DnsFail_PingOk_HttpsOk_DegradedWithInsight()
    {
        var baseSample = SampleBuilder.Healthy();
        var sample = baseSample with
        {
            Dns =
            [
                new DnsResult { Server = "223.5.5.5", Domain = "baidu.com", Ok = false, Ms = 1, Err = "timeout" }
            ]
        };

        var verdict = _engine.Judge(sample);
        Assert.Equal("degraded", verdict.Overall);
        var broadband = verdict.Layers.Single(l => l.Layer == "broadband");
        Assert.Equal("degraded", broadband.State);
        Assert.Contains(broadband.Reasons, r => r.StartsWith("dns_udp_blocked"));
        Assert.Contains(verdict.Insights, i => i.Contains("dns_path_blocked"));
    }

    [Fact]
    public void Judge_OverseasFail_ProxyOk_DirectBlocked()
    {
        var sample = SampleBuilder.WithProxy();
        var verdict = _engine.Judge(sample);

        Assert.Equal("direct_blocked_proxy_ok", verdict.Overall);
        Assert.Equal("fail", verdict.Layers.Single(l => l.Layer == "overseas_direct").State);
        Assert.Equal("ok", verdict.Layers.Single(l => l.Layer == "proxy").State);
    }

    [Fact]
    public void Judge_NoProxy_SkipsProxyLayer()
    {
        var sample = SampleBuilder.Healthy();
        var proxy = _engine.Judge(sample).Layers.Single(l => l.Layer == "proxy");
        Assert.Equal("skipped", proxy.State);
    }

    [Fact]
    public void Judge_NoProxy_AiDirectOnly()
    {
        var verdict = _engine.Judge(SampleBuilder.Healthy());
        Assert.Equal("direct_only", verdict.Ai.State);
        Assert.Contains("直连可达", verdict.Ai.Headline);
    }

    [Fact]
    public void Judge_WithProxy_AllAiProxy_ProxyOnly()
    {
        var verdict = _engine.Judge(SampleBuilder.WithProxy());
        Assert.Equal("proxy_only", verdict.Ai.State);
    }

    [Fact]
    public void Judge_ProxyNotListening_ProxyBad()
    {
        var sample = SampleBuilder.WithProxy() with
        {
            ProxyConfig = SampleBuilder.WithProxy().ProxyConfig with { Listening = false }
        };

        var verdict = _engine.Judge(sample);
        Assert.Equal("proxy_bad", verdict.Overall);
    }

    [Fact]
    public void Judge_BaiduSlow_Degraded()
    {
        var baseSample = SampleBuilder.Healthy();
        var sample = baseSample with
        {
            Https = baseSample.Https
                .Select(h => h.Label == "baidu_direct" ? h with { TotalMs = 2000 } : h)
                .ToList()
        };

        var verdict = _engine.Judge(sample);
        Assert.Equal("degraded", verdict.Overall);
        Assert.Equal("degraded", verdict.Layers.Single(l => l.Layer == "broadband").State);
    }

    [Fact]
    public void Judge_PingFail_HttpsOk_BroadbandDegraded()
    {
        var baseSample = SampleBuilder.Healthy();
        var sample = baseSample with
        {
            Pings = baseSample.Pings
                .Select(p => p.Target == "223.5.5.5" ? p with { Ok = false, LossPct = 100 } : p)
                .ToList()
        };

        var verdict = _engine.Judge(sample);
        Assert.Equal("degraded", verdict.Layers.Single(l => l.Layer == "broadband").State);
        Assert.Contains("firewall", verdict.Layers.Single(l => l.Layer == "broadband").Reasons[0]);
    }

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
