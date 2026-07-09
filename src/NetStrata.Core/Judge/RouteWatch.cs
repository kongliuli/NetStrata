using NetStrata.Core.Models;

namespace NetStrata.Core.Judge;

public static class RouteWatch
{
    public static IReadOnlyList<Alert> Compare(Sample? previous, Sample current)
    {
        if (previous is null)
            return [];

        var alerts = new List<Alert>();
        var t = current.T;

        MaybeAdd(alerts, previous.Iface?.Gateway, current.Iface?.Gateway, t,
            "gateway_changed", (p, c) => $"gateway {p} → {c}");

        MaybeAdd(alerts, previous.Iface?.Ipv4, current.Iface?.Ipv4, t,
            "ipv4_changed", (p, c) => $"ipv4 {p} → {c}");

        MaybeAdd(alerts, previous.Iface?.PrimaryDevice, current.Iface?.PrimaryDevice, t,
            "interface_changed", (p, c) => $"interface {p} → {c}");

        MaybeAdd(alerts, previous.ProxyEgress?.Ip, current.ProxyEgress?.Ip, t,
            "egress_changed", (p, c) => $"proxy egress {p} → {c}");

        return alerts;
    }

    public static IReadOnlyList<Alert> DetectPatterns(IReadOnlyList<Sample> recent)
    {
        if (recent.Count < 3)
            return [];

        var alerts = new List<Alert>();
        var last = recent[^1];
        var t = last.T;

        var last3 = recent.TakeLast(3).ToList();
        if (last3.All(s =>
                !string.IsNullOrWhiteSpace(s.ProxyConfig.ProxyUrl)
                && !s.ProxyConfig.Listening))
        {
            alerts.Add(new Alert
            {
                T = t,
                Type = "proxy_down",
                Message = "proxy configured but not listening (3 cycles)"
            });
        }

        if (recent.Count >= 4)
        {
            var window = recent.TakeLast(4).ToList();
            if (TryParseWindowStart(window[0].T, window[^1].T, out var spanMs)
                && spanMs <= 300_000)
            {
                var ips = window
                    .Select(s => s.ProxyEgress?.Ip)
                    .Where(ip => !string.IsNullOrWhiteSpace(ip))
                    .ToList();

                var changes = 0;
                for (var i = 1; i < ips.Count; i++)
                {
                    if (!string.Equals(ips[i - 1], ips[i], StringComparison.Ordinal))
                        changes++;
                }

                if (ips.Count >= 4 && changes >= 3)
                {
                    alerts.Add(new Alert
                    {
                        T = t,
                        Type = "egress_flapping",
                        Message = $"proxy egress unstable ({changes} changes in {spanMs / 1000}s)"
                    });
                }
            }
        }

        return alerts;
    }

    public static IReadOnlyList<Alert> MergeRecent(
        IReadOnlyList<Alert> existing,
        IReadOnlyList<Alert> incoming,
        int max = 20) =>
        existing.Concat(incoming).TakeLast(max).ToList();

    private static void MaybeAdd(
        List<Alert> alerts,
        string? prev,
        string? curr,
        string t,
        string type,
        Func<string, string, string> message)
    {
        if (string.IsNullOrWhiteSpace(prev) || string.IsNullOrWhiteSpace(curr))
            return;
        if (string.Equals(prev, curr, StringComparison.OrdinalIgnoreCase))
            return;

        alerts.Add(new Alert
        {
            T = t,
            Type = type,
            Message = message(prev, curr),
            Prev = prev,
            Curr = curr
        });
    }

    private static bool TryParseWindowStart(string start, string end, out double spanMs)
    {
        spanMs = 0;
        if (!DateTime.TryParse(start, out var s) || !DateTime.TryParse(end, out var e))
            return false;
        spanMs = (e - s).TotalMilliseconds;
        return spanMs >= 0;
    }
}
