using System.Text.Json.Serialization;

namespace NetStrata.Core.Models;

public sealed record TlsStackLayerResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("ms")]
    public double Ms { get; init; }

    [JsonPropertyName("err")]
    public string? Err { get; init; }

    [JsonPropertyName("ips")]
    public IReadOnlyList<string>? Ips { get; init; }

    [JsonPropertyName("httpCode")]
    public int? HttpCode { get; init; }
}

public sealed record TlsStackResult
{
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("host")]
    public required string Host { get; init; }

    [JsonPropertyName("port")]
    public int Port { get; init; } = 443;

    [JsonPropertyName("dns")]
    public TlsStackLayerResult? Dns { get; init; }

    [JsonPropertyName("tcp")]
    public TlsStackLayerResult? Tcp { get; init; }

    [JsonPropertyName("tls")]
    public TlsStackLayerResult? Tls { get; init; }

    [JsonPropertyName("http")]
    public TlsStackLayerResult? Http { get; init; }

    [JsonPropertyName("verdict")]
    public required string Verdict { get; init; }

    [JsonPropertyName("stoppedAt")]
    public string? StoppedAt { get; init; }
}
