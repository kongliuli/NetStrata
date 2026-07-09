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
                .ToList()
        };

        var verdict = _engine.Judge(sample);
        Assert.Equal("broadband_bad", verdict.Overall);
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
        var sample = SampleBuilder.Healthy() with
        {
            Https = SampleBuilder.Healthy().Https.Concat(
            [
                Https("anthropic_direct", "direct", true),
                Https("openai_direct", "direct", true)
            ]).ToList()
        };

        var verdict = _engine.Judge(sample);
        Assert.Equal("direct_only", verdict.Ai.State);
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
