using NetStrata.Core.Models;

namespace NetStrata.Core.Storage;

/// <summary>
/// Filters samples by time window and builds chart-ready series (for LiveCharts / export).
/// </summary>
public static class TrendWindow
{
    public static IReadOnlyList<Sample> Filter(IReadOnlyList<Sample> samples, TimeSpan window, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var cutoff = now - window;
        return samples
            .Where(s => DateTime.TryParse(s.T, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)
                        && t.ToUniversalTime() >= cutoff)
            .ToList();
    }

    public static int SuggestedTailLimit(TimeSpan window, int intervalMs = 60_000) =>
        Math.Clamp((int)(window.TotalMilliseconds / Math.Max(1, intervalMs)) + 20, 30, 2000);
}

public sealed class TrendChartModel
{
    public IReadOnlyList<string> Labels { get; init; } = [];
    public IReadOnlyList<double?> GatewayMs { get; init; } = [];
    public IReadOnlyList<double?> DomesticMs { get; init; } = [];
    public IReadOnlyList<double?> OverseasMs { get; init; } = [];
    public IReadOnlyDictionary<string, IReadOnlyList<string?>> LayerStates { get; init; } =
        new Dictionary<string, IReadOnlyList<string?>>();
}

public static class TrendChartBuilder
{
    public static TrendChartModel Build(IReadOnlyList<Sample> samples)
    {
        var series = new SeriesBuilder().Build(samples);
        return new TrendChartModel
        {
            Labels = series.T,
            GatewayMs = series.Pings.TryGetValue("gw", out var gw) ? gw : [],
            DomesticMs = series.Pings.TryGetValue("ali", out var ali) ? ali : [],
            OverseasMs = series.Pings.TryGetValue("cf", out var cf) ? cf : [],
            LayerStates = series.Layers
        };
    }
}
