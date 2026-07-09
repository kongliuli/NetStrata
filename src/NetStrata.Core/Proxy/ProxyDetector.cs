using NetStrata.Core.Models;

namespace NetStrata.Core.Proxy;

public interface ISystemProxyReader
{
    SystemProxySettings Read();
}

public sealed class ProxyDetector
{
    public static readonly int[] FallbackPorts = [7890, 7897, 10809, 1080, 7891];
    public static readonly int[] SocksFallbackPorts = [1080, 10808];

    private readonly ISystemProxyReader? _registryReader;

    public ProxyDetector(ISystemProxyReader? registryReader = null) =>
        _registryReader = registryReader;

    public string? Detect(string? overrideValue = null)
    {
        if (overrideValue == "__disabled__")
            return null;

        if (!string.IsNullOrWhiteSpace(overrideValue))
            return overrideValue.Trim();

        foreach (var key in new[] { "https_proxy", "HTTPS_PROXY", "http_proxy", "HTTP_PROXY" })
        {
            var url = NormalizeEnvProxy(Environment.GetEnvironmentVariable(key));
            if (url is not null)
                return url;
        }

        foreach (var key in new[] { "all_proxy", "ALL_PROXY", "socks_proxy", "SOCKS_PROXY" })
        {
            var url = NormalizeSocksProxy(Environment.GetEnvironmentVariable(key));
            if (url is not null)
                return url;
        }

        var system = _registryReader?.Read();
        if (system is { HttpEnable: true } && !string.IsNullOrEmpty(system.HttpProxy))
        {
            var port = system.HttpPort ?? 80;
            return $"http://{system.HttpProxy}:{port}";
        }

        if (system is { SocksEnable: true } && !string.IsNullOrEmpty(system.SocksProxy))
        {
            var port = system.SocksPort ?? 1080;
            return $"socks5://{system.SocksProxy}:{port}";
        }

        foreach (var port in FallbackPorts)
        {
            if (IsPortListening(port))
                return $"http://127.0.0.1:{port}";
        }

        foreach (var port in SocksFallbackPorts)
        {
            if (IsPortListening(port))
                return $"socks5://127.0.0.1:{port}";
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
        if (v.StartsWith("socks", StringComparison.OrdinalIgnoreCase))
            return NormalizeSocksProxy(v);
        if (!v.Contains("://", StringComparison.Ordinal))
            v = "http://" + v;
        return v;
    }

    private static string? NormalizeSocksProxy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        if (!v.Contains("://", StringComparison.Ordinal))
            v = "socks5://" + v;
        if (!v.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase)
            && !v.StartsWith("socks://", StringComparison.OrdinalIgnoreCase))
            return null;
        if (v.StartsWith("socks://", StringComparison.OrdinalIgnoreCase))
            v = "socks5://" + v["socks://".Length..];
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
