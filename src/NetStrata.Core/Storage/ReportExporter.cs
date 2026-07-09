using System.Text.Json;
using System.Text.Json.Serialization;
using NetStrata.Core.Models;

namespace NetStrata.Core.Storage;

public sealed record ExportReport
{
    [JsonPropertyName("generatedAt")]
    public required string GeneratedAt { get; init; }

    [JsonPropertyName("minutes")]
    public int Minutes { get; init; }

    [JsonPropertyName("sampleCount")]
    public int SampleCount { get; init; }

    [JsonPropertyName("from")]
    public string? From { get; init; }

    [JsonPropertyName("to")]
    public string? To { get; init; }

    [JsonPropertyName("overall")]
    public IReadOnlyDictionary<string, int> Overall { get; init; } = new Dictionary<string, int>();

    [JsonPropertyName("layers")]
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Layers { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, int>>();

    [JsonPropertyName("customPings")]
    public IReadOnlyList<CustomPingSummary> CustomPings { get; init; } = [];

    [JsonPropertyName("recentAlerts")]
    public IReadOnlyList<Alert> RecentAlerts { get; init; } = [];

    [JsonPropertyName("conclusionsExcerpt")]
    public string? ConclusionsExcerpt { get; init; }
}

public sealed record CustomPingSummary
{
    [JsonPropertyName("target")]
    public required string Target { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("ok")]
    public int Ok { get; init; }

    [JsonPropertyName("fail")]
    public int Fail { get; init; }

    [JsonPropertyName("avgMs")]
    public double? AvgMs { get; init; }
}

public sealed class ReportExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ExportReport Build(
        IReadOnlyList<Sample> samples,
        IReadOnlyList<Alert> recentAlerts,
        string? conclusionsMarkdown,
        int minutes,
        DateTime? now = null)
    {
        now ??= DateTime.UtcNow;
        var cutoff = now.Value.AddMinutes(-minutes);
        var filtered = FilterByTime(samples, cutoff).ToList();

        var overall = filtered
            .Where(s => s.Verdict is not null)
            .GroupBy(s => s.Verdict!.Overall)
            .ToDictionary(g => g.Key, g => g.Count());

        var layerNames = filtered
            .SelectMany(s => s.Verdict?.Layers ?? [])
            .Select(l => l.Layer)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var layers = new Dictionary<string, IReadOnlyDictionary<string, int>>();
        foreach (var layer in layerNames)
        {
            layers[layer] = filtered
                .Select(s => s.Verdict?.Layers.FirstOrDefault(l =>
                    l.Layer.Equals(layer, StringComparison.OrdinalIgnoreCase))?.State)
                .Where(s => s is not null)
                .GroupBy(s => s!)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        var customPings = filtered
            .SelectMany(s => s.Pings.Where(p => p.Custom))
            .GroupBy(p => p.Target, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var withAvg = g.Where(p => p.AvgMs.HasValue).ToList();
                return new CustomPingSummary
                {
                    Target = g.Key,
                    Label = g.First().Label,
                    Ok = g.Count(p => p.Ok),
                    Fail = g.Count(p => !p.Ok),
                    AvgMs = withAvg.Count > 0 ? withAvg.Average(p => p.AvgMs!.Value) : null
                };
            })
            .OrderBy(p => p.Target)
            .ToList();

        var alerts = recentAlerts
            .Where(a => TryParse(a.T, out var t) && t >= cutoff)
            .TakeLast(20)
            .ToList();

        return new ExportReport
        {
            GeneratedAt = now.Value.ToString("o"),
            Minutes = minutes,
            SampleCount = filtered.Count,
            From = filtered.FirstOrDefault()?.T,
            To = filtered.LastOrDefault()?.T,
            Overall = overall,
            Layers = layers,
            CustomPings = customPings,
            RecentAlerts = alerts,
            ConclusionsExcerpt = Excerpt(conclusionsMarkdown, 800)
        };
    }

    public string ToMarkdown(ExportReport report)
    {
        var lines = new List<string>
        {
            "# NetStrata 诊断报告",
            "",
            $"生成时间: {report.GeneratedAt}",
            $"范围: 最近 {report.Minutes} 分钟 · {report.SampleCount} 条样本",
            $"时间: {report.From} → {report.To}",
            "",
            "## Overall 分布",
            ""
        };

        foreach (var (k, v) in report.Overall.OrderByDescending(x => x.Value))
            lines.Add($"- {k}: {v}");

        lines.Add("");
        lines.Add("## 各层状态");
        lines.Add("");
        foreach (var (layer, stats) in report.Layers.OrderBy(x => x.Key))
        {
            lines.Add($"### {layer}");
            foreach (var (state, count) in stats.OrderByDescending(x => x.Value))
                lines.Add($"- {state}: {count}");
            lines.Add("");
        }

        if (report.CustomPings.Count > 0)
        {
            lines.Add("## 自定义 Ping");
            lines.Add("");
            foreach (var p in report.CustomPings)
            {
                var name = string.IsNullOrWhiteSpace(p.Label) ? p.Target : $"{p.Label} ({p.Target})";
                var avg = p.AvgMs is null ? "n/a" : $"{p.AvgMs:F1}ms";
                lines.Add($"- {name}: ok={p.Ok} fail={p.Fail} avg={avg}");
            }
            lines.Add("");
        }

        if (report.RecentAlerts.Count > 0)
        {
            lines.Add("## 近期告警");
            lines.Add("");
            foreach (var a in report.RecentAlerts)
                lines.Add($"- [{a.Type}] {a.Message}");
            lines.Add("");
        }

        if (!string.IsNullOrWhiteSpace(report.ConclusionsExcerpt))
        {
            lines.Add("## 结论节选");
            lines.Add("");
            lines.Add(report.ConclusionsExcerpt);
            lines.Add("");
        }

        return string.Join('\n', lines);
    }

    public string ToJson(ExportReport report) =>
        JsonSerializer.Serialize(report, JsonOptions);

    private static IEnumerable<Sample> FilterByTime(IReadOnlyList<Sample> samples, DateTime cutoff) =>
        samples.Where(s => TryParse(s.T, out var t) && t >= cutoff);

    private static bool TryParse(string value, out DateTime time) =>
        DateTime.TryParse(value, out time);

    private static string? Excerpt(string? markdown, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return null;
        var trimmed = markdown.Trim();
        return trimmed.Length <= maxChars ? trimmed : trimmed[..maxChars] + "…";
    }
}
