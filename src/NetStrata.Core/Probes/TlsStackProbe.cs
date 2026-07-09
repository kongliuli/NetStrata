using NetStrata.Core.Models;

namespace NetStrata.Core.Probes;

public interface ITlsStackBackend
{
    Task<TlsStackLayerResult> ProbeDnsAsync(string host, CancellationToken ct);
    Task<TlsStackLayerResult> ProbeTcpAsync(string host, int port, CancellationToken ct);
    Task<TlsStackLayerResult> ProbeTlsAsync(string host, int port, CancellationToken ct);
    Task<TlsStackLayerResult> ProbeHttpAsync(string host, int port, CancellationToken ct);
}

public static class TlsStackTargets
{
    public static readonly (string Label, string Host, int Port)[] Defaults =
    [
        ("stack_google", "www.google.com", 443),
        ("stack_github", "github.com", 443),
        ("stack_anthropic", "api.anthropic.com", 443)
    ];

    public static IReadOnlyList<(string Label, string Host, int Port)> Resolve(IReadOnlyList<string>? configTargets)
    {
        if (configTargets is null or { Count: 0 })
            return Defaults;

        return configTargets.Select(Parse).ToList();
    }

    internal static (string Label, string Host, int Port) Parse(string entry)
    {
        var host = entry;
        var port = 443;
        var colon = entry.LastIndexOf(':');
        if (colon > 0 && int.TryParse(entry[(colon + 1)..], out var p))
        {
            host = entry[..colon];
            port = p;
        }

        var label = "stack_" + Sanitize(host);
        return (label, host, port);
    }

    private static string Sanitize(string host) =>
        new string(host.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
}

public static class TlsStackEvaluator
{
    public static TlsStackResult Build(
        string label,
        string host,
        int port,
        TlsStackLayerResult dns,
        TlsStackLayerResult? tcp,
        TlsStackLayerResult? tls,
        TlsStackLayerResult? http)
    {
        if (!dns.Ok)
            return Stop(label, host, port, dns, null, null, null, "dns_fail", "dns");

        if (tcp is null || !tcp.Ok)
            return Stop(label, host, port, dns, tcp, null, null, "tcp_fail", "tcp");

        if (tls is null || !tls.Ok)
            return Stop(label, host, port, dns, tcp, tls, null, "tls_block", "tls");

        if (http is null || !http.Ok)
            return Stop(label, host, port, dns, tcp, tls, http, "http_block", "http");

        return new TlsStackResult
        {
            Label = label,
            Host = host,
            Port = port,
            Dns = dns,
            Tcp = tcp,
            Tls = tls,
            Http = http,
            Verdict = "ok",
            StoppedAt = null
        };
    }

    public static string? ToInsight(TlsStackResult result) =>
        result.Verdict switch
        {
            "tls_block" => $"tls_block:{result.Host} — TCP ok but TLS failed, possible SNI interference",
            "dns_fail" => $"dns_fail:{result.Host} — DNS resolution failed",
            "tcp_fail" => $"tcp_fail:{result.Host} — TCP connect failed",
            "http_block" => $"http_block:{result.Host} — HTTP blocked after TLS",
            _ => null
        };

    private static TlsStackResult Stop(
        string label,
        string host,
        int port,
        TlsStackLayerResult dns,
        TlsStackLayerResult? tcp,
        TlsStackLayerResult? tls,
        TlsStackLayerResult? http,
        string verdict,
        string stoppedAt) => new()
    {
        Label = label,
        Host = host,
        Port = port,
        Dns = dns,
        Tcp = tcp,
        Tls = tls,
        Http = http,
        Verdict = verdict,
        StoppedAt = stoppedAt
    };
}

public sealed class TlsStackProbe
{
    private readonly ITlsStackBackend _backend;

    public TlsStackProbe(ITlsStackBackend? backend = null) =>
        _backend = backend ?? new DefaultTlsStackBackend();

    public async Task<TlsStackResult> ProbeTargetAsync(
        string label,
        string host,
        int port,
        CancellationToken ct = default)
    {
        var dns = await _backend.ProbeDnsAsync(host, ct);
        if (!dns.Ok)
            return TlsStackEvaluator.Build(label, host, port, dns, null, null, null);

        var tcp = await _backend.ProbeTcpAsync(host, port, ct);
        if (!tcp.Ok)
            return TlsStackEvaluator.Build(label, host, port, dns, tcp, null, null);

        var tls = await _backend.ProbeTlsAsync(host, port, ct);
        if (!tls.Ok)
            return TlsStackEvaluator.Build(label, host, port, dns, tcp, tls, null);

        var http = await _backend.ProbeHttpAsync(host, port, ct);
        return TlsStackEvaluator.Build(label, host, port, dns, tcp, tls, http);
    }

    public async Task<IReadOnlyList<TlsStackResult>> ProbeAllAsync(
        IReadOnlyList<(string Label, string Host, int Port)> targets,
        CancellationToken ct = default)
    {
        var results = new List<TlsStackResult>();
        foreach (var (label, host, port) in targets)
            results.Add(await ProbeTargetAsync(label, host, port, ct));
        return results;
    }
}
