using NetStrata.Core.Models;
using NetStrata.Core.Judge;

namespace NetStrata.Core.Tests.Judge;

public sealed class ConclusionEngineTests
{
    private readonly ConclusionEngine _engine = new();

    [Fact]
    public void Generate_Healthy_Minimal()
    {
        var samples = Enumerable.Repeat(SampleBuilder.Healthy(), 5).ToList();
        var md = _engine.GenerateMarkdown(samples);
        Assert.Contains("正常", md);
        Assert.DoesNotContain("# 结论", md);
    }

    [Fact]
    public void Generate_ProxyFlapping_MentionsUnstable()
    {
        var baseSample = SampleBuilder.WithProxy();
        var samples = new List<Sample>
        {
            baseSample with { ProxyEgress = new ProxyEgress { Ok = true, Ip = "1.1.1.1", Ms = 100 } },
            baseSample with { ProxyEgress = new ProxyEgress { Ok = true, Ip = "2.2.2.2", Ms = 100 } },
            baseSample with { ProxyEgress = new ProxyEgress { Ok = true, Ip = "3.3.3.3", Ms = 100 } },
            baseSample with { ProxyEgress = new ProxyEgress { Ok = true, Ip = "4.4.4.4", Ms = 100 } }
        };

        var md = _engine.GenerateMarkdown(samples);
        Assert.Contains("代理不稳定", md);
    }

    [Fact]
    public void Generate_DegradedBurst_MentionsDegraded()
    {
        var baseSample = SampleBuilder.Healthy() with
        {
            Verdict = new VerdictEngine().Judge(SampleBuilder.Healthy())
        };
        var degraded = baseSample with
        {
            Verdict = baseSample.Verdict! with { Overall = "degraded" }
        };
        var samples = Enumerable.Repeat(degraded, 12)
            .Concat(Enumerable.Repeat(baseSample, 8))
            .ToList();

        var md = _engine.GenerateMarkdown(samples);
        Assert.Contains("多次降级", md);
    }

    [Fact]
    public void Generate_CustomPingFail_MentionsTarget()
    {
        var failing = SampleBuilder.Healthy() with
        {
            Pings = SampleBuilder.Healthy().Pings.Concat(
            [
                new PingResult { Target = "192.168.1.50", Label = "nas", Custom = true, Ok = false, AvgMs = 0 }
            ]).ToList()
        };
        var samples = Enumerable.Repeat(failing, 3).ToList();
        var md = _engine.GenerateMarkdown(samples);
        Assert.Contains("nas", md);
        Assert.Contains("192.168.1.50", md);
    }

    [Fact]
    public void Generate_Empty_ReturnsPlaceholder()
    {
        var md = _engine.GenerateMarkdown([]);
        Assert.Contains("no conclusions yet", md);
    }
}
