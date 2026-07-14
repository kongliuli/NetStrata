using System.Diagnostics;

namespace NetStrata.Core.Cli;

public interface IBrowserLauncher
{
    void Open(string url);
}

/// <summary>
/// Opens http(s) URLs via the OS default browser. Unit tests inject a fake.
/// </summary>
public sealed class ShellBrowserLauncher : IBrowserLauncher
{
    private readonly Action<string> _open;

    public ShellBrowserLauncher(Action<string>? open = null) =>
        _open = open ?? OpenDefault;

    public void Open(string url)
    {
        if (!TryNormalizeUrl(url, out var normalized))
            throw new ArgumentException("invalid url", nameof(url));
        _open(normalized);
    }

    public static bool TryNormalizeUrl(string? url, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(url))
            return false;
        var u = url.Trim();
        if (u.Contains(' ') || u.Contains(":::"))
            return false;
        if (u.Contains("://", StringComparison.Ordinal)
            && !u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            u = "https://" + u;
        if (!Uri.TryCreate(u, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme is not ("http" or "https"))
            return false;
        if (string.IsNullOrWhiteSpace(uri.Host))
            return false;
        normalized = uri.AbsoluteUri;
        return true;
    }

    private static void OpenDefault(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
