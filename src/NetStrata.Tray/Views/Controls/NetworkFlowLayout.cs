using System.Windows;
using NetStrata.Core.Flow;
using WpfPoint = System.Windows.Point;

namespace NetStrata.Tray.Views.Controls;

internal sealed record FlowLayout(double Height, IReadOnlyDictionary<string, WpfPoint> Points);

internal static class NetworkFlowLayout
{
    public const double WideBreakpoint = 700;

    public static FlowLayout Calculate(FlowTraceMode mode, double width)
    {
        var narrow = width < WideBreakpoint;
        return mode switch
        {
            FlowTraceMode.Layers => narrow ? NarrowLayers() : WideLayers(width),
            FlowTraceMode.Routes => narrow ? NarrowRoutes() : WideRoutes(width),
            _ => narrow ? NarrowTls() : WideTls(width)
        };
    }

    private static FlowLayout WideLayers(double width)
    {
        const double margin = 72;
        var usable = width - margin * 2;
        return new FlowLayout(320, new Dictionary<string, WpfPoint>
        {
            ["wifi"] = new(margin, 160),
            ["lan"] = new(margin + usable * 0.22, 160),
            ["broadband"] = new(margin + usable * 0.44, 160),
            ["overseas_direct"] = new(margin + usable * 0.68, 90),
            ["proxy"] = new(margin + usable * 0.68, 230),
            ["ai"] = new(width - margin, 160)
        });
    }

    private static FlowLayout NarrowLayers() => new(560, new Dictionary<string, WpfPoint>
    {
        ["wifi"] = new(0.5, 54),
        ["lan"] = new(0.5, 152),
        ["broadband"] = new(0.5, 250),
        ["overseas_direct"] = new(0.28, 354),
        ["proxy"] = new(0.72, 354),
        ["ai"] = new(0.5, 474)
    });

    private static FlowLayout WideRoutes(double width) => new(320, new Dictionary<string, WpfPoint>
    {
        ["source"] = new(92, 160),
        ["direct"] = new(0.46 * width, 88),
        ["proxy"] = new(0.46 * width, 232),
        ["target"] = new(width - 92, 160)
    });

    private static FlowLayout NarrowRoutes() => new(490, new Dictionary<string, WpfPoint>
    {
        ["source"] = new(0.5, 64),
        ["direct"] = new(0.28, 210),
        ["proxy"] = new(0.72, 210),
        ["target"] = new(0.5, 380)
    });

    private static FlowLayout WideTls(double width) => new(320, new Dictionary<string, WpfPoint>
    {
        ["dns"] = new(92, 160),
        ["tcp"] = new(0.36 * width, 160),
        ["tls"] = new(0.64 * width, 160),
        ["http"] = new(width - 92, 160)
    });

    private static FlowLayout NarrowTls() => new(490, new Dictionary<string, WpfPoint>
    {
        ["dns"] = new(0.5, 54),
        ["tcp"] = new(0.5, 164),
        ["tls"] = new(0.5, 274),
        ["http"] = new(0.5, 384)
    });

    public static IReadOnlyDictionary<string, WpfPoint> ResolveRelativeX(FlowLayout layout, double width) =>
        layout.Points.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.X <= 1 ? new WpfPoint(pair.Value.X * width, pair.Value.Y) : pair.Value);
}
