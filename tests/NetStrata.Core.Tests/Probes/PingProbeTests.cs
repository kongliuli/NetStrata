using System.Net.NetworkInformation;
using NetStrata.Core.Probes;

namespace NetStrata.Core.Tests.Probes;

public sealed class PingProbeTests
{
    [Fact]
    public async Task Ping_ValidTarget_ReturnsStats()
    {
        var mock = new MockPingSender(_ => new PingSendResult(IPStatus.Success, 5));
        var probe = new PingProbe(mock);
        var result = await probe.PingTargetAsync("223.5.5.5");

        Assert.True(result.Ok);
        Assert.Equal(0, result.LossPct);
        Assert.NotNull(result.AvgMs);
    }

    [Fact]
    public async Task Ping_CustomTarget_MarkedCustom()
    {
        var mock = new MockPingSender(_ => new PingSendResult(IPStatus.Success, 1));
        var probe = new PingProbe(mock, ["192.168.1.50"]);
        var result = await probe.PingTargetAsync("192.168.1.50", custom: true, label: "nas");

        Assert.True(result.Custom);
        Assert.Equal("nas", result.Label);
    }

    private sealed class MockPingSender(Func<string, PingSendResult> factory) : IPingSender
    {
        public Task<PingSendResult> SendAsync(string target, int timeoutMs, CancellationToken ct) =>
            Task.FromResult(factory(target));
    }
}
