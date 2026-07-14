using NetStrata.Core.Collector;
using NetStrata.Core.Config;
using NetStrata.Core.Probes;

namespace NetStrata.Core.Tests.Collector;

public sealed class HttpsExtraCollectOptionsTests
{
    [Fact]
    public void CollectOptions_CarriesHttpsExtra()
    {
        var opts = new CollectOptions
        {
            HttpsExtra = ["https://example.com/", "https://httpbin.org/get"]
        };
        Assert.Equal(2, opts.HttpsExtra.Count);
    }

    [Fact]
    public void UserConfig_RoundTripsHttpsExtra()
    {
        var path = Path.Combine(Path.GetTempPath(), "netstrata-https-extra-" + Guid.NewGuid() + ".json");
        try
        {
            UserConfigLoader.Save(path, new UserConfig
            {
                HttpsExtra = ["https://example.com/"]
            });
            var loaded = UserConfigLoader.Load(path);
            Assert.Equal(["https://example.com/"], loaded.HttpsExtra);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DirectTargets_StillIncludeAiCatalog()
    {
        Assert.Contains(HttpsProbe.DirectTargets, t => t.Label == "openai_direct");
        Assert.Contains(HttpsProbe.DirectTargets, t => t.Label == "cursor_direct");
    }
}
