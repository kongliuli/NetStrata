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
        using var client = CreateClient(proxyUrl);
        var results = new List<HttpsResult>();
        foreach (var target in targets)
            results.Add(await ProbeOneAsync(client, target, ct));
        return results;
    }

    private HttpClient CreateClient(string? proxyUrl)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = proxyUrl is not null,
            Proxy = proxyUrl is not null ? new WebProxy(proxyUrl) : null,
            ConnectTimeout = _timeout,
            AutomaticDecompression = DecompressionMethods.All
        };
        return new HttpClient(handler) { Timeout = _timeout };
    }

    private async Task<HttpsResult> ProbeOneAsync(HttpClient client, HttpTarget target, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var uri = new Uri(target.Url);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, target.Url);
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);
            sw.Stop();

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
                RemoteIp = await TryResolveRemoteIp(uri.Host, ct),
                TotalMs = sw.Elapsed.TotalMilliseconds,
                TimedOut = false
            };
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return Fail(target, sw, timedOut: true, "timeout");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Fail(target, sw, timedOut: false, ex.Message);
        }
    }

    private static HttpsResult Fail(HttpTarget target, Stopwatch sw, bool timedOut, string err) => new()
    {
        Label = target.Label,
        Url = target.Url,
        Via = target.Via,
        Ok = false,
        TotalMs = sw.Elapsed.TotalMilliseconds,
        TimedOut = timedOut,
        Err = err
    };

    private static async Task<string?> TryResolveRemoteIp(string host, CancellationToken ct)
    {
        try
        {
            var addrs = await Dns.GetHostAddressesAsync(host, ct);
            return addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
