using NetStrata.Core.Cli;

namespace NetStrata.Core.Tests.Cli;

public sealed class BrowserLauncherTests
{
    [Theory]
    [InlineData("https://api.openai.com/", "https://api.openai.com/")]
    [InlineData("api.openai.com", "https://api.openai.com/")]
    [InlineData("http://127.0.0.1:7890", "http://127.0.0.1:7890/")]
    public void TryNormalizeUrl_AcceptsHttp(string input, string expectedPrefix)
    {
        Assert.True(ShellBrowserLauncher.TryNormalizeUrl(input, out var n));
        Assert.StartsWith(expectedPrefix.TrimEnd('/'), n.TrimEnd('/'));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ftp://x")]
    [InlineData("not a url :::")]
    public void TryNormalizeUrl_RejectsBad(string? input) =>
        Assert.False(ShellBrowserLauncher.TryNormalizeUrl(input, out _));

    [Fact]
    public void Open_CallsInjectedAction_WithNormalizedUrl()
    {
        string? seen = null;
        var launcher = new ShellBrowserLauncher(u => seen = u);
        launcher.Open("api2.cursor.sh");
        Assert.Equal("https://api2.cursor.sh/", seen);
    }

    [Fact]
    public void Open_Invalid_Throws()
    {
        var launcher = new ShellBrowserLauncher(_ => { });
        Assert.Throws<ArgumentException>(() => launcher.Open("ftp://bad"));
    }
}
