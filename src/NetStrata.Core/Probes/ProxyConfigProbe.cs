using System.Diagnostics;
using System.Net.NetworkInformation;
using NetStrata.Core.Models;
using NetStrata.Core.Proxy;

namespace NetStrata.Core.Probes;

public interface IPortProcessResolver
{
    string? GetListenerProcessName(int port);
}

public sealed class WindowsNetstatProcessResolver : IPortProcessResolver
{
    public string? GetListenerProcessName(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return null;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            int? pid = null;
            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                    continue;
                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5)
                    continue;
                if (!parts[1].EndsWith($":{port}"))
                    continue;
                if (int.TryParse(parts[^1], out var p))
                {
                    pid = p;
                    break;
                }
            }

            if (pid is null)
                return null;

            return Process.GetProcessById(pid.Value).ProcessName;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class ProxyConfigProbe
{
    private readonly IPortProcessResolver _processResolver;

    public ProxyConfigProbe(IPortProcessResolver? processResolver = null) =>
        _processResolver = processResolver ?? new WindowsNetstatProcessResolver();

    public ProxyConfig Probe(string? proxyUrl, SystemProxySettings? systemProxy = null)
    {
        var port = ProxyDetector.ParsePort(proxyUrl);
        var listening = port is not null && IsListening(port.Value);

        return new ProxyConfig
        {
            ProxyUrl = proxyUrl,
            ProxyPort = port,
            EnvHttp = Environment.GetEnvironmentVariable("http_proxy")
                       ?? Environment.GetEnvironmentVariable("HTTP_PROXY"),
            EnvHttps = Environment.GetEnvironmentVariable("https_proxy")
                        ?? Environment.GetEnvironmentVariable("HTTPS_PROXY"),
            EnvAll = Environment.GetEnvironmentVariable("all_proxy")
                      ?? Environment.GetEnvironmentVariable("ALL_PROXY"),
            SystemProxy = systemProxy ?? new SystemProxySettings(),
            Listening = listening,
            ListenerProcess = listening && port is not null
                ? _processResolver.GetListenerProcessName(port.Value)
                : null
        };
    }

    private static bool IsListening(int port) =>
        IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Any(ep => ep.Port == port);
}
