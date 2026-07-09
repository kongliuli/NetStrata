using System.Net.NetworkInformation;
using NetStrata.Core.Probes;

namespace NetStrata.Core.Probes;

public static class RouteHintDetector
{
    public static IReadOnlyList<string> Detect()
    {
        var hints = new List<string>();
        var up = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToList();

        var defaultRoutes = up.Count(n =>
            n.GetIPProperties().GatewayAddresses
                .Any(g => g.Address.ToString() != "0.0.0.0"));

        if (defaultRoutes > 1)
            hints.Add("multiple default routes detected");

        if (TailscaleAddressFinder.FindTailscaleIpv4() is not null)
            hints.Add("tailscale interface present");

        return hints;
    }
}
