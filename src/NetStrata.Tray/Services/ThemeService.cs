using Microsoft.Win32;
using NetStrata.Core.Ui;

namespace NetStrata.Tray.Services;

internal static class ThemeService
{
    public static bool SystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
                return i == 0;
        }
        catch
        {
            // ponytail: fall back to dark
        }

        return true;
    }

    public static ThemePalette Current(string? themeConfig) =>
        ThemeResolver.Resolve(ThemeResolver.Parse(themeConfig), SystemIsDark());
}
