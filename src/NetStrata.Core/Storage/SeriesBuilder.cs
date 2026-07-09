using NetStrata.Core.Models;

namespace NetStrata.Core.Storage;

public sealed class SeriesBuilder
{
    public SeriesData Build(IReadOnlyList<Sample> samples)
    {
        var t = samples.Select(s => s.T).ToList();
        var verdict = samples.Select(s => s.Verdict?.Overall).ToList();

        var pingKeys = new HashSet<string> { "gw", "ali", "cf", "goo" };
        foreach (var s in samples)
        {
            foreach (var p in s.Pings)
            {
                if (p.Custom)
                {
                    var label = string.IsNullOrWhiteSpace(p.Label) ? p.Target : p.Label;
                    pingKeys.Add($"custom_{SanitizeKey(label)}");
                }
            }
        }

        var pings = pingKeys.ToDictionary(
            k => k,
            key => (IReadOnlyList<double?>)samples.Select(s => ResolvePing(s, key)).ToList());

        var httpsLabels = samples
            .SelectMany(s => s.Https.Select(h => h.Label))
            .Distinct()
            .ToList();

        var https = httpsLabels.ToDictionary(
            label => label,
            label => new HttpsSeries
            {
                TotalMs = samples.Select(s => s.Https.FirstOrDefault(h => h.Label == label)?.TotalMs).ToList(),
                Ok = samples.Select(s => (bool?)s.Https.FirstOrDefault(h => h.Label == label)?.Ok).ToList(),
                TimedOut = samples.Select(s => (bool?)s.Https.FirstOrDefault(h => h.Label == label)?.TimedOut).ToList()
            });

        var layerNames = new[] { "wifi", "lan", "broadband", "overseas_direct", "proxy", "ai" };
        var layers = layerNames.ToDictionary(
            name => name,
            name => (IReadOnlyList<string?>)samples
                .Select(s => s.Verdict?.Layers.FirstOrDefault(l => l.Layer == name)?.State)
                .ToList());

        return new SeriesData
        {
            T = t,
            Verdict = verdict,
            Pings = pings,
            Https = https,
            Layers = layers,
            Ai = new AiSeries
            {
                State = samples.Select(s => s.Verdict?.Ai.State).ToList()
            }
        };
    }

    private static double? ResolvePing(Sample s, string key)
    {
        return key switch
        {
            "gw" => s.Pings.FirstOrDefault(p => p.Target == s.Iface?.Gateway)?.AvgMs,
            "ali" => s.Pings.FirstOrDefault(p => p.Target == "223.5.5.5")?.AvgMs,
            "cf" => s.Pings.FirstOrDefault(p => p.Target == "1.1.1.1")?.AvgMs,
            "goo" => s.Pings.FirstOrDefault(p => p.Target == "8.8.8.8")?.AvgMs,
            _ when key.StartsWith("custom_") => s.Pings
                .FirstOrDefault(p => p.Custom && $"custom_{SanitizeKey(p.Label ?? p.Target)}" == key)?.AvgMs,
            _ => null
        };
    }

    private static string SanitizeKey(string s) =>
        new string(s.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray()).ToLowerInvariant();
}
