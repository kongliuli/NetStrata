using NetStrata.Core.Models;
using NetStrata.Core.Storage;

namespace NetStrata.Core.Tests.Storage;

public sealed class SeriesBuilderTests
{
    [Fact]
    public void Build_IncludesCustomPingSeries()
    {
        var samples = new[]
        {
            new Sample
            {
                T = "2026-07-09T04:00:00Z",
                Pings =
                [
                    new PingResult { Target = "192.168.1.50", Label = "nas", Custom = true, Ok = true, AvgMs = 1.2 }
                ],
                ProxyConfig = new ProxyConfig()
            }
        };

        var series = new SeriesBuilder().Build(samples);
        Assert.True(series.Pings.ContainsKey("custom_nas"));
        Assert.Equal(1.2, series.Pings["custom_nas"][0]);
    }
}
