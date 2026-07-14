namespace NetStrata.Core.Cli;

public static class CliArgs
{
    public static bool IsCliMode(string[] args) =>
        args.Length > 0 && (
            args.Contains("--once")
            || args.Contains("--export")
            || args.Contains("--help")
            || args.Contains("-h")
            || args.Contains("--web")
            || args.Contains("-w")
            || args.Contains("--tui")
            || args.Contains("--follow"));

    public static int ParseInt(string[] args, string flag, int fallback)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag && int.TryParse(args[i + 1], out var n))
                return n;
        }
        return fallback;
    }

    public static string? ParseString(string[] args, string flag, string? fallback = null)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag)
                return args[i + 1];
        }
        return fallback;
    }

    public static IReadOnlyList<string> ParsePing(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--ping" or "-p" && i + 1 < args.Length)
                return args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        return [];
    }
}
