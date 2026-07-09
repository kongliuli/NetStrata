using System.Diagnostics;
using NetStrata.Core.Config;
using NetStrata.Core.Judge;
using NetStrata.Core.Models;
using NetStrata.Core.Probes;
using NetStrata.Core.Proxy;
using NetStrata.Core.Storage;

namespace NetStrata.Core.Collector;

public sealed class CollectOptions
{
    public IReadOnlyList<string> PingExtra { get; init; } = [];
    public string? ProxyOverride { get; init; }
}

public sealed class SampleCollector
{
    private readonly IPingSender _pinger;
    private readonly InterfaceProbe _interfaceProbe = new();
    private readonly WifiProbe _wifiProbe = new();
    private readonly DnsProbe _dnsProbe = new();
    private readonly HttpsProbe _httpsProbe = new();
    private readonly ProxyDetector _proxyDetector;
    private readonly ProxyConfigProbe _proxyConfigProbe;
    private readonly ProxyEgressProbe _proxyEgressProbe = new();
    private readonly ISystemProxyReader _registryReader;
    private readonly VerdictEngine _verdictEngine = new();

    public SampleCollector(
        IPingSender? pinger = null,
        ProxyDetector? proxyDetector = null,
        ProxyConfigProbe? proxyConfigProbe = null,
        ISystemProxyReader? registryReader = null)
    {
        _pinger = pinger ?? new SystemPingSender();
        _registryReader = registryReader ?? new WindowsRegistryProxyReader();
        _proxyDetector = proxyDetector ?? new ProxyDetector(_registryReader);
        _proxyConfigProbe = proxyConfigProbe ?? new ProxyConfigProbe();
    }

    public async Task<Sample> CollectAsync(CollectOptions? options = null, CancellationToken ct = default)
    {
        options ??= new CollectOptions();
        DataDirectory.EnsureExists();
        var sw = Stopwatch.StartNew();

        var iface = await _interfaceProbe.ProbeAsync(ct);
        var wifiTask = _wifiProbe.ProbeAsync(iface, ct);
        var systemProxy = _registryReader.Read();
        var proxyUrl = _proxyDetector.Detect(options.ProxyOverride);

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

        var httpsTargets = proxyUrl is not null
            ? HttpsProbe.DirectTargets.Concat(HttpsProbe.ProxyTargets).ToArray()
            : HttpsProbe.DirectTargets;

        var dnsTask = _dnsProbe.ProbeAsync(ct);
        var httpsTask = _httpsProbe.ProbeTargetsAsync(httpsTargets, proxyUrl, ct);
        await Task.WhenAll(dnsTask, httpsTask, wifiTask);

        var proxyConfig = _proxyConfigProbe.Probe(proxyUrl, systemProxy);
        ProxyEgress? proxyEgress = null;
        if (proxyUrl is not null && proxyConfig.Listening)
            proxyEgress = await _proxyEgressProbe.ProbeAsync(proxyUrl, ct);

        sw.Stop();

        var partial = new Sample
        {
            T = DateTime.UtcNow.ToString("o"),
            CycleMs = sw.Elapsed.TotalMilliseconds,
            Wifi = await wifiTask,
            Iface = iface,
            Dns = await dnsTask,
            Pings = pingResults,
            Https = await httpsTask,
            ProxyConfig = proxyConfig,
            ProxyEgress = proxyEgress
        };

        return partial with { Verdict = _verdictEngine.Judge(partial) };
    }
}
