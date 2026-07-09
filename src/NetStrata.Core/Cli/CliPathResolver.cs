namespace NetStrata.Core.Cli;

public static class CliPathResolver
{
    public static string ResolveExecutable(string? startDir = null)
    {
        startDir ??= AppContext.BaseDirectory;
        foreach (var path in CandidatePaths(startDir))
        {
            if (File.Exists(path))
                return path;
        }

        return "netstrata";
    }

    public static IEnumerable<string> CandidatePaths(string startDir)
    {
        yield return Path.Combine(startDir, "netstrata.exe");
        yield return Path.Combine(startDir, "netstrata.dll");

        var dir = startDir;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            yield return Path.Combine(dir, "src", "NetStrata.Cli", "bin", "Debug", "net8.0", "win-x64", "netstrata.exe");
            yield return Path.Combine(dir, "src", "NetStrata.Cli", "bin", "Debug", "net8.0", "win-x64", "netstrata.dll");
            yield return Path.Combine(dir, "artifacts", "publish", "netstrata.exe");
            dir = Directory.GetParent(dir)?.FullName;
        }
    }
}
