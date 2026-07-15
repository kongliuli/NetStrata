using System.Net;
using System.Net.Sockets;
using System.Text;
using NetStrata.Core.Probes;

namespace NetStrata.Core.Tests.Probes;

public sealed class HttpsProbeTests
{
    [Fact]
    public void EffectiveProxyUrl_Direct_NeverUsesProxy()
    {
        Assert.Null(HttpsProbe.EffectiveProxyUrl("direct", "http://127.0.0.1:7890"));
        Assert.Null(HttpsProbe.EffectiveProxyUrl("DIRECT", "http://127.0.0.1:7890"));
    }

    [Fact]
    public void EffectiveProxyUrl_Proxy_UsesDetectedUrl()
    {
        Assert.Equal("http://127.0.0.1:7890", HttpsProbe.EffectiveProxyUrl("proxy", "http://127.0.0.1:7890"));
        Assert.Null(HttpsProbe.EffectiveProxyUrl("proxy", null));
    }

    [Fact]
    public async Task Timing_PopulatesSegmentFields()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var acceptTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(cts.Token);
            await using var stream = client.GetStream();
            var buffer = new byte[512];
            _ = await stream.ReadAsync(buffer, cts.Token);
            var response = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cts.Token);
        }, cts.Token);

        var probe = new HttpsProbe();
        var results = await probe.ProbeTargetsAsync(
            [new HttpTarget("loopback", $"http://127.0.0.1:{port}/", "direct")],
            proxyUrl: null,
            cts.Token);

        await acceptTask;

        var r = Assert.Single(results);
        Assert.True(r.Ok);
        Assert.True(r.DnsMs >= 0);
        Assert.True(r.ConnectMs >= 0);
        Assert.True(r.FirstByteMs >= r.ConnectMs);
        Assert.True(r.TlsMs >= 0);
        Assert.True(r.TotalMs >= r.FirstByteMs);
    }
}
