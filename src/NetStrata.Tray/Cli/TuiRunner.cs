using NetStrata.Core.Collector;
using NetStrata.Core.Config;
using NetStrata.Core.Models;
using NetStrata.Core.Storage;
using NetStrata.Core.Tui;
using Spectre.Console;

namespace NetStrata.Tray.Cli;

public static class TuiRunner
{
    public static async Task RunAsync(NetStrataOptions options, bool followOnly, CancellationToken ct = default)
    {
        var storage = new JsonSampleStorage(options.DataDir);
        var collector = new SampleCollector();
        var session = new TuiSession
        {
            Lang = NetStrata.Core.Ui.LangResolver.Resolve(options.Lang)
        };

        Console.CancelKeyPress += (_, e) => { session.Running = false; e.Cancel = true; };

        await AnsiConsole.Live(new Spectre.Console.Panel("[grey]starting…[/]"))
            .StartAsync(async ctx =>
            {
                while (session.Running && !ct.IsCancellationRequested)
                {
                    var (sample, alerts) = await LoadSampleAsync(storage, collector, options, followOnly, ct);
                    ctx.UpdateTarget(Render(sample, alerts, session.Lang, options.IntervalMs));
                    ctx.Refresh();

                    if (!await WaitOrKeyAsync(options.IntervalMs, session, ct))
                        break;
                }
            });
    }

    private sealed class TuiSession
    {
        public string Lang { get; set; } = "en";
        public bool Running { get; set; } = true;
    }

    private static async Task<(Sample? Sample, IReadOnlyList<Alert> Alerts)> LoadSampleAsync(
        ISampleStorage storage,
        SampleCollector collector,
        NetStrataOptions options,
        bool followOnly,
        CancellationToken ct)
    {
        var state = await storage.ReadStateAsync(ct);
        if (state?.Latest is not null)
            return (state.Latest, state.RecentAlerts);

        if (followOnly)
            return (null, []);

        var sample = await collector.CollectAsync(new CollectOptions
        {
            PingExtra = options.PingExtra,
            ProxyOverride = options.ProxyOverride,
            TlsStackTargets = options.TlsStackTargets,
            HttpsExtra = options.HttpsExtra
        }, ct);
        return (sample, sample.Alerts);
    }

    private static Spectre.Console.Panel Render(Sample? sample, IReadOnlyList<Alert> alerts, string lang, int intervalMs)
    {
        if (sample?.Verdict is null)
        {
            var msg = lang == "zh"
                ? "等待 daemon 写入 state…\n运行 NetStrata.exe（托盘）或去掉 --follow"
                : "Waiting for daemon state…\nRun NetStrata.exe (tray) or drop --follow";
            return new Spectre.Console.Panel(msg) { Header = new PanelHeader("NetStrata"), Border = BoxBorder.Rounded };
        }

        var v = sample.Verdict;
        var table = new Table().Border(TableBorder.Simple).HideHeaders();
        table.AddColumn("layer");
        table.AddColumn("state");
        table.AddColumn("detail");

        foreach (var (layer, state, detail) in StatusFormatter.FormatLayers(v))
            table.AddRow(layer, StatusFormatter.StateGlyph(state), Markup.Escape(detail));

        var ai = $"[bold]AI[/] {StatusFormatter.StateGlyph(v.Ai.State)} {Markup.Escape(v.Ai.Headline)}";
        var alertLine = StatusFormatter.FormatAlerts(alerts, lang);
        var footer = lang == "zh"
            ? $"[grey]{sample.T} · {sample.CycleMs:F0}ms · 每 {intervalMs / 1000}s 刷新 · q 退出 · l 语言 · r 立即刷新[/]"
            : $"[grey]{sample.T} · {sample.CycleMs:F0}ms · refresh {intervalMs / 1000}s · q quit · l lang · r now[/]";

        var content = string.IsNullOrEmpty(alertLine)
            ? new Rows(
                new Markup(StatusFormatter.FormatHeader(v, lang)),
                table,
                new Markup(ai),
                new Markup(footer))
            : new Rows(
                new Markup(StatusFormatter.FormatHeader(v, lang)),
                table,
                new Markup(ai),
                new Markup($"[yellow]{Markup.Escape(alertLine)}[/]"),
                new Markup(footer));

        return new Spectre.Console.Panel(content)
        {
            Header = new PanelHeader("NetStrata"),
            Border = BoxBorder.Rounded
        };
    }

    private static async Task<bool> WaitOrKeyAsync(int intervalMs, TuiSession session, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(intervalMs);
        while (DateTime.UtcNow < deadline && session.Running && !ct.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(200, ct);
                continue;
            }

            var key = Console.ReadKey(intercept: true);
            switch (char.ToLowerInvariant(key.KeyChar))
            {
                case 'q':
                    session.Running = false;
                    return false;
                case 'l':
                    session.Lang = session.Lang == "zh" ? "en" : "zh";
                    return true;
                case 'r':
                    return true;
            }
        }
        return session.Running;
    }
}
