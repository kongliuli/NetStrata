using NetStrata.Core.Judge;
using NetStrata.Core.Models;
using NetStrata.Core.Storage;

namespace NetStrata.Core.Tests.Storage;

public sealed class JsonSampleStorageTests
{
    [Fact]
    public async Task Append_WritesJsonlLine()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NetStrataTest", Guid.NewGuid().ToString());
        var storage = new JsonSampleStorage(dir);
        var sample = new Sample
        {
            T = "2026-07-09T04:00:00Z",
            ProxyConfig = new ProxyConfig(),
            Verdict = new VerdictEngine().Judge(SampleFixture.Minimal())
        };

        await storage.AppendAsync(sample, CancellationToken.None);
        var lines = await File.ReadAllLinesAsync(Path.Combine(dir, "samples.jsonl"));
        Assert.Single(lines);
        Assert.Contains("2026-07-09", lines[0]);
    }
}

internal static class SampleFixture
{
    public static Sample Minimal() => new()
    {
        T = "2026-07-09T04:00:00Z",
        Iface = new InterfaceInfo { LinkType = "ethernet", Gateway = "192.168.1.1" },
        Pings = [new PingResult { Target = "192.168.1.1", Ok = true, AvgMs = 1 }],
        Https = [new HttpsResult { Label = "baidu_direct", Url = "https://www.baidu.com", Via = "direct", Ok = true, HttpCode = 200 }],
        Dns = [new DnsResult { Server = "223.5.5.5", Domain = "baidu.com", Ok = true }],
        ProxyConfig = new ProxyConfig()
    };
}
