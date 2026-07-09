using System.Text.Json.Serialization;

namespace NetStrata.Core.Models;

public sealed record SystemProxySettings
{
    [JsonPropertyName("httpEnable")]
    public bool HttpEnable { get; init; }

    [JsonPropertyName("httpProxy")]
    public string? HttpProxy { get; init; }

    [JsonPropertyName("httpPort")]
    public int? HttpPort { get; init; }

    [JsonPropertyName("httpsEnable")]
    public bool HttpsEnable { get; init; }

    [JsonPropertyName("httpsProxy")]
    public string? HttpsProxy { get; init; }

    [JsonPropertyName("httpsPort")]
    public int? HttpsPort { get; init; }

    [JsonPropertyName("socksEnable")]
    public bool SocksEnable { get; init; }

    [JsonPropertyName("socksProxy")]
    public string? SocksProxy { get; init; }

    [JsonPropertyName("socksPort")]
    public int? SocksPort { get; init; }

    [JsonPropertyName("autoDetect")]
    public bool AutoDetect { get; init; }

    [JsonPropertyName("bypassList")]
    public string? BypassList { get; init; }
}

public sealed record ProxyConfig
{
    [JsonPropertyName("proxyUrl")]
    public string? ProxyUrl { get; init; }

    [JsonPropertyName("proxyPort")]
    public int? ProxyPort { get; init; }

    [JsonPropertyName("envHttp")]
    public string? EnvHttp { get; init; }

    [JsonPropertyName("envHttps")]
    public string? EnvHttps { get; init; }

    [JsonPropertyName("envAll")]
    public string? EnvAll { get; init; }

    [JsonPropertyName("systemProxy")]
    public SystemProxySettings SystemProxy { get; init; } = new();

    [JsonPropertyName("listening")]
    public bool Listening { get; init; }

    [JsonPropertyName("listenerProcess")]
    public string? ListenerProcess { get; init; }
}

public sealed record ProxyEgress
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("ip")]
    public string? Ip { get; init; }

    [JsonPropertyName("ms")]
    public double Ms { get; init; }

    [JsonPropertyName("err")]
    public string? Err { get; init; }
}
