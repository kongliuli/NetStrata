using NetStrata.Core.Models;

namespace NetStrata.Core.Proxy;

public interface ISystemProxyReader
{
    SystemProxySettings Read();
}

public sealed class ProxyDetector
{
    public static readonly int[] FallbackPorts = [7890, 7897, 10809, 1080, 7891];

    private readonly ISystemProxyReader? _registryReader;

    public ProxyDetector(ISystemProxyReader? registryReader = null) =>
        _registryReader = registryReader;

    public string? Detect(string? overrideValue = null)
    {
        if (overrideValue == "__disabled__")
            return null;

        if (!string.IsNullOrWhiteSpace(overrideValue))
            return overrideValue.Trim();

        foreach (var key in new[] { "https_proxy", "HTTPS_PROXY", "http_proxy", "HTTP_PROXY", "all_proxy", "ALL_PROXY" })
        {
            var env = Environment.GetEnvironmentVariable(key);
            var url = NormalizeEnvProxy(env);
            if (url is not null)
                return url;
        }

        var system = _registryReader?.Read();
        if (system is { HttpEnable: true } && !string.IsNullOrEmpty(system.HttpProxy))
        {
            var port = system.HttpPort ?? 80;
            return $"http://{system.HttpProxy}:{port}";
        }

        foreach (var port in FallbackPorts)
        {
            if (IsPortListening(port))
                return $"http://127.0.0.1:{port}";
        }

        return null;
    }

    public static int? ParsePort(string? proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl))
            return null;
        if (Uri.TryCreate(proxyUrl, UriKind.Absolute, out var uri))
            return uri.Port;
        return null;
    }

    private static string? NormalizeEnvProxy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        if (!v.Contains("://", StringComparison.Ordinal))
            v = "http://" + v;
        return v;
    }

    private static bool IsPortListening(int port)
    {
        try
        {
            return System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(ep => ep.Port == port);
        }
        catch
        {
            return false;
        }
    }
}
