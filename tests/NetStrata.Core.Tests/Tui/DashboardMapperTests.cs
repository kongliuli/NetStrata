using NetStrata.Core.Models;
using NetStrata.Core.Probes;
using NetStrata.Core.Tui;

namespace NetStrata.Core.Tests.Tui;

public sealed class DashboardMapperTests
{
    [Fact]
    public void FromState_Null_ReturnsWaiting()
    {
        var vm = DashboardMapper.FromState(null, "zh");
        Assert.False(vm.HasData);
        Assert.Contains("等待", vm.Headline);
    }

    [Fact]
    public void FromState_WithVerdict_MapsLayersAiAndProxy()
    {
        var https = AiApiCatalog.Providers
            .SelectMany(p => new[]
            {
                new HttpsResult
                {
                    Label = $"{p.Id}_direct", Url = p.ProbeUrl, Via = "direct", Ok = true, TotalMs = 80
                },
                new HttpsResult
                {
                    Label = $"{p.Id}_proxy", Url = p.ProbeUrl, Via = "proxy", Ok = false, TotalMs = 0, Err = "timeout"
                }
            })
            .ToList();

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
                Https = https,
                Pings =
                [
                    new PingResult { Target = "1.2.3.4", Custom = true, Label = "nas", Ok = true, AvgMs = 3, LossPct = 0 }
                ],
                Verdict = new Verdict
                {
                    Overall = "degraded",
                    Headline = "dns blocked",
                    Layers =
                    [
                        new LayerVerdict { Layer = "lan", State = "ok" },
                        new LayerVerdict { Layer = "broadband", State = "degraded" },
                        new LayerVerdict { Layer = "ai", State = "ok" }
                    ],
                    Ai = new AiVerdict { State = "ok", Headline = "AI ok" }
                }
            }
        };

        var vm = DashboardMapper.FromState(state, "zh");
        Assert.True(vm.HasData);
        Assert.Equal("降级", vm.Overall);
        Assert.Equal(2, vm.Layers.Count); // ai layer excluded from network section
        Assert.Equal(AiApiCatalog.Providers.Length, vm.AiApis.Count);
        Assert.Contains(vm.AiApis, a => a.Name == "ChatGPT");
        Assert.Contains(vm.AiApis, a => a.Id == "anthropic" && a.OpenUrl == "https://claude.ai/");
        Assert.Contains(vm.AiApis, a => a.Id == "google" && a.OpenUrl == "https://aistudio.google.com/");
        Assert.Contains(vm.AiApis, a => a.Id == "anthropic" && a.ProbeUrl.Contains("api.anthropic.com"));
        Assert.True(vm.HasCustomPings);
        Assert.Single(vm.CustomPings);
        Assert.Contains("7890", vm.ProxySummary);
        Assert.Contains("proxy down", vm.AlertsSummary);
        Assert.Equal("AI ok", vm.AiHeadline);
    }
}
