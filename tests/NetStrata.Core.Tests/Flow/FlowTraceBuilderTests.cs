using NetStrata.Core.Flow;
using NetStrata.Core.Models;

namespace NetStrata.Core.Tests.Flow;

public sealed class FlowTraceBuilderTests
{
    [Fact]
    public void FromState_MapsSixDiagnosticLayers()
    {
        var state = State(new Sample
        {
            T = "2026-07-15T08:00:00Z",
            ProxyConfig = new ProxyConfig(),
            Verdict = Verdict(
                new LayerVerdict { Layer = "wifi", State = "ok" },
                new LayerVerdict { Layer = "proxy", State = "degraded", Reasons = ["proxy_bad"] })
        });

        var trace = FlowTraceBuilder.FromState(state, "zh").Single(x => x.Mode == FlowTraceMode.Layers);

        Assert.True(trace.HasData);
        Assert.Equal(6, trace.Nodes.Count);
        Assert.Equal(FlowNodeState.Passed, trace.Nodes.Single(x => x.Id == "wifi").State);
        Assert.Equal(FlowNodeState.Degraded, trace.Nodes.Single(x => x.Id == "proxy").State);
        Assert.Contains("非逐包抓取", trace.Disclosure);
    }

    [Fact]
    public void FromState_ComparesDirectAndProxyResults()
    {
        var state = State(new Sample
        {
            T = "2026-07-15T08:00:00Z",
            ProxyConfig = new ProxyConfig(),
            Https =
            [
                Https("openai_direct", "direct", false, 420, "blocked"),
                Https("openai_proxy", "proxy", true, 178)
            ]
        });

        var trace = FlowTraceBuilder.FromState(state, "zh").Single(x => x.Mode == FlowTraceMode.Routes);

        Assert.True(trace.HasData);
        Assert.Equal(FlowNodeState.Failed, trace.Nodes.Single(x => x.Id == "direct").State);
        Assert.Equal(FlowNodeState.Passed, trace.Nodes.Single(x => x.Id == "proxy").State);
        Assert.Equal(FlowNodeState.Passed, trace.Nodes.Single(x => x.Id == "target").State);
        Assert.Equal(178, trace.Nodes.Single(x => x.Id == "proxy").DurationMs);
    }

    [Fact]
    public void FromState_StopsTlsTraceAfterFailedHandshake()
    {
        var state = State(new Sample
        {
            T = "2026-07-15T08:00:00Z",
            ProxyConfig = new ProxyConfig(),
            TlsStack =
            [
                new TlsStackResult
                {
                    Label = "OpenAI",
                    Host = "api.openai.com",
                    Dns = new TlsStackLayerResult { Ok = true, Ms = 24 },
                    Tcp = new TlsStackLayerResult { Ok = true, Ms = 61 },
                    Tls = new TlsStackLayerResult { Ok = false, Ms = 214, Err = "handshake failed" },
                    Verdict = "fail",
                    StoppedAt = "tls"
                }
            ]
        });

        var trace = FlowTraceBuilder.FromState(state, "zh").Single(x => x.Mode == FlowTraceMode.Tls);

        Assert.True(trace.HasData);
        Assert.Equal(FlowNodeState.Failed, trace.Nodes.Single(x => x.Id == "tls").State);
        Assert.Equal(FlowNodeState.Skipped, trace.Nodes.Single(x => x.Id == "http").State);
        Assert.Null(trace.Nodes.Single(x => x.Id == "http").DurationMs);
    }

    private static DaemonState State(Sample sample) => new()
    {
        StartedAt = "2026-07-15T08:00:00Z",
        Cycle = 3,
        Latest = sample
    };

    private static Verdict Verdict(params LayerVerdict[] layers) => new()
    {
        Overall = "degraded",
        Headline = "diagnostic result",
        Layers = layers,
        Ai = new AiVerdict { State = "unknown", Headline = "" }
    };

    private static HttpsResult Https(
        string label,
        string via,
        bool ok,
        double totalMs,
        string? error = null) => new()
    {
        Label = label,
        Url = "https://api.openai.com/",
        Via = via,
        Ok = ok,
        HttpCode = ok ? 200 : 0,
        TotalMs = totalMs,
        Err = error
    };
}
