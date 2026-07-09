using System.Text.Json.Serialization;

namespace NetStrata.Core.Models;

public sealed record PingResult
{
    [JsonPropertyName("target")]
    public required string Target { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("custom")]
    public bool Custom { get; init; }

    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("lossPct")]
    public double LossPct { get; init; }

    [JsonPropertyName("sent")]
    public int Sent { get; init; }

    [JsonPropertyName("received")]
    public int Received { get; init; }

    [JsonPropertyName("minMs")]
    public double? MinMs { get; init; }

    [JsonPropertyName("avgMs")]
    public double? AvgMs { get; init; }

    [JsonPropertyName("maxMs")]
    public double? MaxMs { get; init; }

    [JsonPropertyName("stddevMs")]
    public double? StddevMs { get; init; }

    [JsonPropertyName("err")]
    public string? Err { get; init; }
}
