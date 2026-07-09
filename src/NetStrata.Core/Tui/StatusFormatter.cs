using NetStrata.Core.Models;

namespace NetStrata.Core.Tui;

public static class StatusFormatter
{
    public static string StateGlyph(string state) => state switch
    {
        "ok" => "[green]OK[/]",
        "degraded" => "[yellow]DEG[/]",
        "fail" => "[red]FAIL[/]",
        "skipped" => "[grey]SKIP[/]",
        _ => "[grey]???[/]"
    };

    public static string FormatHeader(Verdict verdict, string lang) =>
        lang == "zh"
            ? $"[bold]{verdict.Overall}[/] — {verdict.Headline}"
            : $"[bold]{verdict.Overall}[/] — {verdict.Headline}";

    public static IReadOnlyList<(string Layer, string State, string Detail)> FormatLayers(Verdict verdict)
    {
        return verdict.Layers
            .Where(l => l.Layer != "ai")
            .Select(l => (
                l.Layer,
                l.State,
                l.Reasons.Count > 0 ? string.Join("; ", l.Reasons) : "-"))
            .ToList();
    }
}
