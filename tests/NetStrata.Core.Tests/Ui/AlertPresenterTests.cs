using NetStrata.Core.Models;
using NetStrata.Core.Ui;

namespace NetStrata.Core.Tests.Ui;

public sealed class AlertPresenterTests
{
    [Fact]
    public void Format_EgressChanged_IsPlainLanguage_Zh()
    {
        var view = AlertPresenter.Format(new Alert
        {
            T = "2026-07-15T06:00:00Z",
            Type = "egress_changed",
            Message = "proxy egress 188.253.121.93 → 2407:cdc0:d002:0:188:253:121:93",
            Prev = "188.253.121.93",
            Curr = "2407:cdc0:d002:0:188:253:121:93"
        }, "zh");

        Assert.Equal("代理出口地址发生变更", view.Title);
        Assert.DoesNotContain("proxy egress", view.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("188.253.121.93", view.Detail);
        Assert.Contains("变更为", view.Detail);
        Assert.Contains("…", view.Detail); // IPv6 shortened
        Assert.Equal("warn", view.Severity);
    }

    [Fact]
    public void Format_ProxyDown_SuggestsAction_En()
    {
        var view = AlertPresenter.Format(new Alert
        {
            T = "2026-07-15T06:00:00Z",
            Type = "proxy_down",
            Message = "proxy configured but not listening (3 cycles)"
        }, "en");

        Assert.Equal("Proxy service is not running", view.Title);
        Assert.Contains("start the proxy app", view.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("fail", view.Severity);
    }

    [Fact]
    public void TakeRecentViews_CapsAtFive_NewestLast()
    {
        var alerts = Enumerable.Range(1, 8).Select(i => new Alert
        {
            T = $"2026-07-15T06:0{i}:00Z",
            Type = "egress_changed",
            Message = $"m{i}",
            Prev = $"{i}.0.0.1",
            Curr = $"{i}.0.0.2"
        }).ToList();

        var views = AlertPresenter.TakeRecentViews(alerts, "zh");
        Assert.Equal(5, views.Count);
        // TakeLast(5) of 1..8 → 4..8, order preserved
        Assert.Contains("4.0.0.1", views[0].Detail);
        Assert.Contains("8.0.0.1", views[^1].Detail);
    }

    [Fact]
    public void SummaryLine_UsesTitlesNotRawMessages()
    {
        var line = AlertPresenter.SummaryLine(
        [
            new Alert
            {
                T = "t1",
                Type = "gateway_changed",
                Message = "gateway 1 → 2",
                Prev = "192.168.1.1",
                Curr = "192.168.1.2"
            },
            new Alert
            {
                T = "t2",
                Type = "egress_changed",
                Message = "proxy egress a → b",
                Prev = "1.1.1.1",
                Curr = "2.2.2.2"
            }
        ], "zh");

        Assert.Contains("路由器", line);
        Assert.Contains("代理出口", line);
        Assert.DoesNotContain("proxy egress", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FriendlyAddress_ShortensLongIpv6()
    {
        var shortIp = AlertPresenter.FriendlyAddress("2407:cdc0:d002:0:188:253:121:93");
        Assert.NotNull(shortIp);
        Assert.True(shortIp!.Length < "2407:cdc0:d002:0:188:253:121:93".Length);
        Assert.Contains('…', shortIp);
    }
}
