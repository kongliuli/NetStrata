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
    public IReadOnlyDictionary<string, string> PingExtraLabels { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> TlsStackTargets { get; init; } = [];
    public IReadOnlyList<string> HttpsExtra { get; init; } = [];
    public string? ProxyOverride { get; init; }
    public bool WithDownload { get; init; }
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
    private readonly CaptiveProbe _captiveProbe = new();
    private readonly ProxyDownloadProbe _proxyDownloadProbe = new();
    private readonly TailscaleProbe _tailscaleProbe = new();
    private readonly TlsStackProbe _tlsStackProbe = new();
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
        if (iface is not null)
            iface = iface with { RouteHints = RouteHintDetector.Detect() };
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
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var customTargets = new HashSet<string>(options.PingExtra, StringComparer.OrdinalIgnoreCase);
        foreach (var target in pingTargets.Where(seen.Add))
        {
            options.PingExtraLabels.TryGetValue(target, out var label);
            pingResults.Add(await ping.PingTargetAsync(
                target,
                custom: customTargets.Contains(target),
                label: label,
                ct: ct));
        }

        var extras = options.HttpsExtra
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select((u, i) => new HttpTarget($"user_{i}_direct", u.Trim(), "direct", AcceptAnyCode: true))
            .ToArray();
        var httpsTargets = (proxyUrl is not null
                ? HttpsProbe.DirectTargets.Concat(HttpsProbe.ProxyTargets)
                : HttpsProbe.DirectTargets.AsEnumerable())
            .Concat(extras)
            .ToArray();

        var dnsTask = _dnsProbe.ProbeAsync(ct);
        var httpsTask = _httpsProbe.ProbeTargetsAsync(httpsTargets, proxyUrl, ct);
        var captiveTask = _captiveProbe.ProbeAsync(ct);
        var tailscaleTask = _tailscaleProbe.ProbeAsync(ct);
        await Task.WhenAll(dnsTask, httpsTask, wifiTask, captiveTask, tailscaleTask);

        var proxyConfig = _proxyConfigProbe.Probe(proxyUrl, systemProxy);
        ProxyEgress? proxyEgress = null;
        ProxyDownload? proxyDownload = null;
        if (proxyUrl is not null && proxyConfig.Listening)
        {
            proxyEgress = await _proxyEgressProbe.ProbeAsync(proxyUrl, ct);
            if (options.WithDownload)
                proxyDownload = await _proxyDownloadProbe.ProbeAsync(proxyUrl, ct);
        }

        sw.Stop();

        var tlsTargets = TlsStackTargets.Resolve(options.TlsStackTargets);
        var tlsStack = await _tlsStackProbe.ProbeAllAsync(tlsTargets, ct);

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
            ProxyEgress = proxyEgress,
            Captive = await captiveTask,
            ProxyDownload = proxyDownload,
            Tailscale = await tailscaleTask,
            TlsStack = tlsStack
        };

        var verdict = _verdictEngine.Judge(partial);
        var insights = tlsStack
            .Select(TlsStackEvaluator.ToInsight)
            .Where(i => i is not null)
            .Cast<string>()
            .ToList();
        if (insights.Count > 0)
            verdict = verdict with { Insights = verdict.Insights.Concat(insights).ToList() };

        return partial with { Verdict = verdict };
    }
}
