using NetStrata.Core.Models;
using NetStrata.Core.Tui;
using NetStrata.Core.Ui;

namespace NetStrata.Core.Tests.Tui;

public sealed class ChainAndLocalMapperTests
{
    [Fact]
    public void ChainMapper_MapsLayersAndReasons()
    {
        var state = new DaemonState
        {
            StartedAt = "t",
            Cycle = 2,
            Latest = new Sample
            {
                T = "2026-07-10T01:00:00Z",
                CycleMs = 900,
                ProxyConfig = new ProxyConfig(),
                Verdict = new Verdict
                {
                    Overall = "degraded",
                    Headline = "dns_udp_blocked: dig 223.5.5.5 fail but ping/https ok",
                    Layers =
                    [
                        new LayerVerdict
                        {
                            Layer = "broadband",
                            State = "degraded",
                            Reasons = ["dns_udp_blocked: dig 223.5.5.5 fail but ping/https ok"],
                            Metrics = new Dictionary<string, object?> { ["baiduMs"] = 120 }
                        },
                        new LayerVerdict { Layer = "ai", State = "ok", Reasons = [] }
                    ],
                    Ai = new AiVerdict { State = "ok", Headline = "全部 6 个 AI API 直连可达（未使用代理）" }
                }
            }
        };

        var vm = ChainMapper.FromState(state, "zh");
        Assert.True(vm.HasData);
        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal("国内宽带", vm.Rows[0].DisplayName);
        Assert.Equal("降级", vm.Overall);
        Assert.Contains(vm.Rows[0].Reasons, r => r.Contains("DNS UDP 被拦"));
        Assert.Contains("百度耗时=120", vm.Rows[0].MetricsSummary);
        Assert.Contains("直连可达", vm.AiHeadline);
    }

    [Fact]
    public void Phrase_LocalizesEngineReasons()
    {
        Assert.Equal("一切正常", UiStrings.Phrase("zh", "all green"));
        Assert.Equal("主链路为 有线（Ethernet）", UiStrings.Phrase("zh", "primary link is ethernet (Ethernet)"));
        Assert.Equal("all green", UiStrings.Phrase("en", "all green"));
    }

    [Fact]
    public void LocalNetMapper_ShowsIpv4()
    {
        var state = new DaemonState
        {
            StartedAt = "t",
            Cycle = 1,
            Latest = new Sample
            {
                T = "t",
                ProxyConfig = new ProxyConfig { ProxyUrl = "http://127.0.0.1:7890", Listening = true },
                Iface = new InterfaceInfo { Ipv4 = "192.168.1.8", Gateway = "192.168.1.1", LinkType = "wifi" },
                Wifi = new WifiInfo { Status = "connected", Ssid = "Home", Rssi = -50 },
                ProxyEgress = new ProxyEgress { Ok = true, Ip = "9.9.9.9" }
            }
        };

        var vm = LocalNetMapper.FromState(state, "zh");
        Assert.True(vm.HasData);
        Assert.Contains(vm.Rows, r => r.Label == "本机 IPv4" && r.Value == "192.168.1.8");
        Assert.Contains(vm.Rows, r => r.Label == "出口 IP" && r.Value == "9.9.9.9");
    }
}
