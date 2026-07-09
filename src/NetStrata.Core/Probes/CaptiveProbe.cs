using System.Diagnostics;
using System.Net.Http;
using NetStrata.Core.Models;

namespace NetStrata.Core.Probes;

public sealed class CaptiveProbe
{
    private const string Url = "http://captive.apple.com/hotspot-detect.html";

    public async Task<CaptiveResult> ProbeAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var handler = new SocketsHttpHandler { UseProxy = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            using var response = await client.GetAsync(Url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            sw.Stop();

            var head = body.Length > 32 ? body[..32] : body;
            return new CaptiveResult
            {
                Ok = response.IsSuccessStatusCode && body.Contains("Success", StringComparison.Ordinal),
                HttpCode = (int)response.StatusCode,
                BodyHead = head,
                Redirected = response.RequestMessage?.RequestUri?.ToString() != Url,
                TotalMs = sw.Elapsed.TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new CaptiveResult
            {
                Ok = false,
                TotalMs = sw.Elapsed.TotalMilliseconds,
                BodyHead = ex.Message
            };
        }
    }
}
