using System.Text.Json;
using System.Text.Json.Serialization;
using NetStrata.Core.Collector;
using NetStrata.Core.Config;
using NetStrata.Core.Judge;
using NetStrata.Core.Storage;
using NetStrata.Cli;

var options = NetStrataOptions.FromEnvironment();

if (args.Contains("--once"))
{
    DataDirectory.EnsureExists();
    var pingExtra = NetStrataOptions.MergePingExtra(
        options.PingExtra,
        Environment.GetEnvironmentVariable("NETSTRATA_PING_EXTRA"),
        ParsePingCli(args),
        msg => PingSkipLogger.Warn(msg));

    var collector = new SampleCollector();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var sample = await collector.CollectAsync(new CollectOptions
    {
        PingExtra = pingExtra,
        PingExtraLabels = options.PingExtraLabels,
        ProxyOverride = options.ProxyOverride,
        TlsStackTargets = options.TlsStackTargets
    }, cts.Token);

    Console.WriteLine(JsonSerializer.Serialize(sample, JsonOptions.Default));
    return 0;
}

if (args.Contains("--web") || args.Contains("-w"))
{
    await WebHostRunner.RunAsync(options, args);
    return 0;
}

if (args.Contains("--export"))
{
    DataDirectory.EnsureExists();
    var minutes = ParseIntCli(args, "--minutes", 60);
    var format = ParseStringCli(args, "--format", "markdown");
    var output = ParseStringCli(args, "-o") ?? ParseStringCli(args, "--output");

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

if (args.Contains("-h") || args.Contains("--help"))
{
    Console.WriteLine("""
        netstrata — Windows layered network health monitor

        Usage:
          netstrata                       Terminal UI (default)
          netstrata --follow              TUI reading daemon state.json only
          netstrata --once                Single probe, JSON to stdout
          netstrata --web                 Start dashboard + background daemon
          netstrata --export -o report.md Export last 60 minutes to file
          netstrata --once --ping IP      Add extra ping targets (comma-separated)
          netstrata --help                Show this help

        TUI keys: q quit · l language · r refresh

        Environment:
          NETSTRATA_PROXY               Force proxy URL; none/off to disable
          NETSTRATA_PING_EXTRA          Extra ping targets (comma-separated, max 10)
          NETSTRATA_INTERVAL_MS         Daemon interval ms (default 60000)
          NETSTRATA_PORT                Web port (default 8787)
          NETSTRATA_LANG                zh / en
          NETSTRATA_DOWNLOAD_EVERY      Proxy download probe every N cycles
          NETSTRATA_CONCLUSION_EVERY    Rewrite conclusions.md every N cycles
          NETSTRATA_NO_OPEN=1           Do not open browser in --web mode

        Config: %APPDATA%\\NetStrata\\config.json
        Docs:  docs/USAGE.md  docs/SCHEDULING.md
        """);
    return 0;
}

if (args.Length == 0 || args.Contains("--tui"))
{
    DataDirectory.EnsureExists();
    var followOnly = args.Contains("--follow");
    await TuiRunner.RunAsync(options, followOnly);
    return 0;
}

Console.Error.WriteLine("netstrata: use --once, --web, --export, --follow, or --help");
return 1;

static int ParseIntCli(string[] args, string flag, int fallback)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == flag && int.TryParse(args[i + 1], out var n))
            return n;
    }
    return fallback;
}

static string? ParseStringCli(string[] args, string flag, string? fallback = null)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == flag)
            return args[i + 1];
    }
    return fallback;
}

static IReadOnlyList<string> ParsePingCli(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] is "--ping" or "-p" && i + 1 < args.Length)
            return args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
    return [];
}

file static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
