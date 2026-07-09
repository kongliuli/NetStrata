using NetStrata.Core.Probes;

namespace NetStrata.Core.Tests.Probes;

public sealed class TailscaleProbeTests
{
    [Theory]
    [InlineData("100.64.0.1", true)]
    [InlineData("100.127.255.254", true)]
    [InlineData("100.63.0.1", false)]
    [InlineData("192.168.1.1", false)]
    public void IsCgNat10064_ClassifiesRange(string ip, bool expected) =>
        Assert.Equal(expected, TailscaleAddressFinder.IsCgNat10064(ip));

    [Fact]
    public void ParseExitNodeActive_DetectsUsingExitNode()
    {
        const string json = """{"UsingExitNode":true,"TailscaleIPs":["100.64.0.2"]}""";
        Assert.True(TailscaleStatusParser.ParseExitNodeActive(json));
    }

    [Fact]
    public void ParseExitNodeActive_DetectsExitNodeStatusOnline()
    {
        const string json = """{"ExitNodeStatus":{"Online":true}}""";
        Assert.True(TailscaleStatusParser.ParseExitNodeActive(json));
    }

    [Fact]
    public async Task Probe_NotInstalled_ReturnsNull()
    {
        var probe = new TailscaleProbe(
            new FakeInstalledChecker(false),
            new FakeStatusReader(null),
            new FakeAddressFinder(null));
        var result = await probe.ProbeAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task Probe_InstalledWithAddress_SetsSignedIn()
    {
        var probe = new TailscaleProbe(
            new FakeInstalledChecker(true),
            new FakeStatusReader("""{"UsingExitNode":false}"""),
            new FakeAddressFinder("100.64.0.5"));

        var result = await probe.ProbeAsync();
        Assert.NotNull(result);
        Assert.True(result!.Installed);
        Assert.True(result.SignedIn);
        Assert.Equal("100.64.0.5", result.Address);
        Assert.False(result.ExitNodeActive);
    }

    [Fact]
    public async Task Probe_ExitNodeFromStatusJson()
    {
        var probe = new TailscaleProbe(
            new FakeInstalledChecker(true),
            new FakeStatusReader("""{"UsingExitNode":true}"""),
            new FakeAddressFinder("100.64.0.8"));
        var result = await probe.ProbeAsync();
        Assert.NotNull(result);
        Assert.True(result!.ExitNodeActive);
    }

    private sealed class FakeInstalledChecker(bool installed) : ITailscaleInstalledChecker
    {
        public bool IsInstalled() => installed;
    }

    private sealed class FakeStatusReader(string? json) : ITailscaleStatusReader
    {
        public string? ReadStatusJson() => json;
    }

    private sealed class FakeAddressFinder(string? address) : ITailscaleAddressFinder
    {
        public string? FindAddress() => address;
    }
}
