using NetStrata.Core.Models;
using NetStrata.Core.Tui;

namespace NetStrata.Core.Tests.Tui;

public sealed class TrayStatusMapperTests
{
    [Theory]
    [InlineData("healthy", "green")]
    [InlineData("direct_blocked_proxy_ok", "green")]
    [InlineData("degraded", "yellow")]
    [InlineData("broadband_bad", "red")]
    [InlineData(null, "gray")]
    public void Map_MapsOverallToColor(string? overall, string color)
    {
        var state = TrayStatusMapper.Map(overall);
        Assert.Equal(color, state.Color);
    }

    [Fact]
    public void MapFromState_UsesLatestVerdict()
    {
        var daemon = new DaemonState
        {
            StartedAt = "2026-07-09T04:00:00Z",
            Cycle = 1,
            Latest = new Sample
            {
                T = "2026-07-09T04:00:00Z",
                CycleMs = 100,
                ProxyConfig = new ProxyConfig(),
                Verdict = new Verdict
                {
                    Overall = "degraded",
                    Headline = "test",
                    Ai = new AiVerdict { State = "ok", Headline = "ok" }
                }
            }
        };

        var icon = TrayStatusMapper.MapFromState(daemon);
        Assert.Equal("yellow", icon.Color);
        Assert.Equal("degraded", icon.Overall);
    }
}
