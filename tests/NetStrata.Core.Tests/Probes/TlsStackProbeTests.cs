using NetStrata.Core.Models;
using NetStrata.Core.Probes;

namespace NetStrata.Core.Tests.Probes;

public sealed class TlsStackProbeTests
{
    [Fact]
    public async Task Probe_DnsFail_StopsAtDns()
    {
        var probe = new TlsStackProbe(new FakeBackend
        {
            Dns = Fail("nxdomain"),
            Tcp = Ok(),
            Tls = Ok(),
            Http = Ok()
        });

        var result = await probe.ProbeTargetAsync("stack_test", "bad.example", 443);
        Assert.Equal("dns_fail", result.Verdict);
        Assert.Equal("dns", result.StoppedAt);
        Assert.NotNull(result.Dns);
        Assert.Null(result.Tcp);
        Assert.Null(result.Tls);
        Assert.Null(result.Http);
    }

    [Fact]
    public async Task Probe_TcpOk_TlsReset_TlsBlock()
    {
        var probe = new TlsStackProbe(new FakeBackend
        {
            Dns = Ok(ips: ["1.2.3.4"]),
            Tcp = Ok(),
            Tls = Fail("reset"),
            Http = Ok()
        });

        var result = await probe.ProbeTargetAsync("stack_github", "github.com", 443);
        Assert.Equal("tls_block", result.Verdict);
        Assert.Equal("tls", result.StoppedAt);
        Assert.True(result.Tcp?.Ok);
        Assert.False(result.Tls?.Ok);
        Assert.Null(result.Http);
    }

    [Fact]
    public async Task Probe_FullStack_Ok()
    {
        var probe = new TlsStackProbe(new FakeBackend
        {
            Dns = Ok(ips: ["140.82.121.4"]),
            Tcp = Ok(),
            Tls = Ok(),
            Http = Ok(httpCode: 200)
        });

        var result = await probe.ProbeTargetAsync("stack_github", "github.com", 443);
        Assert.Equal("ok", result.Verdict);
        Assert.Null(result.StoppedAt);
        Assert.True(result.Dns?.Ok);
        Assert.True(result.Tcp?.Ok);
        Assert.True(result.Tls?.Ok);
        Assert.True(result.Http?.Ok);
    }

    [Fact]
    public void ToInsight_TlsBlock_ContainsHost()
    {
        var result = new TlsStackResult
        {
            Label = "stack_github",
            Host = "github.com",
            Port = 443,
            Dns = Ok(),
            Tcp = Ok(),
            Tls = Fail("reset"),
            Verdict = "tls_block",
            StoppedAt = "tls"
        };
        var insight = TlsStackEvaluator.ToInsight(result);
        Assert.Contains("tls_block:github.com", insight);
    }

    private static TlsStackLayerResult Ok(double ms = 1, string[]? ips = null, int? httpCode = null) => new()
    {
        Ok = true,
        Ms = ms,
        Ips = ips,
        HttpCode = httpCode
    };

    private static TlsStackLayerResult Fail(string err) => new() { Ok = false, Ms = 1, Err = err };

    private sealed class FakeBackend : ITlsStackBackend
    {
        public required TlsStackLayerResult Dns { get; init; }
        public required TlsStackLayerResult Tcp { get; init; }
        public required TlsStackLayerResult Tls { get; init; }
        public required TlsStackLayerResult Http { get; init; }

        public Task<TlsStackLayerResult> ProbeDnsAsync(string host, CancellationToken ct) =>
            Task.FromResult(Dns);

        public Task<TlsStackLayerResult> ProbeTcpAsync(string host, int port, CancellationToken ct) =>
            Task.FromResult(Tcp);

        public Task<TlsStackLayerResult> ProbeTlsAsync(string host, int port, CancellationToken ct) =>
            Task.FromResult(Tls);

        public Task<TlsStackLayerResult> ProbeHttpAsync(string host, int port, CancellationToken ct) =>
            Task.FromResult(Http);
    }
}
