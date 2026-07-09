using System.Text.Json.Serialization;

namespace NetStrata.Core.Models;

public sealed record WifiInfo
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "unknown";

    [JsonPropertyName("device")]
    public string? Device { get; init; }

    [JsonPropertyName("ssid")]
    public string? Ssid { get; init; }

    [JsonPropertyName("ssidRedacted")]
    public bool SsidRedacted { get; init; }

    [JsonPropertyName("bssid")]
    public string? Bssid { get; init; }

    [JsonPropertyName("channel")]
    public int? Channel { get; init; }

    [JsonPropertyName("band")]
    public string? Band { get; init; }

    [JsonPropertyName("rssi")]
    public int? Rssi { get; init; }

    [JsonPropertyName("noise")]
    public int? Noise { get; init; }

    [JsonPropertyName("txRate")]
    public int? TxRate { get; init; }

    [JsonPropertyName("phyMode")]
    public string? PhyMode { get; init; }

    [JsonPropertyName("security")]
    public string? Security { get; init; }
}
