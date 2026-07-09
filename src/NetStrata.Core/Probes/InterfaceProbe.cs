using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetStrata.Core.Models;

namespace NetStrata.Core.Probes;

public sealed class InterfaceProbe : IProbe<InterfaceInfo?>
{
    public string Name => "iface";

    public Task<InterfaceInfo?> ProbeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var ni = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .OrderBy(n => IsLikelyVpn(n) ? 1 : 0)
            .ThenBy(n => n.GetIPProperties().GatewayAddresses
                .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork) ? 0 : 1)
            .ThenBy(n => n.Name)
            .FirstOrDefault();

        if (ni is null)
            return Task.FromResult<InterfaceInfo?>(null);

        var props = ni.GetIPProperties();
        var ipv4 = props.UnicastAddresses
            .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            ?.Address.ToString();
        var ipv6 = props.UnicastAddresses
            .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6
                                  && !a.Address.IsIPv6LinkLocal)
            ?.Address.ToString();
        var gateway = props.GatewayAddresses
            .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
            ?.Address.ToString();

        var linkType = ni.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => "wifi",
            NetworkInterfaceType.Ethernet => "ethernet",
            _ => "other"
        };

        return Task.FromResult<InterfaceInfo?>(new InterfaceInfo
        {
            PrimaryDevice = ni.Name,
            HardwarePort = ni.Description,
            LinkType = linkType,
            Ipv4 = ipv4,
            Ipv6 = ipv6,
            Gateway = gateway,
            SubnetMask = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.IPv4Mask.ToString(),
            DhcpDns = props.DnsAddresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .ToList()
        });
    }

    internal static bool IsLikelyVpn(NetworkInterface ni)
    {
        var name = ni.Name;
        var desc = ni.Description;
        return name.Contains("Tailscale", StringComparison.OrdinalIgnoreCase)
               || desc.Contains("Tailscale", StringComparison.OrdinalIgnoreCase)
               || desc.Contains("Wintun", StringComparison.OrdinalIgnoreCase)
               || desc.Contains("WireGuard", StringComparison.OrdinalIgnoreCase)
               || desc.Contains("TAP-", StringComparison.OrdinalIgnoreCase);
    }
}
