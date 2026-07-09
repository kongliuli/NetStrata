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
    public static TrayIconState Map(string? overall) => overall switch
    {
        "ok" => new TrayIconState { Overall = "ok", Color = "green", Tooltip = "网络正常" },
        "degraded" => new TrayIconState { Overall = "degraded", Color = "yellow", Tooltip = "网络降级" },
        "fail" => new TrayIconState { Overall = "fail", Color = "red", Tooltip = "网络故障" },
        _ => new TrayIconState { Overall = "unknown", Color = "gray", Tooltip = "等待数据" }
    };

    public static TrayIconState MapFromState(DaemonState? state) =>
        Map(state?.Latest?.Verdict?.Overall);
}
