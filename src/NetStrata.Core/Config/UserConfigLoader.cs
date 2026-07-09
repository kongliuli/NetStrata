using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetStrata.Core.Config;

public sealed class UserConfig
{
    [JsonPropertyName("pingExtra")]
    public IReadOnlyList<string> PingExtra { get; init; } = [];

    [JsonPropertyName("pingExtraLabels")]
    public IReadOnlyDictionary<string, string> PingExtraLabels { get; init; } =
        new Dictionary<string, string>();
}

public static class UserConfigLoader
{
    public static UserConfig Load(string path)
    {
        if (!File.Exists(path))
            return new UserConfig();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UserConfig>(json) ?? new UserConfig();
        }
        catch
        {
            return new UserConfig();
        }
    }
}
