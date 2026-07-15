using NetStrata.Core.Models;
using NetStrata.Core.Storage;

namespace NetStrata.Core.Tests.Storage;

public sealed class TrendChartBuilderTests
{
    [Fact]
    public void Filter_KeepsWindowOnly()
    {
        var now = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);
        var samples = new List<Sample>
        {
            SampleAt(now.AddHours(-2)),
            SampleAt(now.AddMinutes(-30)),
            SampleAt(now.AddMinutes(-5))
        };
        var filtered = TrendWindow.Filter(samples, TimeSpan.FromHours(1), now);
        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void Build_MapsPingSeries()
    {
        var s = new Sample
        {
            T = "2026-07-14T12:00:00Z",
            Iface = new InterfaceInfo { Gateway = "192.168.1.1" },
            Pings =
            [
                new PingResult { Target = "192.168.1.1", Ok = true, AvgMs = 4 },
                new PingResult { Target = "223.5.5.5", Ok = true, AvgMs = 20 },
                new PingResult { Target = "1.1.1.1", Ok = true, AvgMs = 80 }
            ],
            ProxyConfig = new ProxyConfig(),
            Verdict = new Verdict
            {
                Overall = "healthy",
                Headline = "all green",
                Layers =
                [
                    new LayerVerdict { Layer = "lan", State = "ok", Reasons = [] }
                ],
                Ai = new AiVerdict { State = "ok", Headline = "ok" }
            }
        };
        var chart = TrendChartBuilder.Build([s]);
        Assert.Single(chart.Labels);
        Assert.Equal(4, chart.GatewayMs[0]);
        Assert.Equal(20, chart.DomesticMs[0]);
        Assert.Equal(80, chart.OverseasMs[0]);
        Assert.Equal("ok", chart.LayerStates["lan"][0]);
    }

    private static Sample SampleAt(DateTime utc) => new()
    {
        T = utc.ToString("o"),
        ProxyConfig = new ProxyConfig()
    };
}
