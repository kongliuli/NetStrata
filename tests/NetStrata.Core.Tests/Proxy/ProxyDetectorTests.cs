using NetStrata.Core.Models;
using NetStrata.Core.Proxy;

namespace NetStrata.Core.Tests.Proxy;

public sealed class ProxyDetectorTests
{
    [Fact]
    public void Detect_EnvOverride_Wins()
    {
        var detector = new ProxyDetector();
        var url = detector.Detect("http://127.0.0.1:9999");
        Assert.Equal("http://127.0.0.1:9999", url);
    }

    [Fact]
    public void Detect_None_Disables()
    {
        var detector = new ProxyDetector();
        Assert.Null(detector.Detect("__disabled__"));
    }

    [Fact]
    public void Detect_Registry_Fallback()
    {
        var reader = new FakeRegistryReader(new SystemProxySettings
        {
            HttpEnable = true,
            HttpProxy = "127.0.0.1",
            HttpPort = 7890
        });
        var detector = new ProxyDetector(reader);
        var url = detector.Detect(null);
        Assert.Equal("http://127.0.0.1:7890", url);
    }

    private sealed class FakeRegistryReader(SystemProxySettings settings) : ISystemProxyReader
    {
        public SystemProxySettings Read() => settings;
    }
}
