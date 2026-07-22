using System.Windows;
using System.Windows.Media;
using NetStrata.Core.Ui;

namespace NetStrata.Tray.Services;

/// <summary>Resolves NsStatus* DynamicResource brushes; falls back to StatusTokens hex.</summary>
internal static class StatusBrushes
{
    public static SolidColorBrush Accent(StatusKind kind) => Resolve(kind, soft: false);

    public static SolidColorBrush Soft(StatusKind kind) => Resolve(kind, soft: true);

    public static SolidColorBrush Border(StatusKind kind)
    {
        var dark = IsDark();
        return FromHex(StatusTokens.BorderHex(kind, dark));
    }

    public static SolidColorBrush ForState(string? state, bool soft = false) =>
        Resolve(StatusTokens.FromState(state), soft);

    public static SolidColorBrush FromBorderHex(string? hex, bool soft = false) =>
        Resolve(StatusTokens.FromBorderHex(hex), soft);

    public static SolidColorBrush ForOverall(string? overall, out SolidColorBrush foreground, out SolidColorBrush border)
    {
        var kind = StatusTokens.FromOverall(overall);
        foreground = Accent(kind);
        border = Border(kind);
        return Soft(kind);
    }

    private static SolidColorBrush Resolve(StatusKind kind, bool soft)
    {
        var key = StatusTokens.ResourceKey(kind, soft);
        if (System.Windows.Application.Current?.TryFindResource(key) is SolidColorBrush brush)
            return brush;
        return FromHex(soft ? StatusTokens.SoftHex(kind, IsDark()) : StatusTokens.AccentHex(kind, IsDark()));
    }

    private static bool IsDark()
    {
        var p = ThemeApplier.CurrentPalette();
        return string.Equals(p.WindowBg, ThemeResolver.Dark.WindowBg, StringComparison.OrdinalIgnoreCase);
    }

    private static SolidColorBrush FromHex(string hex)
    {
        var brush = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
