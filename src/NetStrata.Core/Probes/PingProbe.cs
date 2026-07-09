using System.Net.NetworkInformation;
using NetStrata.Core.Models;

namespace NetStrata.Core.Probes;

public sealed class PingProbe : IProbe<IReadOnlyList<PingResult>>
{
    public static readonly string[] BaseTargets = ["223.5.5.5", "119.29.29.29", "1.1.1.1", "8.8.8.8"];

    private readonly IPingSender _pinger;
    private readonly IReadOnlyList<string> _extraTargets;

    public PingProbe(IPingSender pinger, IReadOnlyList<string>? extraTargets = null)
    {
        _pinger = pinger;
        _extraTargets = extraTargets ?? [];
    }

    public string Name => "pings";

    public async Task<IReadOnlyList<PingResult>> ProbeAsync(CancellationToken ct)
    {
        var results = new List<PingResult>();
        foreach (var target in _extraTargets.Concat(BaseTargets).Distinct())
        {
            results.Add(await PingTargetAsync(target, custom: _extraTargets.Contains(target), ct));
        }
        return results;
    }

    public async Task<PingResult> PingTargetAsync(
        string target,
        bool custom = false,
        CancellationToken ct = default,
        string? label = null)
    {
        const int count = 3;
        const int timeoutMs = 1500;
        var rtts = new List<double>();
        var received = 0;
        string? lastErr = null;

        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var reply = await _pinger.SendAsync(target, timeoutMs, ct);
                if (reply.Status == IPStatus.Success)
                {
                    received++;
                    rtts.Add(reply.RoundtripTime);
                }
                else
                    lastErr = reply.Status.ToString();
            }
            catch (Exception ex)
            {
                lastErr = ex.Message;
            }
        }

        var lossPct = (count - received) * 100.0 / count;
        double? avg = rtts.Count > 0 ? rtts.Average() : null;
        return new PingResult
        {
            Target = target,
            Label = label,
            Custom = custom,
            Ok = received > 0,
            LossPct = lossPct,
            Sent = count,
            Received = received,
            MinMs = rtts.Count > 0 ? rtts.Min() : null,
            AvgMs = avg,
            MaxMs = rtts.Count > 0 ? rtts.Max() : null,
            StddevMs = rtts.Count > 1 ? StdDev(rtts) : null,
            Err = received == 0 ? lastErr : null
        };
    }

    private static double StdDev(IReadOnlyList<double> values)
    {
        var avg = values.Average();
        return Math.Sqrt(values.Select(v => (v - avg) * (v - avg)).Average());
    }
}
