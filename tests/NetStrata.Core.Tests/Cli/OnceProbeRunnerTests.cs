using NetStrata.Core.Cli;
using NetStrata.Core.Collector;
using NetStrata.Core.Config;
using NetStrata.Core.Models;

namespace NetStrata.Core.Tests.Cli;

public sealed class OnceProbeRunnerTests
{
    [Fact]
    public async Task RunAsync_UsesCollectorAndReturnsVerdict()
    {
        CollectOptions? seen = null;
        var sample = new Sample
        {
            T = "2026-01-01T00:00:00Z",
            CycleMs = 12,
            ProxyConfig = new ProxyConfig(),
            Verdict = new Verdict
            {
                Overall = "healthy",
                Headline = "all green",
                Layers = [],
                Ai = new AiVerdict { State = "ok", Headline = "ok" }
            }
        };

        var runner = new OnceProbeRunner(
            new FakeCollector((opts, _) =>
            {
                seen = opts;
                return Task.FromResult(sample);
            }),
            () => new NetStrataOptions
            {
                PingExtra = ["1.1.1.1"],
                ProxyOverride = "http://127.0.0.1:7890"
            });

        var result = await runner.RunAsync();
        Assert.True(result.Ok);
        Assert.Equal("healthy", result.Overall);
        Assert.Equal("all green", result.Headline);
        Assert.NotNull(result.RawJson);
        Assert.Contains("healthy", result.RawJson);
        Assert.Equal(["1.1.1.1"], seen!.PingExtra);
        Assert.Equal("http://127.0.0.1:7890", seen.ProxyOverride);
    }

    [Fact]
    public async Task RunAsync_CollectorThrows_ReturnsError()
    {
        var runner = new OnceProbeRunner(
            new FakeCollector((_, _) => throw new InvalidOperationException("boom")),
            () => new NetStrataOptions());

        var result = await runner.RunAsync();
        Assert.False(result.Ok);
        Assert.Equal("boom", result.Error);
    }

    [Fact]
    public void Parser_ReadsVerdict()
    {
        const string json = """
            {"verdict":{"overall":"degraded","headline":"proxy slow","layers":[],"ai":{"state":"ok","headline":"ok"}}}
            """;
        var parsed = OnceProbeParser.Parse(json);
        Assert.True(parsed.Ok);
        Assert.Equal("degraded", parsed.Overall);
    }

    private sealed class FakeCollector(Func<CollectOptions?, CancellationToken, Task<Sample>> impl) : ISampleCollector
    {
        public Task<Sample> CollectAsync(CollectOptions? options = null, CancellationToken ct = default) =>
            impl(options, ct);
    }
}
