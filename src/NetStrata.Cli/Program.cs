using System.Text.Json;
using System.Text.Json.Serialization;
using NetStrata.Core.Collector;
using NetStrata.Core.Config;
using NetStrata.Core.Storage;

if (args.Contains("--once"))
{
    DataDirectory.EnsureExists();
    var options = NetStrataOptions.FromEnvironment();
    var pingExtra = NetStrataOptions.MergePingExtra(
        options.PingExtra,
        Environment.GetEnvironmentVariable("NETSTRATA_PING_EXTRA"),
        ParsePingCli(args));

    var collector = new SampleCollector();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var sample = await collector.CollectAsync(new CollectOptions
    {
        PingExtra = pingExtra,
        ProxyOverride = options.ProxyOverride
    }, cts.Token);

    Console.WriteLine(JsonSerializer.Serialize(sample, JsonOptions.Default));
    return 0;
}

if (args.Contains("--web") || args.Contains("-w"))
{
    var options = NetStrataOptions.FromEnvironment();
    await WebHostRunner.RunAsync(options, args);
    return 0;
}

if (args.Contains("-h") || args.Contains("--help"))
{
    Console.WriteLine("""
        netstrata — Windows layered network health monitor

        Usage:
          netstrata --once              Single probe, JSON to stdout
          netstrata --web               Start dashboard + background daemon
          netstrata --once --ping IP    Add extra ping targets (comma-separated)
          netstrata --help              Show this help

        Environment:
          NETSTRATA_PROXY               Force proxy URL; none/off to disable
          NETSTRATA_PING_EXTRA          Extra ping targets (comma-separated, max 10)
          NETSTRATA_INTERVAL_MS         Daemon interval (default 60000)
          NETSTRATA_PORT                Web port (default 8787)
          NETSTRATA_NO_OPEN=1           Do not open browser in --web mode
        """);
    return 0;
}

Console.Error.WriteLine("netstrata: use --once, --web, or --help");
return 1;

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
