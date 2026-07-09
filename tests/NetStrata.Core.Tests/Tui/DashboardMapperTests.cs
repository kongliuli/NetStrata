using NetStrata.Core.Models;
using NetStrata.Core.Tui;

namespace NetStrata.Core.Tests.Tui;

public sealed class DashboardMapperTests
{
    [Fact]
    public void FromState_Null_ReturnsWaiting()
    {
        var vm = DashboardMapper.FromState(null);
        Assert.False(vm.HasData);
        Assert.Contains("等待", vm.Headline);
    }

    [Fact]
    public void FromState_WithVerdict_MapsLayersAndProxy()
    {
        var state = new DaemonState
        {
            StartedAt = "t",
            Cycle = 3,
            RecentAlerts = [new Alert { T = "t", Type = "route", Message = "proxy down" }],
            Latest = new Sample
            {
                T = "2026-07-09T05:00:00Z",
                CycleMs = 1200,
                ProxyConfig = new ProxyConfig
                {
                    ProxyUrl = "http://127.0.0.1:7890",
                    Listening = true,
                    ListenerProcess = "FlClashCore",
                    SystemProxy = new SystemProxySettings
                    {
                        HttpEnable = true,
                        HttpProxy = "127.0.0.1",
                        HttpPort = 7890
                    }
                },
                Verdict = new Verdict
                {
                    Overall = "degraded",
                    Headline = "dns blocked",
                    Layers =
                    [
                        new LayerVerdict { Layer = "lan", State = "ok" },
                        new LayerVerdict { Layer = "broadband", State = "degraded" }
                    ],
                    Ai = new AiVerdict { State = "ok", Headline = "ok" }
                }
            }
        };

        var vm = DashboardMapper.FromState(state);
        Assert.True(vm.HasData);
        Assert.Equal("degraded", vm.Overall);
        Assert.Equal(2, vm.Layers.Count);
        Assert.Contains("7890", vm.ProxySummary);
        Assert.Contains("proxy down", vm.AlertsSummary);
    }
}
