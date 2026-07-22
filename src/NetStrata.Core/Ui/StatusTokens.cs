namespace NetStrata.Core.Ui;

/// <summary>
/// Semantic status colors for WPF (W11a). Values match DESIGN.md §2;
/// soft dark variants are NetStrata additions for readable alert/badge fills.
/// </summary>
public enum StatusKind
{
    Ok,
    Degraded,
    Fail,
    Skipped,
    Info
}

public static class StatusTokens
{
    // accent (text / stroke)
    public const string OkLight = "#137333";
    public const string OkDark = "#81C995";
    public const string DegradedLight = "#B06000";
    public const string DegradedDark = "#FDD663";
    public const string FailLight = "#C5221F";
    public const string FailDark = "#F28B82";
    public const string SkippedLight = "#5F6368";
    public const string SkippedDark = "#9AA0A6";

    // soft fills
    public const string OkSoftLight = "#E8F5E9";
    public const string OkSoftDark = "#1E3A2A";
    public const string DegradedSoftLight = "#FEF7E0";
    public const string DegradedSoftDark = "#3D3320";
    public const string FailSoftLight = "#FCE8E6";
    public const string FailSoftDark = "#3C2523";
    public const string SkippedSoftLight = "#F1F3F4";
    public const string SkippedSoftDark = "#2A2E35";
    public const string InfoSoftLight = "#E8F0FE";
    public const string InfoSoftDark = "#1A2A3C";

    // vivid borders used by heat strips / overall badge outline (same family as ok/fail)
    public const string OkBorderLight = "#34A853";
    public const string OkBorderDark = "#34A853";
    public const string DegradedBorderLight = "#F9AB00";
    public const string DegradedBorderDark = "#FBBC04";
    public const string FailBorderLight = "#EA4335";
    public const string FailBorderDark = "#EA4335";

    public const string ResourceOk = "NsStatusOkBrush";
    public const string ResourceOkSoft = "NsStatusOkSoftBrush";
    public const string ResourceDegraded = "NsStatusDegradedBrush";
    public const string ResourceDegradedSoft = "NsStatusDegradedSoftBrush";
    public const string ResourceFail = "NsStatusFailBrush";
    public const string ResourceFailSoft = "NsStatusFailSoftBrush";
    public const string ResourceSkipped = "NsStatusSkippedBrush";
    public const string ResourceSkippedSoft = "NsStatusSkippedSoftBrush";
    public const string ResourceInfo = "NsStatusInfoBrush";
    public const string ResourceInfoSoft = "NsStatusInfoSoftBrush";

    public static StatusKind FromState(string? state) =>
        (state ?? "").Trim().ToLowerInvariant() switch
        {
            "ok" or "pass" or "passed" or "healthy" or "健康" or "正常" => StatusKind.Ok,
            "degraded" or "warn" or "warning" or "降级" => StatusKind.Degraded,
            "fail" or "failed" or "bad" or "error" or "失败" or "异常" => StatusKind.Fail,
            "skipped" or "skip" or "unknown" or "跳过" or "未知" => StatusKind.Skipped,
            _ => StatusKind.Info
        };

    public static StatusKind FromOverall(string? overall)
    {
        var key = (overall ?? "").Trim();
        if (key is "健康" or "healthy")
            return StatusKind.Ok;
        if (key is "降级" or "degraded")
            return StatusKind.Degraded;
        if (key.Contains("异常", StringComparison.Ordinal)
            || key.Contains("bad", StringComparison.OrdinalIgnoreCase)
            || key.Contains("失败", StringComparison.Ordinal)
            || key.Contains("fail", StringComparison.OrdinalIgnoreCase))
            return StatusKind.Fail;
        return StatusKind.Info;
    }

    public static StatusKind FromBorderHex(string? hex) =>
        (hex ?? "").Trim().ToLowerInvariant() switch
        {
            "#34a853" or "#0f9d58" or "#137333" or "#81c995" => StatusKind.Ok,
            "#fbbc04" or "#f9ab00" or "#b06000" or "#fdd663" => StatusKind.Degraded,
            "#ea4335" or "#d93025" or "#c5221f" or "#f28b82" => StatusKind.Fail,
            "#9aa0a6" or "#5f6368" or "#2d323c" => StatusKind.Skipped,
            _ => StatusKind.Info
        };

    public static StatusKind FromAlertSeverity(string? severity) =>
        (severity ?? "").Trim().ToLowerInvariant() switch
        {
            "fail" or "error" => StatusKind.Fail,
            "warn" or "warning" => StatusKind.Degraded,
            _ => StatusKind.Info
        };

    public static string AccentHex(StatusKind kind, bool dark) => kind switch
    {
        StatusKind.Ok => dark ? OkDark : OkLight,
        StatusKind.Degraded => dark ? DegradedDark : DegradedLight,
        StatusKind.Fail => dark ? FailDark : FailLight,
        StatusKind.Skipped => dark ? SkippedDark : SkippedLight,
        _ => dark ? ThemeResolver.Dark.Accent : ThemeResolver.Light.Accent
    };

    public static string SoftHex(StatusKind kind, bool dark) => kind switch
    {
        StatusKind.Ok => dark ? OkSoftDark : OkSoftLight,
        StatusKind.Degraded => dark ? DegradedSoftDark : DegradedSoftLight,
        StatusKind.Fail => dark ? FailSoftDark : FailSoftLight,
        StatusKind.Skipped => dark ? SkippedSoftDark : SkippedSoftLight,
        _ => dark ? InfoSoftDark : InfoSoftLight
    };

    public static string BorderHex(StatusKind kind, bool dark) => kind switch
    {
        StatusKind.Ok => dark ? OkBorderDark : OkBorderLight,
        StatusKind.Degraded => dark ? DegradedBorderDark : DegradedBorderLight,
        StatusKind.Fail => dark ? FailBorderDark : FailBorderLight,
        StatusKind.Skipped => AccentHex(StatusKind.Skipped, dark),
        _ => AccentHex(StatusKind.Info, dark)
    };

    public static string ResourceKey(StatusKind kind, bool soft) => (kind, soft) switch
    {
        (StatusKind.Ok, false) => ResourceOk,
        (StatusKind.Ok, true) => ResourceOkSoft,
        (StatusKind.Degraded, false) => ResourceDegraded,
        (StatusKind.Degraded, true) => ResourceDegradedSoft,
        (StatusKind.Fail, false) => ResourceFail,
        (StatusKind.Fail, true) => ResourceFailSoft,
        (StatusKind.Skipped, false) => ResourceSkipped,
        (StatusKind.Skipped, true) => ResourceSkippedSoft,
        (_, false) => ResourceInfo,
        (_, true) => ResourceInfoSoft
    };
}
