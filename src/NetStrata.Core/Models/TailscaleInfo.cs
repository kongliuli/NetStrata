using System.Text.Json.Serialization;

namespace NetStrata.Core.Models;

public sealed record TailscaleInfo
{
    [JsonPropertyName("installed")]
    public bool Installed { get; init; }

    [JsonPropertyName("signedIn")]
    public bool SignedIn { get; init; }

    [JsonPropertyName("exitNodeActive")]
    public bool ExitNodeActive { get; init; }

    [JsonPropertyName("address")]
    public string? Address { get; init; }
}
