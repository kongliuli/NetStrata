using NetStrata.Core.Models;

namespace NetStrata.Core.Judge;

public sealed class ConclusionEngine
{
    public string GenerateMarkdown(IReadOnlyList<Sample> samples, int window = 60)
    {
        if (samples.Count == 0)
            return "_(no conclusions yet)_\n";

        var last = samples.TakeLast(Math.Min(window, samples.Count)).ToList();
        var last20 = last.TakeLast(Math.Min(20, last.Count)).ToList();
        var bullets = new List<string>();

        if (last20.Count(s => s.Verdict?.Overall == "degraded") >= 10)
            bullets.Add("过去 20 轮中网络多次降级");

        if (last.Count(s => LayerState(s, "wifi") == "fail") >= 3)
            bullets.Add("Wi-Fi 信号不稳定");

        var recent = last.TakeLast(Math.Min(5, last.Count)).ToList();
        if (recent.Count >= 3
            && recent.All(s => LayerState(s, "proxy") == "ok")
            && recent.All(s => LayerState(s, "overseas_direct") == "fail"))
            bullets.Add("国际直连被屏蔽，代理工作正常（预期）");

        if (last.Count(s => s.Verdict?.Ai.State == "fail") >= 5)
            bullets.Add("AI API 持续不可达，请检查代理");

        foreach (var group in last
                     .SelectMany(CustomPingFailures)
                     .GroupBy(x => x.Target, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() >= 3)
            {
                var label = group.First().Label ?? group.Key;
                bullets.Add($"自定义目标 {label} ({group.Key}) 不可达");
            }
        }

        if (last.Count(s => s.Verdict?.Insights.Any(i =>
                i.Contains("tls_block", StringComparison.OrdinalIgnoreCase)) == true) >= 2)
            bullets.Add("检测到 TLS/SNI 层阻断");

        if (DetectProxyFlapping(last))
            bullets.Add("代理不稳定，出口 IP 频繁切换");

        if (bullets.Count == 0)
            return "网络状态整体正常。\n";

        return "# 结论\n\n" + string.Join("\n", bullets.Select(b => $"- {b}")) + "\n";
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
