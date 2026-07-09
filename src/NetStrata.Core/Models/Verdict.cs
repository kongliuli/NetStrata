using System.Text.Json.Serialization;

namespace NetStrata.Core.Models;

public sealed record LayerVerdict
{
    [JsonPropertyName("layer")]
    public required string Layer { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("reasons")]
    public IReadOnlyList<string> Reasons { get; init; } = [];

    [JsonPropertyName("metrics")]
    public IReadOnlyDictionary<string, object?> Metrics { get; init; } =
        new Dictionary<string, object?>();
}

public sealed record AiVerdict
{
    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("headline")]
    public required string Headline { get; init; }
}

public sealed record Verdict
{
    [JsonPropertyName("overall")]
    public required string Overall { get; init; }

    [JsonPropertyName("headline")]
    public required string Headline { get; init; }

    [JsonPropertyName("insights")]
    public IReadOnlyList<string> Insights { get; init; } = [];

    [JsonPropertyName("layers")]
    public IReadOnlyList<LayerVerdict> Layers { get; init; } = [];

    [JsonPropertyName("ai")]
    public required AiVerdict Ai { get; init; }
}
