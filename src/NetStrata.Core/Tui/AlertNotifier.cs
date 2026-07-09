using NetStrata.Core.Models;

namespace NetStrata.Core.Tui;

public static class AlertNotifier
{
    public static IReadOnlyList<Alert> DetectNew(
        IReadOnlyList<Alert> previous,
        IReadOnlyList<Alert> current)
    {
        if (current.Count == 0)
            return [];

        var seen = new HashSet<string>(previous.Select(Key), StringComparer.Ordinal);
        return current.Where(a => !seen.Contains(Key(a))).ToList();
    }

    public static string Key(Alert alert) =>
        $"{alert.T}|{alert.Type}|{alert.Message}";
}

public sealed class AlertWatchState
{
    private IReadOnlyList<Alert> _snapshot = [];
    private bool _primed;

    public IReadOnlyList<Alert> ConsumeNew(DaemonState? state)
    {
        var current = state?.RecentAlerts ?? [];
        if (!_primed)
        {
            _snapshot = current;
            _primed = true;
            return [];
        }

        var fresh = AlertNotifier.DetectNew(_snapshot, current);
        _snapshot = current;
        return fresh;
    }
}
