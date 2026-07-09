using NetStrata.Core.Probes;

namespace NetStrata.Core.Tests.Probes;

public sealed class WifiProbeTests
{
    [Fact]
    public void Parse_Ethernet_ReturnsNotWifi()
    {
        var wifi = NetshWifiParser.Parse(null, "ethernet");
        Assert.Equal("not_wifi", wifi?.Status);
    }

    [Fact]
    public void Parse_ConnectedWifi_ParsesFields()
    {
        const string output = """
            Name                   : Wi-Fi
            SSID                   : TestNet
            BSSID                  : aa:bb:cc:dd:ee:ff
            Signal                 : 70%
            Channel                : 36
            Receive rate (Mbps)    : 866
            Radio type             : 802.11ac
            Authentication         : WPA2-Personal
            State                  : connected
            """;

        var wifi = NetshWifiParser.Parse(output, "wifi");
        Assert.Equal("connected", wifi?.Status);
        Assert.Equal("TestNet", wifi?.Ssid);
        Assert.Equal(-65, wifi?.Rssi);
        Assert.Equal(866, wifi?.TxRate);
    }
}
