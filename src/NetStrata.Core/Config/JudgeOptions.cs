namespace NetStrata.Core.Config;

/// <summary>Configurable anchors and thresholds for VerdictEngine (defaults = historical hardcodes).</summary>
public sealed class JudgeOptions
{
    public string DomesticPingTarget { get; init; } = "223.5.5.5";
    public string DomesticHttpsLabel { get; init; } = "baidu_direct";
    public string DomesticDnsDomain { get; init; } = "baidu.com";
    public string DomesticDnsServer { get; init; } = "223.5.5.5";
    public int WifiRssiFailDbm { get; init; } = -80;
    public int WifiRssiDegradedDbm { get; init; } = -70;
    public int WifiTxRateDegradedMbps { get; init; } = 50;
    public double LanGatewayDegradedMs { get; init; } = 30;
    public double BroadbandHttpsDegradedMs { get; init; } = 1500;

    public static JudgeOptions Default { get; } = new();

    public static JudgeOptions FromConfig(JudgeConfig? cfg)
    {
        if (cfg is null)
            return Default;
        return new JudgeOptions
        {
            DomesticPingTarget = NullIfEmpty(cfg.DomesticPingTarget) ?? Default.DomesticPingTarget,
            DomesticHttpsLabel = NullIfEmpty(cfg.DomesticHttpsLabel) ?? Default.DomesticHttpsLabel,
            DomesticDnsDomain = NullIfEmpty(cfg.DomesticDnsDomain) ?? Default.DomesticDnsDomain,
            DomesticDnsServer = NullIfEmpty(cfg.DomesticDnsServer) ?? Default.DomesticDnsServer,
            WifiRssiFailDbm = cfg.WifiRssiFailDbm ?? Default.WifiRssiFailDbm,
            WifiRssiDegradedDbm = cfg.WifiRssiDegradedDbm ?? Default.WifiRssiDegradedDbm,
            WifiTxRateDegradedMbps = cfg.WifiTxRateDegradedMbps ?? Default.WifiTxRateDegradedMbps,
            LanGatewayDegradedMs = cfg.LanGatewayDegradedMs ?? Default.LanGatewayDegradedMs,
            BroadbandHttpsDegradedMs = cfg.BroadbandHttpsDegradedMs ?? Default.BroadbandHttpsDegradedMs
        };
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public sealed class JudgeConfig
{
    public string? DomesticPingTarget { get; init; }
    public string? DomesticHttpsLabel { get; init; }
    public string? DomesticDnsDomain { get; init; }
    public string? DomesticDnsServer { get; init; }
    public int? WifiRssiFailDbm { get; init; }
    public int? WifiRssiDegradedDbm { get; init; }
    public int? WifiTxRateDegradedMbps { get; init; }
    public double? LanGatewayDegradedMs { get; init; }
    public double? BroadbandHttpsDegradedMs { get; init; }
}
