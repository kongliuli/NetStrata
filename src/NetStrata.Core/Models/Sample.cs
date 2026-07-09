using System.Text.Json.Serialization;

namespace NetStrata.Core.Models;

public sealed record Sample
{
    [JsonPropertyName("t")]
    public required string T { get; init; }

    [JsonPropertyName("cycleMs")]
    public double CycleMs { get; init; }

    [JsonPropertyName("wifi")]
    public WifiInfo? Wifi { get; init; }

    [JsonPropertyName("iface")]
    public InterfaceInfo? Iface { get; init; }

    [JsonPropertyName("dns")]
    public IReadOnlyList<DnsResult> Dns { get; init; } = [];

    [JsonPropertyName("pings")]
    public IReadOnlyList<PingResult> Pings { get; init; } = [];

    [JsonPropertyName("https")]
    public IReadOnlyList<HttpsResult> Https { get; init; } = [];

    [JsonPropertyName("proxyConfig")]
    public required ProxyConfig ProxyConfig { get; init; }

    [JsonPropertyName("proxyEgress")]
    public ProxyEgress? ProxyEgress { get; init; }

    [JsonPropertyName("verdict")]
    public Verdict? VerdictResult { get; init; }
}
