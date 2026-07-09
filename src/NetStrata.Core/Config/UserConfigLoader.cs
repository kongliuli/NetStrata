using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetStrata.Core.Config;

public sealed class UserConfig
{
    [JsonPropertyName("intervalMs")]
    public int? IntervalMs { get; init; }

    [JsonPropertyName("port")]
    public int? Port { get; init; }

    [JsonPropertyName("pingExtra")]
    public IReadOnlyList<string> PingExtra { get; init; } = [];

    [JsonPropertyName("pingExtraLabels")]
    public IReadOnlyDictionary<string, string> PingExtraLabels { get; init; } =
        new Dictionary<string, string>();

    [JsonPropertyName("tlsStackTargets")]
    public IReadOnlyList<string> TlsStackTargets { get; init; } = [];
}

public static class UserConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static UserConfig Load(string path)
    {
        if (!File.Exists(path))
            return new UserConfig();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UserConfig>(json, JsonOptions) ?? new UserConfig();
        }
        catch
        {
            return new UserConfig();
        }
    }

    public static void Save(string path, UserConfig config)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
    }
}

public sealed record SettingsFormModel
{
    public string IntervalMs { get; init; } = "60000";
    public string Port { get; init; } = "8787";
    public string PingExtra { get; init; } = "";
    public string PingLabels { get; init; } = "";
    public string TlsStackTargets { get; init; } = "";
}

public static class SettingsMapper
{
    public static SettingsFormModel ToForm(UserConfig config)
    {
        var labels = config.PingExtraLabels
            .Select(kv => $"{kv.Key}={kv.Value}");
        return new SettingsFormModel
        {
            IntervalMs = (config.IntervalMs ?? 60_000).ToString(),
            Port = (config.Port ?? 8787).ToString(),
            PingExtra = string.Join(", ", config.PingExtra),
            PingLabels = string.Join(Environment.NewLine, labels),
            TlsStackTargets = string.Join(Environment.NewLine, config.TlsStackTargets)
        };
    }

    public static UserConfig FromForm(SettingsFormModel form)
    {
        int? interval = int.TryParse(form.IntervalMs.Trim(), out var iv) ? iv : null;
        int? port = int.TryParse(form.Port.Trim(), out var pv) ? pv : null;

        var pingExtra = form.PingExtra
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in form.PingLabels.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var part = line.Trim();
            var idx = part.IndexOf('=');
            if (idx <= 0)
                continue;
            labels[part[..idx].Trim()] = part[(idx + 1)..].Trim();
        }

        var tls = form.TlsStackTargets
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return new UserConfig
        {
            IntervalMs = interval,
            Port = port,
            PingExtra = pingExtra,
            PingExtraLabels = labels,
            TlsStackTargets = tls
        };
    }
}
