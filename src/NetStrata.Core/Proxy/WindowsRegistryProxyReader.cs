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

        ParseProxyServer(server, out var host, out var port);

        return new SystemProxySettings
        {
            HttpEnable = enabled,
            HttpProxy = host,
            HttpPort = port,
            HttpsEnable = enabled,
            HttpsProxy = host,
            HttpsPort = port,
            BypassList = bypass
        };
    }

    private static void ParseProxyServer(string? server, out string? host, out int? port)
    {
        host = null;
        port = null;
        if (string.IsNullOrWhiteSpace(server))
            return;

        // http=host:port;https=host:port  or  host:port
        var part = server.Split(';')[0];
        if (part.Contains('='))
            part = part.Split('=')[1];

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
