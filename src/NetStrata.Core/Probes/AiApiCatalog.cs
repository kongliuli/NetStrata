namespace NetStrata.Core.Probes;

/// <summary>
/// AI / developer API catalog.
/// ProbeUrl = TLS/HTTP reachability (API host; 401/403/404 still counts as reachable).
/// OpenUrl = human-facing site opened from the UI (never dump users on bare API 404 pages).
/// </summary>
public static class AiApiCatalog
{
    public sealed record Provider(string Id, string DisplayName, string ProbeUrl, string OpenUrl);

    public static readonly Provider[] Providers =
    [
        new("openai", "ChatGPT", "https://api.openai.com/", "https://chatgpt.com/"),
        new("cursor", "Cursor", "https://api2.cursor.sh/", "https://cursor.com/"),
        new("opencode", "OpenCode", "https://opencode.ai/zen/v1/models", "https://opencode.ai/"),
        new("google", "Google AI", "https://generativelanguage.googleapis.com/", "https://aistudio.google.com/"),
        new("github", "GitHub", "https://api.github.com/", "https://github.com/"),
        new("anthropic", "Anthropic", "https://api.anthropic.com/", "https://claude.ai/")
    ];

    public static IEnumerable<HttpTarget> DirectTargets() =>
        Providers.Select(p => new HttpTarget($"{p.Id}_direct", p.ProbeUrl, "direct", AcceptAnyCode: true));

    public static IEnumerable<HttpTarget> ProxyTargets() =>
        Providers.Select(p => new HttpTarget($"{p.Id}_proxy", p.ProbeUrl, "proxy", AcceptAnyCode: true));

    public static readonly HashSet<string> AiLabels = new(
        Providers.SelectMany(p => new[] { $"{p.Id}_direct", $"{p.Id}_proxy" }),
        StringComparer.OrdinalIgnoreCase);

    public static bool IsAiLabel(string label) => AiLabels.Contains(label);

    public static Provider? Find(string providerId) =>
        Providers.FirstOrDefault(p => p.Id.Equals(providerId, StringComparison.OrdinalIgnoreCase));

    public static string DisplayName(string providerId) =>
        Find(providerId)?.DisplayName ?? providerId;

    public static string OpenUrl(string providerId) =>
        Find(providerId)?.OpenUrl ?? "https://example.com/";
}
