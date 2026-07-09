using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using DnsClient;
using NetStrata.Core.Models;

namespace NetStrata.Core.Probes;

public sealed class DefaultTlsStackBackend : ITlsStackBackend
{
    private const int DnsTimeoutMs = 5000;
    private const int TcpTimeoutMs = 5000;
    private const int TlsTimeoutMs = 8000;
    private const int HttpTimeoutMs = 8000;

    public async Task<TlsStackLayerResult> ProbeDnsAsync(string host, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(DnsTimeoutMs);
            var client = new LookupClient();
            var response = await client.QueryAsync(host, QueryType.A, cancellationToken: cts.Token);
            sw.Stop();
            var ips = response.Answers.ARecords().Select(r => r.Address.ToString()).ToList();
            var ok = response.Header.ResponseCode == DnsHeaderResponseCode.NoError && ips.Count > 0;
            return new TlsStackLayerResult
            {
                Ok = ok,
                Ms = sw.Elapsed.TotalMilliseconds,
                Ips = ips,
                Err = ok ? null : response.Header.ResponseCode.ToString()
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TlsStackLayerResult { Ok = false, Ms = sw.Elapsed.TotalMilliseconds, Err = ex.Message };
        }
    }

    public async Task<TlsStackLayerResult> ProbeTcpAsync(string host, int port, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TcpTimeoutMs);
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, cts.Token);
            sw.Stop();
            return new TlsStackLayerResult { Ok = true, Ms = sw.Elapsed.TotalMilliseconds };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TlsStackLayerResult { Ok = false, Ms = sw.Elapsed.TotalMilliseconds, Err = ex.Message };
        }
    }

    public async Task<TlsStackLayerResult> ProbeTlsAsync(string host, int port, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TlsTimeoutMs);
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, cts.Token);
            using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, cts.Token);
            sw.Stop();
            return new TlsStackLayerResult { Ok = ssl.IsAuthenticated, Ms = sw.Elapsed.TotalMilliseconds };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TlsStackLayerResult { Ok = false, Ms = sw.Elapsed.TotalMilliseconds, Err = ex.Message };
        }
    }

    public async Task<TlsStackLayerResult> ProbeHttpAsync(string host, int port, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(HttpTimeoutMs);
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, cts.Token);
            using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, cts.Token);

            var request = $"GET / HTTP/1.1\r\nHost: {host}\r\nConnection: close\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(request);
            await ssl.WriteAsync(bytes, cts.Token);

            var buffer = new byte[512];
            var read = await ssl.ReadAsync(buffer, cts.Token);
            sw.Stop();

            var head = Encoding.ASCII.GetString(buffer, 0, read);
            var code = ParseHttpStatus(head);
            var ok = code is >= 200 and < 500 && code != 451;
            return new TlsStackLayerResult
            {
                Ok = ok,
                Ms = sw.Elapsed.TotalMilliseconds,
                HttpCode = code,
                Err = ok ? null : $"http {code}"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TlsStackLayerResult { Ok = false, Ms = sw.Elapsed.TotalMilliseconds, Err = ex.Message };
        }
    }

    private static int? ParseHttpStatus(string head)
    {
        if (!head.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
            return null;
        var parts = head.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && int.TryParse(parts[1], out var code) ? code : null;
    }
}
