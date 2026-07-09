using Microsoft.Win32;
using NetStrata.Core.Models;

namespace NetStrata.Core.Proxy;

public sealed class WindowsRegistryProxyReader : ISystemProxyReader
{
    public SystemProxySettings Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
        if (key is null)
            return new SystemProxySettings();

        var enabled = (int)(key.GetValue("ProxyEnable") ?? 0) == 1;
        var server = key.GetValue("ProxyServer") as string;
        var bypass = key.GetValue("ProxyOverride") as string;

        ParseProxyServer(server, out var httpHost, out var httpPort, out var socksHost, out var socksPort);

        return new SystemProxySettings
        {
            HttpEnable = enabled && httpHost is not null,
            HttpProxy = httpHost,
            HttpPort = httpPort,
            HttpsEnable = enabled && httpHost is not null,
            HttpsProxy = httpHost,
            HttpsPort = httpPort,
            SocksEnable = enabled && socksHost is not null,
            SocksProxy = socksHost,
            SocksPort = socksPort,
            BypassList = bypass
        };
    }

    internal static void ParseProxyServer(
        string? server,
        out string? httpHost,
        out int? httpPort,
        out string? socksHost,
        out int? socksPort)
    {
        httpHost = null;
        httpPort = null;
        socksHost = null;
        socksPort = null;
        if (string.IsNullOrWhiteSpace(server))
            return;

        foreach (var segment in server.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = segment.Trim();
            if (part.Contains('='))
            {
                var kv = part.Split('=', 2);
                var scheme = kv[0].Trim().ToLowerInvariant();
                part = kv[1].Trim();
                ParseHostPort(part, out var host, out var port);
                if (scheme is "socks" or "socks5")
                {
                    socksHost = host;
                    socksPort = port;
                }
                else if (scheme is "http" or "https")
                {
                    httpHost ??= host;
                    httpPort ??= port;
                }
                continue;
            }

            ParseHostPort(part, out var h, out var p);
            httpHost ??= h;
            httpPort ??= p;
        }
    }

    private static void ParseHostPort(string part, out string? host, out int? port)
    {
        host = null;
        port = null;
        var idx = part.LastIndexOf(':');
        if (idx > 0 && int.TryParse(part[(idx + 1)..], out var p))
        {
            host = part[..idx];
            port = p;
        }
        else
            host = part;
    }
}
