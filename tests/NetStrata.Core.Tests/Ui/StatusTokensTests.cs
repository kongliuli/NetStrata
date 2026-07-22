using NetStrata.Core.Ui;

namespace NetStrata.Core.Tests.Ui;

public sealed class StatusTokensTests
{
    [Theory]
    [InlineData("ok", StatusKind.Ok)]
    [InlineData("healthy", StatusKind.Ok)]
    [InlineData("degraded", StatusKind.Degraded)]
    [InlineData("fail", StatusKind.Fail)]
    [InlineData("skipped", StatusKind.Skipped)]
    [InlineData("unknown", StatusKind.Skipped)]
    public void FromState_MapsKnownKeys(string state, StatusKind expected) =>
        Assert.Equal(expected, StatusTokens.FromState(state));

    [Theory]
    [InlineData("healthy", StatusKind.Ok)]
    [InlineData("健康", StatusKind.Ok)]
    [InlineData("degraded", StatusKind.Degraded)]
    [InlineData("proxy_bad", StatusKind.Fail)]
    [InlineData("direct_blocked_proxy_ok", StatusKind.Info)]
    public void FromOverall_MapsHeadline(string overall, StatusKind expected) =>
        Assert.Equal(expected, StatusTokens.FromOverall(overall));

    [Fact]
    public void SoftHex_DarkFail_IsNotLightPink()
    {
        var soft = StatusTokens.SoftHex(StatusKind.Fail, dark: true);
        Assert.Equal(StatusTokens.FailSoftDark, soft);
        Assert.NotEqual(StatusTokens.FailSoftLight, soft);
    }

    [Fact]
    public void FromBorderHex_AcceptsMapperColors()
    {
        Assert.Equal(StatusKind.Ok, StatusTokens.FromBorderHex("#34A853"));
        Assert.Equal(StatusKind.Fail, StatusTokens.FromBorderHex("#EA4335"));
        Assert.Equal(StatusKind.Degraded, StatusTokens.FromBorderHex("#FBBC04"));
    }

    [Fact]
    public void ResourceKey_Stable()
    {
        Assert.Equal("NsStatusOkSoftBrush", StatusTokens.ResourceKey(StatusKind.Ok, soft: true));
        Assert.Equal("NsStatusFailBrush", StatusTokens.ResourceKey(StatusKind.Fail, soft: false));
    }
}
