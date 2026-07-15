using System.Diagnostics;
using System.Net;
using DnsClient;
using NetStrata.Core.Models;

namespace NetStrata.Core.Probes;

public sealed class DnsProbe : IProbe<IReadOnlyList<DnsResult>>
{
    public static readonly string[] Servers = ["system", "223.5.5.5", "119.29.29.29", "8.8.8.8", "1.1.1.1"];
    public static readonly string[] Domains = ["baidu.com", "google.com", "github.com", "cloudflare.com"];

    public string Name => "dns";

    public async Task<IReadOnlyList<DnsResult>> ProbeAsync(CancellationToken ct)
    {
        var tasks = Servers.SelectMany(server => Domains.Select(domain => QueryAsync(server, domain, ct)));
        return await Task.WhenAll(tasks);
    }

    private static async Task<DnsResult> QueryAsync(string server, string domain, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            IDnsQueryResponse response;
            if (server == "system")
            {
                var client = new LookupClient();
                response = await client.QueryAsync(domain, QueryType.A, cancellationToken: ct);
            }
            else
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(server), 53);
                var client = new LookupClient(endpoint);
                response = await client.QueryAsync(domain, QueryType.A, cancellationToken: ct);
            }

            sw.Stop();
            var ips = response.Answers.ARecords().Select(r => r.Address.ToString()).ToList();
            return new DnsResult
            {
                Server = server,
                Domain = domain,
                Ok = response.Header.ResponseCode == DnsHeaderResponseCode.NoError && ips.Count > 0,
                Ms = sw.Elapsed.TotalMilliseconds,
                Ips = ips,
                Flags = response.Header.ResponseCode.ToString(),
                Err = ips.Count == 0 ? "no A records" : null
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DnsResult
            {
                Server = server,
                Domain = domain,
                Ok = false,
                Ms = sw.Elapsed.TotalMilliseconds,
                Err = ex.Message
            };
        }
    }
}
