using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace NetStrata.Core.Config;

public static partial class PingTargetValidator
{
    public static bool IsValid(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return false;

        var t = target.Trim();
        if (IPAddress.TryParse(t, out var ip))
            return ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6;

        return t.Length <= 253 && HostnameRegex().IsMatch(t);
    }

    public static IReadOnlyList<string> Filter(
        IEnumerable<string> targets,
        int max,
        Action<string>? onSkip = null)
    {
        var merged = new List<string>();
        foreach (var raw in targets)
        {
            var t = raw?.Trim();
            if (string.IsNullOrWhiteSpace(t))
                continue;

            if (!IsValid(t))
            {
                onSkip?.Invoke($"invalid ping target skipped: {t}");
                continue;
            }

            if (merged.Contains(t, StringComparer.OrdinalIgnoreCase))
                continue;

            if (merged.Count >= max)
            {
                onSkip?.Invoke($"ping extra cap {max} reached, skipping {t}");
                break;
            }

            merged.Add(t);
        }

        return merged;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9]([a-zA-Z0-9\-\.]*[a-zA-Z0-9])?$")]
    private static partial Regex HostnameRegex();
}
