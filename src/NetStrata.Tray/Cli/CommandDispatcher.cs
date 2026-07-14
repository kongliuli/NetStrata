using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NetStrata.Core.Cli;
using NetStrata.Core.Collector;
using NetStrata.Core.Config;
using NetStrata.Core.Judge;
using NetStrata.Core.Storage;

namespace NetStrata.Tray.Cli;

public static class CommandDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static bool IsCliMode(string[] args) => CliArgs.IsCliMode(args);

    public static async Task<int> RunAsync(string[] args)
    {
        var options = NetStrataOptions.FromEnvironment();

        if (args.Contains("--once"))
            return await RunOnceAsync(args, options);

        if (args.Contains("--web") || args.Contains("-w"))
        {
            Console.Error.WriteLine("NetStrata: --web 本阶段未启用（WPF 托盘已内置进程内 Daemon）。直接运行 NetStrata.exe 即可。");
            return 2;
        }

        if (args.Contains("--export"))
            return await RunExportAsync(args, options);

        if (args.Contains("-h") || args.Contains("--help"))
        {
            PrintHelp();
            return 0;
        }

        if (args.Contains("--tui") || args.Contains("--follow"))
        {
            DataDirectory.EnsureExists();
            await TuiRunner.RunAsync(options, followOnly: args.Contains("--follow"));
            return 0;
        }

        Console.Error.WriteLine("NetStrata: use --once, --export, --follow, --tui, or --help (no args = tray UI)");
        return 1;
    }

    private static async Task<int> RunOnceAsync(string[] args, NetStrataOptions options)
    {
        DataDirectory.EnsureExists();
        var pingExtra = NetStrataOptions.MergePingExtra(
            options.PingExtra,
            Environment.GetEnvironmentVariable("NETSTRATA_PING_EXTRA"),
            CliArgs.ParsePing(args),
            msg => PingSkipLogger.Warn(msg));

        var collector = new SampleCollector();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var sample = await collector.CollectAsync(new CollectOptions
        {
            PingExtra = pingExtra,
            PingExtraLabels = options.PingExtraLabels,
            ProxyOverride = options.ProxyOverride,
            TlsStackTargets = options.TlsStackTargets,
            HttpsExtra = options.HttpsExtra
        }, cts.Token);

        Console.WriteLine(JsonSerializer.Serialize(sample, JsonOptions));
        return 0;
    }

    private static async Task<int> RunExportAsync(string[] args, NetStrataOptions options)
    {
        DataDirectory.EnsureExists();
        var minutes = CliArgs.ParseInt(args, "--minutes", 60);
        var format = CliArgs.ParseString(args, "--format", "markdown");
        var output = CliArgs.ParseString(args, "-o") ?? CliArgs.ParseString(args, "--output");

        var storage = new JsonSampleStorage(options.DataDir);
        var exporter = new ReportExporter();
        var conclusions = new ConclusionEngine();

        var samples = await storage.ReadTailAsync(Math.Max(240, minutes * 2), CancellationToken.None);
        var state = await storage.ReadStateAsync(CancellationToken.None);
        var conclusionsMd = await storage.ReadConclusionsAsync(CancellationToken.None)
            ?? conclusions.GenerateMarkdown(samples);
        var report = exporter.Build(samples, state?.RecentAlerts ?? [], conclusionsMd, minutes);
        var content = (format ?? "markdown").Equals("json", StringComparison.OrdinalIgnoreCase)
            ? exporter.ToJson(report)
            : exporter.ToMarkdown(report);

        if (!string.IsNullOrWhiteSpace(output))
            await File.WriteAllTextAsync(output, content);
        else
            Console.Write(content);

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            NetStrata — Windows layered network health monitor (WPF + CLI)

            Usage:
              NetStrata.exe                       Tray UI + in-process daemon (default)
              NetStrata.exe --once                Single probe, JSON to stdout
              NetStrata.exe --export -o report.md Export last 60 minutes to file
              NetStrata.exe --once --ping IP      Add extra ping targets (comma-separated)
              NetStrata.exe --tui                 Terminal UI
              NetStrata.exe --follow              TUI reading daemon state.json only
              NetStrata.exe --help                Show this help

            TUI keys: q quit · l language · r refresh

            Environment:
              NETSTRATA_PROXY               Force proxy URL; none/off to disable
              NETSTRATA_PING_EXTRA          Extra ping targets (comma-separated, max 10)
              NETSTRATA_INTERVAL_MS         Daemon interval ms (default 60000)
              NETSTRATA_LANG                zh / en
              NETSTRATA_DOWNLOAD_EVERY      Proxy download probe every N cycles
              NETSTRATA_CONCLUSION_EVERY    Rewrite conclusions.md every N cycles

            Config: %APPDATA%\NetStrata\config.json
            Docs:  docs/USAGE.md
            """);
    }
}

internal static class NativeConsole
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeConsole();
}
