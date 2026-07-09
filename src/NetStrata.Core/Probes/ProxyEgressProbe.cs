using System.Diagnostics;
using System.Net;
using System.Net.Http;
using NetStrata.Core.Models;

namespace NetStrata.Core.Probes;

public sealed class ProxyEgressProbe
{
    private static readonly string[] Urls =
    [
        "https://api.ipify.org",
        "https://ifconfig.me/ip"
    ];

    public async Task<ProxyEgress> ProbeAsync(string proxyUrl, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var handler = new SocketsHttpHandler
        {
            Proxy = new WebProxy(proxyUrl),
            UseProxy = true,
            ConnectTimeout = TimeSpan.FromSeconds(8)
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };

        foreach (var url in Urls)
        {
            try
            {
                var ip = (await client.GetStringAsync(url, ct)).Trim();
                sw.Stop();
                return new ProxyEgress { Ok = true, Ip = ip, Ms = sw.Elapsed.TotalMilliseconds };
            }
            catch (Exception ex)
            {
                if (url == Urls[^1])
                {
                    sw.Stop();
                    return new ProxyEgress { Ok = false, Ms = sw.Elapsed.TotalMilliseconds, Err = ex.Message };
                }
            }
        }

        sw.Stop();
        return new ProxyEgress { Ok = false, Ms = sw.Elapsed.TotalMilliseconds, Err = "no egress url" };
    }
}
