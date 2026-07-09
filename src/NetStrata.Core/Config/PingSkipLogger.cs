using NetStrata.Core.Storage;

namespace NetStrata.Core.Config;

public static class PingSkipLogger
{
    public static void Warn(string message)
    {
        try
        {
            DataDirectory.EnsureExists();
            var line = $"{DateTime.UtcNow:o} WARN {message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(DataDirectory.LogsPath, "daemon.log"), line);
        }
        catch
        {
            // ponytail: skip logging must not break collection
        }
    }
}
