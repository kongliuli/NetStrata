using System.Text.Json.Serialization;

namespace NetStrata.Core.Models;

public sealed record Sample
{
    [JsonPropertyName("t")]
    public required string T { get; init; }

    /// <summary>daemon | manual — who produced this sample.</summary>
    [JsonPropertyName("trigger")]
    public string Trigger { get; init; } = "daemon";

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

    [JsonPropertyName("captive")]
    public CaptiveResult? Captive { get; init; }

    [JsonPropertyName("proxyDownload")]
    public ProxyDownload? ProxyDownload { get; init; }

    [JsonPropertyName("tailscale")]
    public TailscaleInfo? Tailscale { get; init; }

    [JsonPropertyName("tlsStack")]
    public IReadOnlyList<TlsStackResult>? TlsStack { get; init; }

    [JsonPropertyName("alerts")]
    public IReadOnlyList<Alert> Alerts { get; init; } = [];

    [JsonPropertyName("verdict")]
    public Verdict? Verdict { get; init; }
}
