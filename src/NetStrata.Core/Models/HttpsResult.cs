using System.Text.Json.Serialization;

namespace NetStrata.Core.Models;

public sealed record HttpsResult
{
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("via")]
    public required string Via { get; init; }

    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("httpCode")]
    public int HttpCode { get; init; }

    [JsonPropertyName("remoteIp")]
    public string? RemoteIp { get; init; }

    [JsonPropertyName("dnsMs")]
    public double DnsMs { get; init; }

    [JsonPropertyName("connectMs")]
    public double ConnectMs { get; init; }

    [JsonPropertyName("tlsMs")]
    public double TlsMs { get; init; }

    [JsonPropertyName("firstByteMs")]
    public double FirstByteMs { get; init; }

    [JsonPropertyName("totalMs")]
    public double TotalMs { get; init; }

    [JsonPropertyName("timedOut")]
    public bool TimedOut { get; init; }

    [JsonPropertyName("err")]
    public string? Err { get; init; }
}
