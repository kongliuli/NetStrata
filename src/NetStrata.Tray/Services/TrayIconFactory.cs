using System.Drawing;
using NetStrata.Core.Tui;

namespace NetStrata.Tray.Services;

internal static class TrayIconFactory
{
    public static Icon FromColor(string color) => color switch
    {
        "green" => Create(Color.FromArgb(52, 168, 83)),
        "yellow" => Create(Color.FromArgb(251, 188, 4)),
        "red" => Create(Color.FromArgb(234, 67, 53)),
        _ => Create(Color.FromArgb(154, 160, 166))
    };

    private static Icon Create(Color fill)
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(fill);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.FillEllipse(brush, 2, 2, 12, 12);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public static void Apply(NotifyIcon icon, TrayIconState state)
    {
        icon.Text = Truncate(state.Tooltip, 63);
        var next = FromColor(state.Color);
        var prev = icon.Icon;
        icon.Icon = next;
        prev?.Dispose();
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";
}
