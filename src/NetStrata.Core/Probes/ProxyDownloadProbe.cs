using System.Diagnostics;
using System.Net;
using System.Net.Http;
using NetStrata.Core.Models;

namespace NetStrata.Core.Probes;

public sealed class ProxyDownloadProbe
{
    private const string Url = "https://speed.cloudflare.com/__down?bytes=5000000";
    private const int TargetBytes = 5_000_000;

    public async Task<ProxyDownload> ProbeAsync(string proxyUrl, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var handler = new SocketsHttpHandler
            {
                Proxy = new WebProxy(proxyUrl),
                UseProxy = true,
                ConnectTimeout = TimeSpan.FromSeconds(15)
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            using var response = await client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            sw.Stop();

            var mbps = sw.Elapsed.TotalSeconds > 0
                ? bytes.Length * 8.0 / sw.Elapsed.TotalSeconds / 1_000_000
                : (double?)null;

            return new ProxyDownload
            {
                Ok = true,
                Bytes = bytes.Length,
                Ms = sw.Elapsed.TotalMilliseconds,
                Mbps = mbps
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ProxyDownload
            {
                Ok = false,
                Bytes = 0,
                Ms = sw.Elapsed.TotalMilliseconds,
                Err = ex.Message
            };
        }
    }
}
