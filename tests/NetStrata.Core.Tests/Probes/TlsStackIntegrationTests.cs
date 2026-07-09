using NetStrata.Core.Probes;

namespace NetStrata.Core.Tests.Probes;

[Trait("Category", "Integration")]
public sealed class TlsStackIntegrationTests
{
    [Fact]
    public async Task Probe_RealDnsAndTcp_Github()
    {
        var probe = new TlsStackProbe();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var result = await probe.ProbeTargetAsync("stack_github", "github.com", 443, cts.Token);

        Assert.NotNull(result.Dns);
        Assert.True(result.Dns.Ok, result.Dns.Err);
        Assert.NotEqual("dns_fail", result.Verdict);
    }
}
