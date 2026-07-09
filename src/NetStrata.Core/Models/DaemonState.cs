using System.Text.Json.Serialization;
using NetStrata.Core.Models;

namespace NetStrata.Core.Models;

public sealed record DaemonState
{
    [JsonPropertyName("startedAt")]
    public required string StartedAt { get; init; }

    [JsonPropertyName("cycle")]
    public int Cycle { get; init; }

    [JsonPropertyName("latest")]
    public Sample? Latest { get; init; }

    [JsonPropertyName("recentAlerts")]
    public IReadOnlyList<Alert> RecentAlerts { get; init; } = [];

    [JsonPropertyName("rolling")]
    public RollingStats Rolling { get; init; } = new();
}

public sealed record RollingStats
{
    [JsonPropertyName("last20Overall")]
    public IReadOnlyDictionary<string, int> Last20Overall { get; init; } =
        new Dictionary<string, int>();
}

public sealed record Alert
{
    [JsonPropertyName("t")]
    public required string T { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("prev")]
    public string? Prev { get; init; }

    [JsonPropertyName("curr")]
    public string? Curr { get; init; }
}

public sealed record SeriesData
{
    [JsonPropertyName("t")]
    public IReadOnlyList<string> T { get; init; } = [];

    [JsonPropertyName("verdict")]
    public IReadOnlyList<string?> Verdict { get; init; } = [];

    [JsonPropertyName("pings")]
    public IReadOnlyDictionary<string, IReadOnlyList<double?>> Pings { get; init; } =
        new Dictionary<string, IReadOnlyList<double?>>();

    [JsonPropertyName("https")]
    public IReadOnlyDictionary<string, HttpsSeries> Https { get; init; } =
        new Dictionary<string, HttpsSeries>();

    [JsonPropertyName("layers")]
    public IReadOnlyDictionary<string, IReadOnlyList<string?>> Layers { get; init; } =
        new Dictionary<string, IReadOnlyList<string?>>();

    [JsonPropertyName("ai")]
    public AiSeries Ai { get; init; } = new();
}

public sealed record HttpsSeries
{
    [JsonPropertyName("totalMs")]
    public IReadOnlyList<double?> TotalMs { get; init; } = [];

    [JsonPropertyName("ok")]
    public IReadOnlyList<bool?> Ok { get; init; } = [];

    [JsonPropertyName("timedOut")]
    public IReadOnlyList<bool?> TimedOut { get; init; } = [];
}

public sealed record AiSeries
{
    [JsonPropertyName("state")]
    public IReadOnlyList<string?> State { get; init; } = [];
}
