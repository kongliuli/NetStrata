using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using NetStrata.Core.Collector;
using NetStrata.Core.Config;
using NetStrata.Core.Judge;
using NetStrata.Core.Models;
using NetStrata.Core.Storage;

namespace NetStrata.Daemon;

public sealed class ProbeDaemon : BackgroundService
{
    private readonly SampleCollector _collector;
    private readonly ISampleStorage _storage;
    private readonly NetStrataOptions _options;
    private readonly ConclusionEngine _conclusions = new();
    private readonly string _startedAt = DateTime.UtcNow.ToString("o");
    private int _cycle;
    private Sample? _previous;
    private IReadOnlyList<Alert> _recentAlerts = [];

    public ProbeDaemon(SampleCollector collector, ISampleStorage storage, NetStrataOptions options)
    {
        _collector = collector;
        _storage = storage;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProbeLoopAsync(stoppingToken);
    }

    public async Task ProbeLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            _cycle++;

            try
            {
                var withDownload = _options.DownloadEvery > 0 && _cycle % _options.DownloadEvery == 0;
                var sample = await _collector.CollectAsync(new CollectOptions
                {
                    PingExtra = _options.PingExtra,
                    PingExtraLabels = _options.PingExtraLabels,
                    ProxyOverride = _options.ProxyOverride,
                    WithDownload = withDownload,
                    TlsStackTargets = _options.TlsStackTargets
                }, ct);

                var history = await _storage.ReadTailAsync(3, ct);
                var compareAlerts = RouteWatch.Compare(_previous, sample);
                var patternAlerts = RouteWatch.DetectPatterns(history.Append(sample).ToList());
                var cycleAlerts = compareAlerts.Concat(patternAlerts).ToList();
                if (cycleAlerts.Count > 0)
                    sample = sample with { Alerts = cycleAlerts };

                await _storage.AppendAsync(sample, ct);
                _recentAlerts = RouteWatch.MergeRecent(_recentAlerts, cycleAlerts);
                _previous = sample;

                var recent = await _storage.ReadTailAsync(20, ct);
                var rolling = new RollingStats
                {
                    Last20Overall = recent
                        .Where(s => s.Verdict is not null)
                        .GroupBy(s => s.Verdict!.Overall)
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                await _storage.WriteStateAsync(new DaemonState
                {
                    StartedAt = _startedAt,
                    Cycle = _cycle,
                    Latest = sample,
                    RecentAlerts = _recentAlerts,
                    Rolling = rolling
                }, ct);

                if (_options.ConclusionEvery > 0 && _cycle % _options.ConclusionEvery == 0)
                {
                    var window = await _storage.ReadTailAsync(60, ct);
                    await _storage.WriteConclusionsAsync(_conclusions.GenerateMarkdown(window), ct);
                }

                Log($"cycle {_cycle} overall={sample.Verdict?.Overall} ms={sample.CycleMs:F0}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"cycle {_cycle} error: {ex.Message}");
            }

            var delay = Math.Max(0, _options.IntervalMs - (int)sw.ElapsedMilliseconds);
            await Task.Delay(delay, ct);
        }
    }

    private static void Log(string message)
    {
        try
        {
            DataDirectory.EnsureExists();
            var line = $"{DateTime.UtcNow:o} {message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(DataDirectory.LogsPath, "daemon.log"), line);
        }
        catch
        {
            // ponytail: logging must not break daemon
        }
    }
}
