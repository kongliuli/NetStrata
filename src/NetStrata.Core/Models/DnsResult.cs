using System.Text.Json.Serialization;

namespace NetStrata.Core.Models;

public sealed record DnsResult
{
    [JsonPropertyName("server")]
    public required string Server { get; init; }

    [JsonPropertyName("domain")]
    public required string Domain { get; init; }

    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("ms")]
    public double Ms { get; init; }

    [JsonPropertyName("ips")]
    public IReadOnlyList<string> Ips { get; init; } = [];

    [JsonPropertyName("flags")]
    public string? Flags { get; init; }

    [JsonPropertyName("err")]
    public string? Err { get; init; }
}
