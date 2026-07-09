using NetStrata.Core.Models;
using NetStrata.Core.Tui;

namespace NetStrata.Core.Tests.Tui;

public sealed class AlertNotifierTests
{
    [Fact]
    public void DetectNew_ReturnsOnlyUnseenAlerts()
    {
        var prev =
        [
            new Alert { T = "1", Type = "route", Message = "old" }
        ];
        var curr =
        [
            new Alert { T = "1", Type = "route", Message = "old" },
            new Alert { T = "2", Type = "proxy", Message = "proxy down" }
        ];

        var fresh = AlertNotifier.DetectNew(prev, curr);
        Assert.Single(fresh);
        Assert.Equal("proxy down", fresh[0].Message);
    }

    [Fact]
    public void ConsumeNew_FirstPoll_SeedsWithoutNotify()
    {
        var watch = new AlertWatchState();
        var state = new DaemonState
        {
            StartedAt = "t",
            Cycle = 1,
            RecentAlerts = [new Alert { T = "1", Type = "x", Message = "boot" }]
        };

        Assert.Empty(watch.ConsumeNew(state));
        Assert.Empty(watch.ConsumeNew(state));
    }

    [Fact]
    public void ConsumeNew_SecondPoll_ReturnsNewAlert()
    {
        var watch = new AlertWatchState();
        watch.ConsumeNew(new DaemonState
        {
            StartedAt = "t",
            Cycle = 1,
            RecentAlerts = []
        });

        var fresh = watch.ConsumeNew(new DaemonState
        {
            StartedAt = "t",
            Cycle = 2,
            RecentAlerts = [new Alert { T = "2", Type = "route", Message = "flap" }]
        });

        Assert.Single(fresh);
        Assert.Equal("flap", fresh[0].Message);
    }
}
