using System.Diagnostics;
using NetStrata.Core.Judge;
using NetStrata.Core.Models;
using NetStrata.Core.Probes;

namespace NetStrata.Core.Collector;

public sealed class CollectOptions
{
    public IReadOnlyList<string> PingExtra { get; init; } = [];
}

public sealed class SampleCollector
{
    private readonly IPingSender _pinger;
    private readonly InterfaceProbe _interfaceProbe = new();
    private readonly DnsProbe _dnsProbe = new();
    private readonly HttpsProbe _httpsProbe = new();
    private readonly VerdictEngine _verdictEngine = new();

    public SampleCollector(IPingSender? pinger = null) =>
        _pinger = pinger ?? new SystemPingSender();

    public async Task<Sample> CollectAsync(CollectOptions? options = null, CancellationToken ct = default)
    {
        options ??= new CollectOptions();
        var sw = Stopwatch.StartNew();

        var iface = await _interfaceProbe.ProbeAsync(ct);

        var pingTargets = new List<string>();
        if (!string.IsNullOrEmpty(iface?.Gateway))
            pingTargets.Add(iface.Gateway);
        pingTargets.AddRange(options.PingExtra);
        pingTargets.AddRange(PingProbe.BaseTargets);

        var ping = new PingProbe(_pinger, options.PingExtra);
        var pingResults = new List<PingResult>();
        var seen = new HashSet<string>();
        foreach (var target in pingTargets.Where(seen.Add))
        {
            pingResults.Add(await ping.PingTargetAsync(
                target,
                custom: options.PingExtra.Contains(target),
                ct: ct));
        }

        var dnsTask = _dnsProbe.ProbeAsync(ct);
        var httpsTask = _httpsProbe.ProbeAsync(ct);
        await Task.WhenAll(dnsTask, httpsTask);

        sw.Stop();

        var partial = new Sample
        {
            T = DateTime.UtcNow.ToString("o"),
            CycleMs = sw.Elapsed.TotalMilliseconds,
            Wifi = null,
            Iface = iface,
            Dns = await dnsTask,
            Pings = pingResults,
            Https = await httpsTask,
            ProxyConfig = new ProxyConfig(),
            ProxyEgress = null
        };

        return partial with { Verdict = _verdictEngine.Judge(partial) };
    }
}
