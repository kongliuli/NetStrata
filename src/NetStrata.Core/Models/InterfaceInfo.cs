using System.Text.Json.Serialization;

namespace NetStrata.Core.Models;

public sealed record InterfaceInfo
{
    [JsonPropertyName("primaryService")]
    public string? PrimaryService { get; init; }

    [JsonPropertyName("primaryDevice")]
    public string? PrimaryDevice { get; init; }

    [JsonPropertyName("hardwarePort")]
    public string? HardwarePort { get; init; }

    [JsonPropertyName("linkType")]
    public string LinkType { get; init; } = "unknown";

    [JsonPropertyName("ipv4")]
    public string? Ipv4 { get; init; }

    [JsonPropertyName("gateway")]
    public string? Gateway { get; init; }

    [JsonPropertyName("subnetMask")]
    public string? SubnetMask { get; init; }

    [JsonPropertyName("dhcpServer")]
    public string? DhcpServer { get; init; }

    [JsonPropertyName("dhcpDns")]
    public IReadOnlyList<string> DhcpDns { get; init; } = [];

    [JsonPropertyName("routeHints")]
    public IReadOnlyList<string> RouteHints { get; init; } = [];
}
