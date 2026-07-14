using NetStrata.Core.Config;

namespace NetStrata.Core.Tests.Config;

public sealed class SettingsMapperTests
{
    [Fact]
    public void RoundTrip_PreservesFields()
    {
        var config = new UserConfig
        {
            IntervalMs = 45_000,
            Port = 9000,
            PingExtra = ["192.168.1.50"],
            PingExtraLabels = new Dictionary<string, string> { ["192.168.1.50"] = "nas" },
            TlsStackTargets = ["github.com"]
        };

        var form = SettingsMapper.ToForm(config);
        var back = SettingsMapper.FromForm(form);

        Assert.Equal(45_000, back.IntervalMs);
        Assert.Equal(9000, back.Port);
        Assert.Equal("192.168.1.50", Assert.Single(back.PingExtra));
        Assert.Equal("nas", back.PingExtraLabels["192.168.1.50"]);
        Assert.Equal("github.com", Assert.Single(back.TlsStackTargets));
    }

    [Fact]
    public void SaveLoad_WritesJson()
    {
        var path = Path.Combine(Path.GetTempPath(), "netstrata-config-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            UserConfigLoader.Save(path, new UserConfig { Port = 8788, PingExtra = ["1.1.1.1"] });
            var loaded = UserConfigLoader.Load(path);
            Assert.Equal(8788, loaded.Port);
            Assert.Equal("1.1.1.1", Assert.Single(loaded.PingExtra));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void FromForm_EmptyPingKeepsExistingTargets()
    {
        var existing = new UserConfig
        {
            PingExtra = ["192.168.1.50"],
            PingExtraLabels = new Dictionary<string, string> { ["192.168.1.50"] = "nas" },
            HttpsExtra = ["https://example.com/"]
        };

        var back = SettingsMapper.FromForm(new SettingsFormModel
        {
            IntervalMs = "60000",
            Port = "8787",
            PingExtra = "",
            PingLabels = "",
            TlsStackTargets = "github.com",
            Lang = "zh",
            Theme = "dark"
        }, existing);

        Assert.Equal("192.168.1.50", Assert.Single(back.PingExtra));
        Assert.Equal("nas", back.PingExtraLabels["192.168.1.50"]);
        Assert.Equal("https://example.com/", Assert.Single(back.HttpsExtra));
        Assert.Equal("dark", back.Theme);
    }
}
