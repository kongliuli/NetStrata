using NetStrata.Core.Models;

namespace NetStrata.Core.Ui;

/// <summary>Human-readable, localized alert lines for non-expert users.</summary>
public sealed record AlertView(
    string Title,
    string Detail,
    string Severity,
    string WhenLocal,
    string Type);

public static class AlertPresenter
{
    public const int OverviewMax = 5;

    public static AlertView Format(Alert alert, string? lang = null)
    {
        lang = LangResolver.Resolve(lang);
        var when = FormatWhen(alert.T, lang);
        return alert.Type.ToLowerInvariant() switch
        {
            "egress_changed" => new(
                UiStrings.T(lang, "代理出口地址发生变更", "Proxy exit address has changed"),
                DescribeChange(lang,
                    UiStrings.T(lang, "代理用于访问外网的出口地址", "The proxy exit address used to reach the internet"),
                    alert.Prev, alert.Curr),
                "warn", when, alert.Type),
            "gateway_changed" => new(
                UiStrings.T(lang, "路由器（网关）发生变更", "Default gateway has changed"),
                DescribeChange(lang,
                    UiStrings.T(lang, "本机默认网关", "This PC's default gateway"),
                    alert.Prev, alert.Curr),
                "warn", when, alert.Type),
            "ipv4_changed" => new(
                UiStrings.T(lang, "本机 IP 地址发生变更", "This PC's IP address has changed"),
                DescribeChange(lang,
                    UiStrings.T(lang, "本机 IPv4 地址", "This PC's IPv4 address"),
                    alert.Prev, alert.Curr),
                "info", when, alert.Type),
            "interface_changed" => new(
                UiStrings.T(lang, "上网网卡发生变更", "Active network adapter has changed"),
                DescribeChange(lang,
                    UiStrings.T(lang, "当前使用的网卡", "The active network adapter"),
                    alert.Prev, alert.Curr),
                "info", when, alert.Type),
            "proxy_down" => new(
                UiStrings.T(lang, "代理服务未在运行", "Proxy service is not running"),
                UiStrings.T(lang,
                    "已配置代理，但连续多次无法连接本地端口。请启动代理软件，或核对端口配置是否正确。",
                    "A proxy is configured, but its local port did not respond for several checks. Please start the proxy app or verify the port."),
                "fail", when, alert.Type),
            "egress_flapping" => new(
                UiStrings.T(lang, "代理出口地址频繁波动", "Proxy exit address is fluctuating"),
                UiStrings.T(lang,
                    "短时间内代理出口地址多次变更，网络可能不稳定，或代理正在切换节点。",
                    "The proxy exit address changed several times in a short window. The network may be unstable, or the proxy is switching nodes."),
                "warn", when, alert.Type),
            _ => new(
                UiStrings.T(lang, "网络状态发生变更", "Network status has changed"),
                SoftenRawMessage(alert.Message, lang),
                "info", when, alert.Type)
        };
    }

    /// <summary>Newest-last slice for overview (max <see cref="OverviewMax"/>).</summary>
    public static IReadOnlyList<AlertView> TakeRecentViews(IReadOnlyList<Alert> alerts, string? lang = null, int take = OverviewMax)
    {
        if (alerts.Count == 0)
            return [];
        lang = LangResolver.Resolve(lang);
        take = Math.Clamp(take, 1, OverviewMax);
        return alerts.TakeLast(take).Select(a => Format(a, lang)).ToList();
    }

    public static string SummaryLine(IReadOnlyList<Alert> alerts, string? lang = null, int take = OverviewMax)
    {
        var views = TakeRecentViews(alerts, lang, take);
        if (views.Count == 0)
            return "";
        lang = LangResolver.Resolve(lang);
        return string.Join(UiStrings.T(lang, "；", "; "), views.Select(v => v.Title));
    }

    private static string DescribeChange(string lang, string what, string? prev, string? curr)
    {
        var from = FriendlyAddress(prev);
        var to = FriendlyAddress(curr);
        if (from is null && to is null)
            return what;
        if (from is null)
            return UiStrings.T(lang, $"{what}现为 {to}。", $"{what} is now {to}.");
        if (to is null)
            return UiStrings.T(lang, $"{what}由 {from} 变更为未知。", $"{what} changed from {from} to unknown.");
        return UiStrings.T(lang,
            $"{what}由 {from} 变更为 {to}。",
            $"{what} changed from {from} to {to}.");
    }

    /// <summary>Shorten long IPv6 / technical strings for non-expert readers.</summary>
    public static string? FriendlyAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        if (v.Contains(':') && v.Length > 22)
            return v[..8] + "…" + v[^6..];
        if (v.Length > 40)
            return v[..18] + "…";
        return v;
    }

    private static string SoftenRawMessage(string message, string lang)
    {
        // Fallback for unknown types: strip jargon tokens for display.
        var m = message
            .Replace("proxy egress", UiStrings.T(lang, "代理出口", "proxy exit"), StringComparison.OrdinalIgnoreCase)
            .Replace("gateway", UiStrings.T(lang, "网关", "gateway"), StringComparison.OrdinalIgnoreCase)
            .Replace("interface", UiStrings.T(lang, "网卡", "adapter"), StringComparison.OrdinalIgnoreCase);
        return m;
    }

    private static string FormatWhen(string iso, string lang)
    {
        if (!DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return iso;
        var local = dt.ToLocalTime();
        return local.ToString(UiStrings.T(lang, "M月d日 HH:mm", "MMM d HH:mm"));
    }
}
