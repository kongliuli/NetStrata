using NetStrata.Core.Models;
using NetStrata.Core.Ui;

namespace NetStrata.Core.Judge;

public sealed class ConclusionEngine
{
    public string GenerateMarkdown(IReadOnlyList<Sample> samples, int window = 60, string? lang = null)
    {
        lang = LangResolver.Resolve(lang);
        if (samples.Count == 0)
            return "_(no conclusions yet)_\n";

        var last = samples.TakeLast(Math.Min(window, samples.Count)).ToList();
        var last20 = last.TakeLast(Math.Min(20, last.Count)).ToList();
        var bullets = new List<string>();

        if (last20.Count(s => s.Verdict?.Overall == "degraded") >= 10)
            bullets.Add("frequent degradation in last 20 cycles");

        if (last.Count(s => LayerState(s, "wifi") == "fail") >= 3)
            bullets.Add($"Wi-Fi fail in {last.Count(s => LayerState(s, "wifi") == "fail")} recent cycles");

        var recent = last.TakeLast(Math.Min(5, last.Count)).ToList();
        if (recent.Count >= 3
            && recent.All(s => LayerState(s, "proxy") == "ok")
            && recent.All(s => LayerState(s, "overseas_direct") == "fail"))
            bullets.Add("overseas direct blocked, proxy ok (expected)");

        if (last.Count(s => s.Verdict?.Ai.State == "fail") >= 5)
            bullets.Add("AI fail in 5+ recent cycles");

        foreach (var group in last
                     .SelectMany(CustomPingFailures)
                     .GroupBy(x => x.Target, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() >= 3)
            {
                var label = group.First().Label ?? group.Key;
                bullets.Add($"custom target {label} ({group.Key}) unreachable");
            }
        }

        if (last.Count(s => s.Verdict?.Insights.Any(i =>
                i.Contains("tls_block", StringComparison.OrdinalIgnoreCase)) == true) >= 2)
            bullets.Add("TLS/SNI block detected");

        if (DetectProxyFlapping(last))
            bullets.Add("proxy flapping — egress IP changing often");

        if (bullets.Count == 0)
            return UiStrings.Phrase(lang, "network mostly healthy") + "\n";

        var title = UiStrings.T(lang, "# 结论", "# Conclusions");
        var localized = bullets.Select(b => $"- {UiStrings.Phrase(lang, b)}");
        return title + "\n\n" + string.Join("\n", localized) + "\n";
    }

    private static string? LayerState(Sample sample, string layer) =>
        sample.Verdict?.Layers.FirstOrDefault(l =>
            l.Layer.Equals(layer, StringComparison.OrdinalIgnoreCase))?.State;

    private static IEnumerable<(string Target, string? Label)> CustomPingFailures(Sample sample) =>
        sample.Pings
            .Where(p => p.Custom && !p.Ok)
            .Select(p => (p.Target, p.Label));

    private static bool DetectProxyFlapping(IReadOnlyList<Sample> samples)
    {
        var ips = samples
            .Select(s => s.ProxyEgress?.Ip)
            .Where(ip => !string.IsNullOrWhiteSpace(ip))
            .ToList();

        if (ips.Count < 4)
            return false;

        var changes = 0;
        for (var i = 1; i < ips.Count; i++)
        {
            if (!string.Equals(ips[i - 1], ips[i], StringComparison.Ordinal))
                changes++;
        }

        return changes >= 3;
    }
}
