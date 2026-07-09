using System.Diagnostics;
using NetStrata.Core.Models;

namespace NetStrata.Core.Probes;

public interface INetshWifiReader
{
    string? RunShowInterfaces();
}

public sealed class NetshWifiReader : INetshWifiReader
{
    public string? RunShowInterfaces()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show interfaces",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return null;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return output;
        }
        catch
        {
            return null;
        }
    }
}

public static class NetshWifiParser
{
    public static WifiInfo? Parse(string? output, string linkType)
    {
        if (linkType is "ethernet" or "other")
            return new WifiInfo { Status = "not_wifi" };

        if (string.IsNullOrWhiteSpace(output))
            return new WifiInfo { Status = "no_interface" };

        var fields = ParseFields(output);
        if (!fields.TryGetValue("State", out var state) || !state.Contains("connected", StringComparison.OrdinalIgnoreCase))
        {
            return new WifiInfo
            {
                Status = "disconnected",
                Device = fields.GetValueOrDefault("Name"),
                Ssid = fields.GetValueOrDefault("SSID")
            };
        }

        var signalPct = ParseInt(fields.GetValueOrDefault("Signal"));
        int? rssi = signalPct is not null ? signalPct.Value / 2 - 100 : null;
        var channel = ParseInt(fields.GetValueOrDefault("Channel"));
        var txRate = ParseInt(fields.GetValueOrDefault("Receive rate (Mbps)")
                              ?? fields.GetValueOrDefault("Receive rate"));

        return new WifiInfo
        {
            Status = "connected",
            Device = fields.GetValueOrDefault("Name"),
            Ssid = fields.GetValueOrDefault("SSID"),
            Bssid = fields.GetValueOrDefault("BSSID"),
            Channel = channel,
            Rssi = rssi,
            TxRate = txRate,
            PhyMode = fields.GetValueOrDefault("Radio type"),
            Security = fields.GetValueOrDefault("Authentication")
        };
    }

    internal static Dictionary<string, string> ParseFields(string output)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in output.Split('\n'))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0)
                continue;
            var key = line[..idx].Trim();
            var val = line[(idx + 1)..].Trim();
            if (key.Length > 0)
                dict[key] = val;
        }
        return dict;
    }

    private static int? ParseInt(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        var digits = new string(s.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return int.TryParse(digits.Split('.')[0], out var n) ? n : null;
    }
}

public sealed class WifiProbe
{
    private readonly INetshWifiReader _reader;

    public WifiProbe(INetshWifiReader? reader = null) =>
        _reader = reader ?? new NetshWifiReader();

    public Task<WifiInfo?> ProbeAsync(InterfaceInfo? iface, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var linkType = iface?.LinkType ?? "unknown";
        var output = linkType == "wifi" ? _reader.RunShowInterfaces() : null;
        return Task.FromResult(NetshWifiParser.Parse(output, linkType));
    }
}
