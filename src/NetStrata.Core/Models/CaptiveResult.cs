using System.Text.Json.Serialization;

namespace NetStrata.Core.Models;

public sealed record CaptiveResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("httpCode")]
    public int HttpCode { get; init; }

    [JsonPropertyName("bodyHead")]
    public string? BodyHead { get; init; }

    [JsonPropertyName("redirected")]
    public bool Redirected { get; init; }

    [JsonPropertyName("totalMs")]
    public double TotalMs { get; init; }
}

public sealed record ProxyDownload
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; init; }

    [JsonPropertyName("ms")]
    public double Ms { get; init; }

    [JsonPropertyName("mbps")]
    public double? Mbps { get; init; }

    [JsonPropertyName("err")]
    public string? Err { get; init; }
}
