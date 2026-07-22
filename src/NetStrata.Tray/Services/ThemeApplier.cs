using System.Windows;
using System.Windows.Media;
using HandyControl.Data;
using HandyControl.Tools;
using NetStrata.Core.Config;
using NetStrata.Core.Ui;

namespace NetStrata.Tray.Services;

/// <summary>
/// Syncs HandyControl SkinDefault/SkinDark with NetStrata theme setting.
/// Semantic status colors stay on cards; chrome uses HC brushes.
/// </summary>
internal static class ThemeApplier
{
    private static SkinType? _appliedSkin;

    public static ThemePalette CurrentPalette() =>
        ThemeService.Current(NetStrataOptions.FromEnvironment().Theme);

    public static void Apply(FrameworkElement target)
    {
        var palette = CurrentPalette();
        // keep a few app-specific keys for status badges that HC doesn't own
        target.Resources["WindowBg"] = Brush(palette.WindowBg);
        target.Resources["CardBg"] = Brush(palette.CardBg);
        target.Resources["CardBorder"] = Brush(palette.CardBorder);
        target.Resources["Muted"] = Brush(palette.Muted);
        target.Resources["Accent"] = Brush(palette.Accent);
        target.Resources["AlertBg"] = Brush(palette.AlertBg);
        target.Resources["AlertFg"] = Brush(palette.AlertFg);
        target.Resources["AlertBorder"] = Brush(palette.AlertBorder);

        ApplyStatusTokens(target.Resources, IsDark(palette));
        ApplyHandySkin(palette);
        // also push status tokens to Application so DynamicResource in XAML resolves before first Apply
        if (System.Windows.Application.Current is not null)
            ApplyStatusTokens(System.Windows.Application.Current.Resources, IsDark(palette));
    }

    public static void ApplyHandySkin() => ApplyHandySkin(CurrentPalette());

    public static void ApplyHandySkin(ThemePalette palette) => ApplyHandySkin(IsDark(palette));

    public static void ApplyHandySkin(bool dark)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        var skin = dark ? SkinType.Dark : SkinType.Default;
        if (_appliedSkin == skin)
            return;

        var dicts = app.Resources.MergedDictionaries;
        if (dicts.Count >= 1)
            dicts[0] = ResourceHelper.GetSkin(skin);
        else
        {
            dicts.Add(ResourceHelper.GetSkin(skin));
            dicts.Add(ResourceHelper.GetTheme());
        }

        _appliedSkin = skin;
        ApplyStatusTokens(app.Resources, dark);
    }

    private static void ApplyStatusTokens(ResourceDictionary resources, bool dark)
    {
        void Set(string key, string hex) => resources[key] = Brush(hex);

        Set(StatusTokens.ResourceOk, StatusTokens.AccentHex(StatusKind.Ok, dark));
        Set(StatusTokens.ResourceOkSoft, StatusTokens.SoftHex(StatusKind.Ok, dark));
        Set(StatusTokens.ResourceDegraded, StatusTokens.AccentHex(StatusKind.Degraded, dark));
        Set(StatusTokens.ResourceDegradedSoft, StatusTokens.SoftHex(StatusKind.Degraded, dark));
        Set(StatusTokens.ResourceFail, StatusTokens.AccentHex(StatusKind.Fail, dark));
        Set(StatusTokens.ResourceFailSoft, StatusTokens.SoftHex(StatusKind.Fail, dark));
        Set(StatusTokens.ResourceSkipped, StatusTokens.AccentHex(StatusKind.Skipped, dark));
        Set(StatusTokens.ResourceSkippedSoft, StatusTokens.SoftHex(StatusKind.Skipped, dark));
        Set(StatusTokens.ResourceInfo, StatusTokens.AccentHex(StatusKind.Info, dark));
        Set(StatusTokens.ResourceInfoSoft, StatusTokens.SoftHex(StatusKind.Info, dark));
    }

    private static bool IsDark(ThemePalette palette) =>
        string.Equals(palette.WindowBg, ThemeResolver.Dark.WindowBg, StringComparison.OrdinalIgnoreCase);

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
