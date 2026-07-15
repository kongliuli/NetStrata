using System.Text.Json;
using System.Text.Json.Serialization;
using NetStrata.Core.Ui;

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

    /// <summary>Extra HTTPS URLs probed each cycle (openable in browser).</summary>
    [JsonPropertyName("httpsExtra")]
    public IReadOnlyList<string> HttpsExtra { get; init; } = [];

    /// <summary>zh | en | auto (default Chinese).</summary>
    [JsonPropertyName("lang")]
    public string? Lang { get; init; }

    /// <summary>system | light | dark.</summary>
    [JsonPropertyName("theme")]
    public string? Theme { get; init; }

    [JsonPropertyName("judge")]
    public JudgeConfig? Judge { get; init; }
}

public static class UserConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
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
    public string Lang { get; init; } = "zh";
    public string Theme { get; init; } = "system";
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
            TlsStackTargets = string.Join(Environment.NewLine, config.TlsStackTargets),
            Lang = string.IsNullOrWhiteSpace(config.Lang) ? "zh" : config.Lang!,
            Theme = string.IsNullOrWhiteSpace(config.Theme) ? "system" : config.Theme!
        };
    }

    public static UserConfig FromForm(SettingsFormModel form, UserConfig? existing = null)
    {
        int? interval = int.TryParse(form.IntervalMs.Trim(), out var iv) ? iv : null;
        // port is reserved for future web dashboard — keep existing, do not edit from Settings
        var port = existing?.Port;

        // ponytail: empty PingExtra in form means "keep existing" (Settings UI no longer edits targets)
        IReadOnlyList<string> pingExtra;
        IReadOnlyDictionary<string, string> labels;
        if (string.IsNullOrWhiteSpace(form.PingExtra) && string.IsNullOrWhiteSpace(form.PingLabels) && existing is not null)
        {
            pingExtra = existing.PingExtra;
            labels = existing.PingExtraLabels;
        }
        else
        {
            pingExtra = form.PingExtra
                .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in form.PingLabels.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
            {
                var part = line.Trim();
                var idx = part.IndexOf('=');
                if (idx <= 0)
                    continue;
                parsed[part[..idx].Trim()] = part[(idx + 1)..].Trim();
            }

            labels = parsed;
        }

        var tls = form.TlsStackTargets
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var lang = form.Lang.Trim().ToLowerInvariant() switch
        {
            "en" => "en",
            "auto" => "auto",
            _ => "zh"
        };

        return new UserConfig
        {
            IntervalMs = interval,
            Port = port,
            PingExtra = pingExtra,
            PingExtraLabels = labels,
            TlsStackTargets = tls,
            HttpsExtra = existing?.HttpsExtra ?? [],
            Lang = lang,
            Theme = ThemeResolver.ToConfig(ThemeResolver.Parse(form.Theme)),
            Judge = existing?.Judge
        };
    }
}
