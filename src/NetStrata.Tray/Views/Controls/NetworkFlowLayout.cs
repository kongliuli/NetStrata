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
            FlowTraceMode.Probe => CalculateProbe(["a", "b", "c", "d"], width),
            _ => narrow ? NarrowTls() : WideTls(width)
        };
    }

    /// <summary>
    /// Trunk on the left, egress hubs, then monitored targets fanned on the right.
    /// </summary>
    public static FlowLayout CalculateMultiTarget(FlowTrace trace, double width)
    {
        var points = new Dictionary<string, WpfPoint>();
        var narrow = width < WideBreakpoint;
        var directs = trace.Nodes.Where(n => n.Id.StartsWith("tgt:", StringComparison.Ordinal) && n.Lane == "direct").ToList();
        var proxies = trace.Nodes.Where(n => n.Id.StartsWith("tgt:", StringComparison.Ordinal) && n.Lane == "proxy").ToList();
        var hasDirect = trace.Nodes.Any(n => n.Id == "direct");
        var hasProxy = trace.Nodes.Any(n => n.Id == "proxy");

        if (narrow)
        {
            var y = 40.0;
            foreach (var id in new[] { "wifi", "lan", "broadband" })
            {
                if (trace.Nodes.Any(n => n.Id == id))
                {
                    points[id] = new WpfPoint(0.5, y);
                    y += 72;
                }
            }

            if (hasDirect)
            {
                points["direct"] = new WpfPoint(0.28, y);
            }

            if (hasProxy)
            {
                points["proxy"] = new WpfPoint(0.72, y);
            }

            if (hasDirect || hasProxy)
                y += 88;

            PlaceTargetsColumn(points, directs, 0.28, ref y);
            var yProxy = y;
            if (directs.Count > 0 && proxies.Count > 0)
                yProxy = points.Values.DefaultIfEmpty(new WpfPoint(0, y)).Max(p => p.Y) + 72;
            // place proxy targets starting near proxy hub band
            var startProxy = hasProxy
                ? (points.TryGetValue("proxy", out var ph) ? ph.Y + 80 : y)
                : y;
            var yp = startProxy;
            PlaceTargetsColumn(points, proxies, 0.72, ref yp);
            var height = Math.Max(y, Math.Max(yProxy, yp)) + 48;
            return new FlowLayout(height, points);
        }

        const double margin = 56;
        var usable = Math.Max(120, width - margin * 2);
        var trunkY = 160.0;
        points["wifi"] = new(margin, trunkY);
        points["lan"] = new(margin + usable * 0.18, trunkY);
        points["broadband"] = new(margin + usable * 0.36, trunkY);

        var hubX = margin + usable * 0.55;
        if (hasDirect && hasProxy)
        {
            points["direct"] = new(hubX, 88);
            points["proxy"] = new(hubX, 232);
        }
        else if (hasDirect)
        {
            points["direct"] = new(hubX, trunkY);
        }
        else if (hasProxy)
        {
            points["proxy"] = new(hubX, trunkY);
        }

        var targetX = width - margin;
        PlaceTargetsFan(points, directs, targetX, hasProxy ? 40 : 80, hasProxy ? 140 : 280);
        PlaceTargetsFan(points, proxies, targetX, hasDirect ? 200 : 80, hasDirect ? 300 : 280);

        var maxY = points.Values.DefaultIfEmpty(new WpfPoint(0, 280)).Max(p => p.Y);
        return new FlowLayout(Math.Max(320, maxY + 80), points);
    }

    private static void PlaceTargetsColumn(
        Dictionary<string, WpfPoint> points,
        IReadOnlyList<FlowNode> targets,
        double xFrac,
        ref double y)
    {
        foreach (var t in targets)
        {
            points[t.Id] = new WpfPoint(xFrac, y);
            y += 70;
        }
    }

    private static void PlaceTargetsFan(
        Dictionary<string, WpfPoint> points,
        IReadOnlyList<FlowNode> targets,
        double x,
        double y0,
        double y1)
    {
        if (targets.Count == 0)
            return;
        for (var i = 0; i < targets.Count; i++)
        {
            var t = targets.Count == 1 ? 0.5 : (double)i / (targets.Count - 1);
            points[targets[i].Id] = new WpfPoint(x, y0 + (y1 - y0) * t);
        }
    }

    /// <summary>Linear layout for N stages.</summary>
    public static FlowLayout CalculateProbe(IReadOnlyList<string> nodeIds, double width)
    {
        if (nodeIds.Count == 0)
            return new FlowLayout(200, new Dictionary<string, WpfPoint>());

        var narrow = width < WideBreakpoint;
        if (narrow)
        {
            const double top = 48;
            const double step = 96;
            var points = new Dictionary<string, WpfPoint>();
            for (var i = 0; i < nodeIds.Count; i++)
                points[nodeIds[i]] = new WpfPoint(0.5, top + i * step);
            return new FlowLayout(top + (nodeIds.Count - 1) * step + 64, points);
        }

        const double margin = 72;
        var usable = Math.Max(80, width - margin * 2);
        var pointsWide = new Dictionary<string, WpfPoint>();
        for (var i = 0; i < nodeIds.Count; i++)
        {
            var t = nodeIds.Count == 1 ? 0.5 : (double)i / (nodeIds.Count - 1);
            pointsWide[nodeIds[i]] = new WpfPoint(margin + usable * t, 140);
        }

        return new FlowLayout(280, pointsWide);
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
