namespace NetStrata.Core.Config;

using NetStrata.Core.Storage;

public sealed class NetStrataOptions
{
    public int IntervalMs { get; init; } = 60_000;
    public int Port { get; init; } = 8787;
    public string? ProxyOverride { get; init; }
    public IReadOnlyList<string> PingExtra { get; init; } = [];
    public IReadOnlyDictionary<string, string> PingExtraLabels { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> TlsStackTargets { get; init; } = [];
    public IReadOnlyList<string> HttpsExtra { get; init; } = [];
    public string Lang { get; init; } = "zh";
    public string Theme { get; init; } = "system";
    public int DownloadEvery { get; init; } = 10;
    public int ConclusionEvery { get; init; } = 30;
    public bool NoOpen { get; init; }
    public string DataDir { get; init; } = DataDirectory.DataPath;

    public static NetStrataOptions FromEnvironment()
    {
        var config = UserConfigLoader.Load(DataDirectory.ConfigPath);

        var pingExtra = MergePingExtra(config.PingExtra, Environment.GetEnvironmentVariable("NETSTRATA_PING_EXTRA"));
        return new NetStrataOptions
        {
            IntervalMs = ParseInt(Environment.GetEnvironmentVariable("NETSTRATA_INTERVAL_MS"), config.IntervalMs ?? 60_000),
            Port = ParseInt(Environment.GetEnvironmentVariable("NETSTRATA_PORT"), config.Port ?? 8787),
            ProxyOverride = NormalizeProxyOverride(Environment.GetEnvironmentVariable("NETSTRATA_PROXY")),
            PingExtra = pingExtra,
            PingExtraLabels = config.PingExtraLabels,
            TlsStackTargets = config.TlsStackTargets,
            HttpsExtra = config.HttpsExtra,
            Lang = Environment.GetEnvironmentVariable("NETSTRATA_LANG")
                   ?? config.Lang
                   ?? "zh",
            Theme = Environment.GetEnvironmentVariable("NETSTRATA_THEME")
                    ?? config.Theme
                    ?? "system",
            DownloadEvery = ParseInt(Environment.GetEnvironmentVariable("NETSTRATA_DOWNLOAD_EVERY"), 10),
            ConclusionEvery = ParseInt(Environment.GetEnvironmentVariable("NETSTRATA_CONCLUSION_EVERY"), 30),
            NoOpen = Environment.GetEnvironmentVariable("NETSTRATA_NO_OPEN") == "1",
            DataDir = DataDirectory.DataPath
        };
    }

    public static IReadOnlyList<string> MergePingExtra(
        IReadOnlyList<string> fromConfig,
        string? fromEnv,
        IReadOnlyList<string>? fromCli = null,
        Action<string>? onSkip = null) =>
        PingTargetValidator.Filter(
            fromConfig.Concat(ParseList(fromEnv)).Concat(fromCli ?? []),
            max: 10,
            onSkip);

    private static string? NormalizeProxyOverride(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        return v.Equals("none", StringComparison.OrdinalIgnoreCase)
               || v.Equals("off", StringComparison.OrdinalIgnoreCase)
            ? "__disabled__"
            : v;
    }

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, out var n) ? n : fallback;

    private static IReadOnlyList<string> ParseList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
