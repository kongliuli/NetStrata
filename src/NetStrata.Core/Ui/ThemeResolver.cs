namespace NetStrata.Core.Ui;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public sealed record ThemePalette(
    string WindowBg,
    string Foreground,
    string Muted,
    string CardBg,
    string CardBorder,
    string AlertBg,
    string AlertFg,
    string AlertBorder,
    string InputBg,
    string Accent);

public static class ThemeResolver
{
    public static readonly ThemePalette Dark = new(
        WindowBg: "#0F1115",
        Foreground: "#E8EAED",
        Muted: "#9AA0A6",
        CardBg: "#1A1D24",
        CardBorder: "#2D323C",
        AlertBg: "#3C2F1E",
        AlertFg: "#FBBC04",
        AlertBorder: "#FBBC04",
        InputBg: "#1A1D24",
        Accent: "#8AB4F8");

    public static readonly ThemePalette Light = new(
        WindowBg: "#F5F6F8",
        Foreground: "#202124",
        Muted: "#5F6368",
        CardBg: "#FFFFFF",
        CardBorder: "#DADCE0",
        AlertBg: "#FEF7E0",
        AlertFg: "#B06000",
        AlertBorder: "#F9AB00",
        InputBg: "#FFFFFF",
        Accent: "#1A73E8");

    public static ThemeMode Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "light" => ThemeMode.Light,
        "dark" => ThemeMode.Dark,
        _ => ThemeMode.System
    };

    public static string ToConfig(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => "light",
        ThemeMode.Dark => "dark",
        _ => "system"
    };

    public static ThemePalette Resolve(ThemeMode mode, bool systemIsDark) => mode switch
    {
        ThemeMode.Light => Light,
        ThemeMode.Dark => Dark,
        _ => systemIsDark ? Dark : Light
    };
}
