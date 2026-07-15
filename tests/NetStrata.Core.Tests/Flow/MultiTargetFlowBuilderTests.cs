using NetStrata.Core.Flow;
using NetStrata.Core.Models;

namespace NetStrata.Core.Tests.Flow;

public sealed class MultiTargetFlowBuilderTests
{
    [Fact]
    public void FromState_OneBlockPerTarget_WithOwnEgressPath()
    {
        var state = State(Sample(
            Https("openai_direct", "direct", false, 400),
            Https("openai_proxy", "proxy", true, 180),
            Https("github_direct", "direct", true, 90)));

        var blocks = MultiTargetFlowBuilder.FromState(state, "zh");

        Assert.Equal(2, blocks.Count);
        var openai = blocks.Single(b => b.Id == "tgt:openai");
        Assert.Contains("代理", openai.EgressLabel);
        Assert.Contains(openai.Trace.Nodes, n => n.Id == "proxy");
        Assert.DoesNotContain(openai.Trace.Nodes, n => n.Id == "direct");
        Assert.Equal(["wifi", "lan", "broadband", "proxy", "tgt:openai"],
            openai.Trace.Nodes.Select(n => n.Id).ToArray());

        var github = blocks.Single(b => b.Id == "tgt:github");
        Assert.Contains("直连", github.EgressLabel);
        Assert.Contains(github.Trace.Nodes, n => n.Id == "direct");
        Assert.DoesNotContain(github.Trace.Nodes, n => n.Id == "proxy");
    }

    [Fact]
    public void BuildTargets_PicksWorkingPathPerMonitor()
    {
        var sample = Sample(
            Https("openai_direct", "direct", false, 400),
            Https("openai_proxy", "proxy", true, 180),
            Https("cursor_direct", "direct", true, 120),
            Https("cursor_proxy", "proxy", true, 200));

        var targets = MultiTargetFlowBuilder.BuildTargets(sample, "zh");

        Assert.Equal(2, targets.Count);
        Assert.Equal("proxy", targets.Single(t => t.Id == "tgt:openai").Lane);
        Assert.Equal("direct", targets.Single(t => t.Id == "tgt:cursor").Lane);
    }

    [Fact]
    public void FromState_SortsFailuresBeforeProxyBeforeOk()
    {
        var state = State(Sample(
            Https("zzz_ok_direct", "direct", true, 50),
            Https("aaa_fail_direct", "direct", false, 0),
            Https("mmm_proxy_direct", "direct", false, 0),
            Https("mmm_proxy_proxy", "proxy", true, 120)));

        var ids = MultiTargetFlowBuilder.FromState(state, "zh").Select(b => b.Id).ToList();

        Assert.Equal("tgt:aaa_fail", ids[0]);
        Assert.Equal("tgt:mmm_proxy", ids[1]);
        Assert.Equal("tgt:zzz_ok", ids[2]);
    }

    [Fact]
    public void Fingerprint_ChangesWhenOutcomeChanges()
    {
        var a = MultiTargetFlowBuilder.FromState(
            State(Sample(Https("x_direct", "direct", true, 10))), "zh").Single();
        var b = MultiTargetFlowBuilder.FromState(
            State(Sample(Https("x_direct", "direct", false, 10))), "zh").Single();
        Assert.NotEqual(a.Fingerprint, b.Fingerprint);
    }

    private static DaemonState State(Sample sample) => new()
    {
        StartedAt = "2026-07-15T08:00:00Z",
        Cycle = 2,
        Latest = sample
    };

    private static Sample Sample(params HttpsResult[] https) => new()
    {
        T = "2026-07-15T08:00:00Z",
        ProxyConfig = new ProxyConfig { ProxyUrl = "http://127.0.0.1:7890" },
        Https = https,
        Verdict = new Verdict
        {
            Overall = "direct_blocked_proxy_ok",
            Headline = "ok",
            Layers =
            [
                new() { Layer = "wifi", State = "ok" },
                new() { Layer = "lan", State = "ok" },
                new() { Layer = "broadband", State = "ok" },
                new() { Layer = "overseas_direct", State = "fail" },
                new() { Layer = "proxy", State = "ok" },
                new() { Layer = "ai", State = "ok" }
            ],
            Ai = new AiVerdict { State = "proxy_only", Headline = "" }
        }
    };

    private static HttpsResult Https(string label, string via, bool ok, double totalMs) => new()
    {
        Label = label,
        Url = $"https://{label}.example/",
        Via = via,
        Ok = ok,
        HttpCode = ok ? 200 : 0,
        TotalMs = totalMs,
        Err = ok ? null : "blocked"
    };
}
