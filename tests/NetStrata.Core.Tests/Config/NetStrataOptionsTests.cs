using NetStrata.Core.Config;

namespace NetStrata.Core.Tests.Config;

public sealed class NetStrataOptionsTests
{
    [Fact]
    public void MergePingExtra_FiltersInvalidAndCaps()
    {
        var skipped = new List<string>();
        var merged = NetStrataOptions.MergePingExtra(
            ["223.5.5.5", "oops bad"],
            "1.1.1.1,223.5.5.5",
            ["8.8.8.8"],
            skipped.Add);

        Assert.Equal(3, merged.Count);
        Assert.Contains("invalid ping target skipped: oops bad", skipped);
    }
}
