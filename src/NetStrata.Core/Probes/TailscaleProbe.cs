using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using NetStrata.Core.Models;

namespace NetStrata.Core.Probes;

public interface ITailscaleInstalledChecker
{
    bool IsInstalled();
}

public interface ITailscaleStatusReader
{
    string? ReadStatusJson();
}

public sealed class WindowsTailscaleInstalledChecker : ITailscaleInstalledChecker
{
    public bool IsInstalled()
    {
        try
        {
            using var sc = new System.ServiceProcess.ServiceController("Tailscale");
            _ = sc.Status;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class TailscaleCliStatusReader : ITailscaleStatusReader
{
    public string? ReadStatusJson()
    {
        var exe = FindTailscaleExe();
        if (exe is null)
            return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "status --json",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return null;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }

    internal static string? FindTailscaleExe()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidate = Path.Combine(programFiles, "Tailscale", "tailscale.exe");
        if (File.Exists(candidate))
            return candidate;

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.Combine(dir.Trim(), "tailscale.exe");
            if (File.Exists(full))
                return full;
        }
        return null;
    }
}

public interface ITailscaleAddressFinder
{
    string? FindAddress();
}

public sealed class NetworkTailscaleAddressFinder : ITailscaleAddressFinder
{
    public string? FindAddress() => TailscaleAddressFinder.FindTailscaleIpv4();
}

public static class TailscaleAddressFinder
{
    public static string? FindTailscaleIpv4(IEnumerable<NetworkInterface>? interfaces = null)
    {
        interfaces ??= NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up);

        string? fallback = null;
        foreach (var ni in interfaces)
        {
            var name = ni.Name;
            var desc = ni.Description;
            var isTailscaleNic = name.Contains("Tailscale", StringComparison.OrdinalIgnoreCase)
                                 || desc.Contains("Tailscale", StringComparison.OrdinalIgnoreCase)
                                 || desc.Contains("Wintun", StringComparison.OrdinalIgnoreCase);

            foreach (var ip in ni.GetIPProperties().UnicastAddresses
                         .Select(a => a.Address)
                         .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                         .Select(a => a.ToString()))
            {
                if (!IsCgNat10064(ip))
                    continue;

                if (isTailscaleNic)
                    return ip;

                fallback ??= ip;
            }
        }

        return fallback;
    }

    public static bool IsCgNat10064(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr))
            return false;

        var bytes = addr.GetAddressBytes();
        return bytes[0] == 100 && (bytes[1] & 0xC0) == 64;
    }
}

public static class TailscaleStatusParser
{
    public static bool ParseExitNodeActive(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("ExitNodeStatus", out var exitStatus)
                && exitStatus.ValueKind == JsonValueKind.Object
                && exitStatus.TryGetProperty("Online", out var online)
                && online.ValueKind == JsonValueKind.True)
                return true;

            if (root.TryGetProperty("UsingExitNode", out var usingExit)
                && usingExit.ValueKind == JsonValueKind.True)
                return true;
        }
        catch
        {
            return false;
        }

        return false;
    }
}

public sealed class TailscaleProbe
{
    private readonly ITailscaleInstalledChecker _installedChecker;
    private readonly ITailscaleStatusReader _statusReader;
    private readonly ITailscaleAddressFinder _addressFinder;

    public TailscaleProbe(
        ITailscaleInstalledChecker? installedChecker = null,
        ITailscaleStatusReader? statusReader = null,
        ITailscaleAddressFinder? addressFinder = null)
    {
        _installedChecker = installedChecker ?? new WindowsTailscaleInstalledChecker();
        _statusReader = statusReader ?? new TailscaleCliStatusReader();
        _addressFinder = addressFinder ?? new NetworkTailscaleAddressFinder();
    }

    public Task<TailscaleInfo?> ProbeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var address = _addressFinder.FindAddress();
        var installed = _installedChecker.IsInstalled() || address is not null;
        if (!installed)
            return Task.FromResult<TailscaleInfo?>(null);

        var signedIn = address is not null;
        var statusJson = _statusReader.ReadStatusJson();
        var exitNode = TailscaleStatusParser.ParseExitNodeActive(statusJson);

        return Task.FromResult<TailscaleInfo?>(new TailscaleInfo
        {
            Installed = true,
            SignedIn = signedIn,
            ExitNodeActive = exitNode,
            Address = address
        });
    }
}
