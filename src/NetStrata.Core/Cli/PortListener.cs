using System.Net.NetworkInformation;

namespace NetStrata.Core.Cli;

public static class PortListener
{
    public static bool IsTcpListening(int port)
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(ep => ep.Port == port);
        }
        catch
        {
            return false;
        }
    }
}
