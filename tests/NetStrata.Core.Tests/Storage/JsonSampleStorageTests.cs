using NetStrata.Core.Judge;
using NetStrata.Core.Models;
using NetStrata.Core.Storage;

namespace NetStrata.Core.Tests.Storage;

public sealed class JsonSampleStorageTests
{
    [Fact]
    public async Task Append_WritesDatedJsonlLine()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NetStrataTest", Guid.NewGuid().ToString());
        var storage = new JsonSampleStorage(dir);
        var sample = new Sample
        {
            T = "2026-07-09T04:00:00Z",
            Trigger = "manual",
            ProxyConfig = new ProxyConfig(),
            Verdict = new VerdictEngine().Judge(SampleFixture.Minimal())
        };

        await storage.AppendAsync(sample, CancellationToken.None);
        var today = JsonSampleStorage.DayFileName(DateTime.UtcNow);
        var lines = await File.ReadAllLinesAsync(Path.Combine(dir, today));
        Assert.Single(lines);
        Assert.Contains("2026-07-09", lines[0]);
        Assert.Contains("\"trigger\":\"manual\"", lines[0]);
    }

    [Fact]
    public async Task ReadTail_ReadsFromEndAcrossDays()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NetStrataTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var d1 = DateTime.UtcNow.Date.AddDays(-1);
        var d2 = DateTime.UtcNow.Date;
        var day1 = Path.Combine(dir, JsonSampleStorage.DayFileName(d1));
        var day2 = Path.Combine(dir, JsonSampleStorage.DayFileName(d2));
        var t1a = d1.AddHours(1).ToString("o");
        var t1b = d1.AddHours(2).ToString("o");
        var t2a = d2.AddHours(1).ToString("o");
        var t2b = d2.AddHours(2).ToString("o");
        await File.WriteAllTextAsync(day1,
            $"{{\"t\":\"{t1a}\",\"trigger\":\"daemon\",\"proxyConfig\":{{}}}}\n" +
            $"{{\"t\":\"{t1b}\",\"trigger\":\"daemon\",\"proxyConfig\":{{}}}}\n");
        await File.WriteAllTextAsync(day2,
            $"{{\"t\":\"{t2a}\",\"trigger\":\"manual\",\"proxyConfig\":{{}}}}\n" +
            $"{{\"t\":\"{t2b}\",\"trigger\":\"daemon\",\"proxyConfig\":{{}}}}\n");

        var storage = new JsonSampleStorage(dir);
        var tail = await storage.ReadTailAsync(3, CancellationToken.None);
        Assert.Equal(3, tail.Count);
        Assert.Equal(t1b, tail[0].T);
        Assert.Equal(t2b, tail[^1].T);
    }

    [Fact]
    public async Task Purge_RemovesFilesOlderThanRetention()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NetStrataTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var oldDay = DateTime.UtcNow.Date.AddDays(-(JsonSampleStorage.RetentionDays + 5));
        var oldFile = Path.Combine(dir, JsonSampleStorage.DayFileName(oldDay));
        await File.WriteAllTextAsync(oldFile, """{"t":"old","proxyConfig":{}}""" + Environment.NewLine);

        var storage = new JsonSampleStorage(dir);
        await storage.AppendAsync(new Sample
        {
            T = DateTime.UtcNow.ToString("o"),
            ProxyConfig = new ProxyConfig()
        }, CancellationToken.None);

        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(Path.Combine(dir, JsonSampleStorage.DayFileName(DateTime.UtcNow))));
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
