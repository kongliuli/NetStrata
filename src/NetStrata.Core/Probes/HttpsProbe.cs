using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using NetStrata.Core.Models;

namespace NetStrata.Core.Probes;

public sealed record HttpTarget(
    string Label,
    string Url,
    string Via,
    bool AcceptAnyCode = false);

public sealed class HttpsProbe : IProbe<IReadOnlyList<HttpsResult>>
{
    public static readonly HttpTarget[] ProxyTargets =
    [
        new("google_proxy", "https://www.google.com", "proxy"),
        new("cloudflare_proxy", "https://www.cloudflare.com", "proxy"),
        new("github_proxy", "https://github.com", "proxy"),
        new("youtube_proxy", "https://www.youtube.com", "proxy"),
        new("anthropic_proxy", "https://api.anthropic.com/", "proxy", AcceptAnyCode: true),
        new("openai_proxy", "https://api.openai.com/", "proxy", AcceptAnyCode: true)
    ];

    public static readonly HttpTarget[] DirectTargets =
    [
        new("baidu_direct", "https://www.baidu.com", "direct"),
        new("taobao_direct", "https://www.taobao.com", "direct"),
        new("google_direct", "https://www.google.com", "direct"),
        new("cloudflare_direct", "https://www.cloudflare.com", "direct"),
        new("github_direct", "https://github.com", "direct"),
        new("anthropic_direct", "https://api.anthropic.com/", "direct", AcceptAnyCode: true),
        new("openai_direct", "https://api.openai.com/", "direct", AcceptAnyCode: true)
    ];

    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(8);

    public string Name => "https";

    public async Task<IReadOnlyList<HttpsResult>> ProbeAsync(CancellationToken ct) =>
        await ProbeTargetsAsync(DirectTargets, proxyUrl: null, ct);

    public async Task<IReadOnlyList<HttpsResult>> ProbeTargetsAsync(
        IEnumerable<HttpTarget> targets,
        string? proxyUrl,
        CancellationToken ct)
    {
        var results = new List<HttpsResult>();
        foreach (var target in targets)
            results.Add(await ProbeOneAsync(target, proxyUrl, ct));
        return results;
    }

    private async Task<HttpsResult> ProbeOneAsync(HttpTarget target, string? proxyUrl, CancellationToken ct)
    {
        var totalSw = Stopwatch.StartNew();
        var uri = new Uri(target.Url);

        var dnsMs = 0.0;
        string? remoteIp = null;
        var dnsSw = Stopwatch.StartNew();
        try
        {
            var addrs = await Dns.GetHostAddressesAsync(uri.Host, ct);
            remoteIp = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString()
                       ?? addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6)?.ToString();
        }
        catch
        {
            // dnsMs still recorded
        }
        dnsMs = dnsSw.Elapsed.TotalMilliseconds;

        var connectMs = 0.0;
        using var handler = CreateHandler(proxyUrl, ms => connectMs = ms);
        using var client = new HttpClient(handler) { Timeout = _timeout };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, target.Url);
            var requestSw = Stopwatch.StartNew();
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);
            requestSw.Stop();
            totalSw.Stop();

            var firstByteMs = requestSw.Elapsed.TotalMilliseconds;
            // ponytail: HttpClient hides TLS handshake instant; remainder after TCP connect approximates TLS+TTFB
            var tlsMs = Math.Max(0, firstByteMs - connectMs);

            var ok = target.AcceptAnyCode
                ? response.StatusCode > 0
                : response.IsSuccessStatusCode
                  || (int)response.StatusCode is 301 or 302 or 401 or 403;

            return new HttpsResult
            {
                Label = target.Label,
                Url = target.Url,
                Via = target.Via,
                Ok = ok,
                HttpCode = (int)response.StatusCode,
                RemoteIp = remoteIp,
                DnsMs = dnsMs,
                ConnectMs = connectMs,
                TlsMs = tlsMs,
                FirstByteMs = firstByteMs,
                TotalMs = totalSw.Elapsed.TotalMilliseconds,
                TimedOut = false
            };
        }
        catch (TaskCanceledException)
        {
            totalSw.Stop();
            return Fail(target, totalSw.Elapsed.TotalMilliseconds, dnsMs, connectMs, timedOut: true, "timeout");
        }
        catch (Exception ex)
        {
            totalSw.Stop();
            return Fail(target, totalSw.Elapsed.TotalMilliseconds, dnsMs, connectMs, timedOut: false, ex.Message);
        }
    }

    private static SocketsHttpHandler CreateHandler(string? proxyUrl, Action<double> onConnectMs)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = proxyUrl is not null,
            Proxy = proxyUrl is not null ? new WebProxy(proxyUrl) : null,
            ConnectTimeout = TimeSpan.FromSeconds(8),
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = async (context, token) =>
            {
                var sw = Stopwatch.StartNew();
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(context.DnsEndPoint, token);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }

                onConnectMs(sw.Elapsed.TotalMilliseconds);
                return new NetworkStream(socket, ownsSocket: true);
            }
        };
        return handler;
    }

    private static HttpsResult Fail(
        HttpTarget target,
        double totalMs,
        double dnsMs,
        double connectMs,
        bool timedOut,
        string err) => new()
    {
        Label = target.Label,
        Url = target.Url,
        Via = target.Via,
        Ok = false,
        DnsMs = dnsMs,
        ConnectMs = connectMs,
        TotalMs = totalMs,
        TimedOut = timedOut,
        Err = err
    };
}
