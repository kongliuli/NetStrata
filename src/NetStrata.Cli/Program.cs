using System.Text.Json;
using System.Text.Json.Serialization;
using NetStrata.Core.Collector;
using NetStrata.Core.Models;

if (args.Contains("--once"))
{
    var pingExtra = ParsePingExtra(args);
    var collector = new SampleCollector();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var sample = await collector.CollectAsync(new CollectOptions { PingExtra = pingExtra }, cts.Token);

    var json = JsonSerializer.Serialize(sample, JsonOptions.Default);
    Console.WriteLine(json);
    return 0;
}

if (args.Contains("-h") || args.Contains("--help"))
{
    Console.WriteLine("""
        netstrata — Windows layered network health monitor

        Usage:
          netstrata --once              Single probe, JSON to stdout
          netstrata --once --ping IP    Add extra ping targets (comma-separated)
          netstrata --help              Show this help
        """);
    return 0;
}

Console.Error.WriteLine("netstrata: use --once or --help");
return 1;

static IReadOnlyList<string> ParsePingExtra(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] is "--ping" or "-p" && i + 1 < args.Length)
            return args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    var env = Environment.GetEnvironmentVariable("NETSTRATA_PING_EXTRA");
    return string.IsNullOrWhiteSpace(env)
        ? []
        : env.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
