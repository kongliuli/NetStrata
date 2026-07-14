using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NetStrata.Core.Collector;
using NetStrata.Core.Config;
using NetStrata.Core.Models;

namespace NetStrata.Core.Cli;

public sealed record OnceProbeResult(
    bool Ok,
    string? Overall,
    string? Headline,
    string? Error,
    string? RawJson);

public interface IOnceProbeRunner
{
    Task<OnceProbeResult> RunAsync(CancellationToken ct = default);
}

public interface ISampleCollector
{
    Task<Sample> CollectAsync(CollectOptions? options = null, CancellationToken ct = default);
}

public sealed class SampleCollectorAdapter(SampleCollector inner) : ISampleCollector
{
    public Task<Sample> CollectAsync(CollectOptions? options = null, CancellationToken ct = default) =>
        inner.CollectAsync(options, ct);
}

/// <summary>
/// In-process single probe (no Process.Start).
/// </summary>
public sealed class OnceProbeRunner : IOnceProbeRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly ISampleCollector _collector;
    private readonly Func<NetStrataOptions> _optionsFactory;

    public OnceProbeRunner(
        ISampleCollector? collector = null,
        Func<NetStrataOptions>? optionsFactory = null)
    {
        _collector = collector ?? new SampleCollectorAdapter(new SampleCollector());
        _optionsFactory = optionsFactory ?? NetStrataOptions.FromEnvironment;
    }

    public async Task<OnceProbeResult> RunAsync(CancellationToken ct = default)
    {
        try
        {
            var options = _optionsFactory();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(30));

            var sample = await _collector.CollectAsync(new CollectOptions
            {
                PingExtra = options.PingExtra,
                PingExtraLabels = options.PingExtraLabels,
                ProxyOverride = options.ProxyOverride,
                TlsStackTargets = options.TlsStackTargets,
                HttpsExtra = options.HttpsExtra
            }, linked.Token);

            var json = JsonSerializer.Serialize(sample, JsonOptions);
            return new OnceProbeResult(
                true,
                sample.Verdict?.Overall,
                sample.Verdict?.Headline,
                null,
                json);
        }
        catch (Exception ex)
        {
            return new OnceProbeResult(false, null, null, ex.Message, null);
        }
    }
}

public static class OnceProbeParser
{
    public static OnceProbeResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new OnceProbeResult(false, null, null, "empty output", json);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var overall = root.GetProperty("verdict").GetProperty("overall").GetString();
            var headline = root.GetProperty("verdict").GetProperty("headline").GetString();
            return new OnceProbeResult(true, overall, headline, null, json);
        }
        catch (Exception ex)
        {
            return new OnceProbeResult(false, null, null, ex.Message, json);
        }
    }
}
