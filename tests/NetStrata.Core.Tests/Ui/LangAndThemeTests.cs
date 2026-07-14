using NetStrata.Core.Probes;
using NetStrata.Core.Ui;

namespace NetStrata.Core.Tests.Ui;

public sealed class LangAndThemeTests
{
    [Theory]
    [InlineData(null, "zh")]
    [InlineData("", "zh")]
    [InlineData("auto", "zh")]
    [InlineData("zh", "zh")]
    [InlineData("zh-CN", "zh")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    public void LangResolver_DefaultsChinese(string? input, string expected) =>
        Assert.Equal(expected, LangResolver.Resolve(input));

    [Fact]
    public void ThemeResolver_SystemUsesFlag()
    {
        Assert.Equal(ThemeResolver.Light, ThemeResolver.Resolve(ThemeMode.System, systemIsDark: false));
        Assert.Equal(ThemeResolver.Dark, ThemeResolver.Resolve(ThemeMode.System, systemIsDark: true));
        Assert.Equal(ThemeResolver.Light, ThemeResolver.Resolve(ThemeMode.Light, systemIsDark: true));
    }

    [Fact]
    public void AiApiCatalog_HasSixProviders()
    {
        Assert.Equal(6, AiApiCatalog.Providers.Length);
        Assert.Contains(AiApiCatalog.Providers, p => p.Id == "openai");
        Assert.Contains(AiApiCatalog.Providers, p => p.Id == "cursor");
        Assert.Contains(AiApiCatalog.Providers, p => p.Id == "opencode");
        Assert.Contains(AiApiCatalog.Providers, p => p.Id == "google");
        Assert.Contains(AiApiCatalog.Providers, p => p.Id == "github");
        Assert.Contains(AiApiCatalog.Providers, p => p.Id == "anthropic");
        Assert.Contains(HttpsProbe.DirectTargets, t => t.Label == "cursor_direct");
        Assert.Contains(HttpsProbe.ProxyTargets, t => t.Label == "anthropic_proxy");

        var ant = AiApiCatalog.Find("anthropic")!;
        Assert.Equal("https://api.anthropic.com/", ant.ProbeUrl);
        Assert.Equal("https://claude.ai/", ant.OpenUrl);
        var google = AiApiCatalog.Find("google")!;
        Assert.Equal("https://generativelanguage.googleapis.com/", google.ProbeUrl);
        Assert.Equal("https://aistudio.google.com/", google.OpenUrl);
        Assert.NotEqual(ant.ProbeUrl, ant.OpenUrl);
    }
}
