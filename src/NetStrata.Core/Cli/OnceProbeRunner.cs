using System.Diagnostics;
using System.Text.Json;

namespace NetStrata.Core.Cli;

public sealed record OnceProbeResult(
    bool Ok,
    string? Overall,
    string? Headline,
    string? Error,
    string? RawJson);

public interface IOnceProbeRunner
{
    Task<OnceProbeResult> RunAsync(CancellationToken ct = default);
}

public sealed class OnceProbeRunner : IOnceProbeRunner
{
    private readonly Func<string> _resolveExecutable;
    private readonly Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string Stdout)>> _execute;

    public OnceProbeRunner(
        Func<string>? resolveExecutable = null,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string Stdout)>>? execute = null)
    {
        _resolveExecutable = resolveExecutable ?? (() => CliPathResolver.ResolveExecutable());
        _execute = execute ?? ExecuteProcessAsync;
    }

    public async Task<OnceProbeResult> RunAsync(CancellationToken ct = default)
    {
        var exe = _resolveExecutable();
        var info = BuildStartInfo(exe);
        if (info is null)
            return new OnceProbeResult(false, null, null, "cannot resolve netstrata executable", null);

        try
        {
            var (exitCode, stdout) = await _execute(info, ct);
            if (exitCode != 0)
                return new OnceProbeResult(false, null, null, $"exit {exitCode}", stdout);

            return OnceProbeParser.Parse(stdout);
        }
        catch (Exception ex)
        {
            return new OnceProbeResult(false, null, null, ex.Message, null);
        }
    }

    public static ProcessStartInfo? BuildStartInfo(string exe)
    {
        if (exe.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo("dotnet", $"\"{exe}\" --once")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        if (exe.Equals("netstrata", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo(exe, "--once")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        if (!File.Exists(exe) && !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return null;

        return new ProcessStartInfo(exe, "--once")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
    }

    private static async Task<(int ExitCode, string Stdout)> ExecuteProcessAsync(
        ProcessStartInfo info,
        CancellationToken ct)
    {
        using var process = Process.Start(info)!;
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, stdout);
    }
}

public static class OnceProbeParser
{
    public static OnceProbeResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new OnceProbeResult(false, null, null, "empty output", json);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var overall = root.GetProperty("verdict").GetProperty("overall").GetString();
            var headline = root.GetProperty("verdict").GetProperty("headline").GetString();
            return new OnceProbeResult(true, overall, headline, null, json);
        }
        catch (Exception ex)
        {
            return new OnceProbeResult(false, null, null, ex.Message, json);
        }
    }
}
