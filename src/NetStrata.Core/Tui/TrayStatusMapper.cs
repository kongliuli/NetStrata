using NetStrata.Core.Models;

namespace NetStrata.Core.Tui;

public sealed record TrayIconState
{
    public required string Overall { get; init; }
    public required string Color { get; init; }
    public required string Tooltip { get; init; }
}

public static class TrayStatusMapper
{
    public static TrayIconState Map(string? overall)
    {
        if (string.IsNullOrWhiteSpace(overall))
            return Unknown();

        return overall switch
        {
            "healthy" or "direct_blocked_proxy_ok" =>
                new TrayIconState { Overall = overall, Color = "green", Tooltip = "网络正常" },
            "degraded" =>
                new TrayIconState { Overall = overall, Color = "yellow", Tooltip = "网络降级" },
            "wifi_bad" or "lan_bad" or "broadband_bad" or "proxy_bad" =>
                new TrayIconState { Overall = overall, Color = "red", Tooltip = HeadlineFor(overall) },
            _ => new TrayIconState { Overall = overall, Color = "yellow", Tooltip = overall }
        };
    }

    public static TrayIconState MapFromState(DaemonState? state) =>
        Map(state?.Latest?.Verdict?.Overall);

    private static TrayIconState Unknown() =>
        new() { Overall = "unknown", Color = "gray", Tooltip = "等待数据" };

    private static string HeadlineFor(string overall) => overall switch
    {
        "wifi_bad" => "Wi-Fi 故障",
        "lan_bad" => "局域网故障",
        "broadband_bad" => "宽带/DNS 故障",
        "proxy_bad" => "代理故障",
        _ => "网络异常"
    };
}
