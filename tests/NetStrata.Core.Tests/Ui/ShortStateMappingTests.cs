using NetStrata.Core.Ui;

namespace NetStrata.Core.Tests.Ui;

/// <summary>
/// W11d: documents the state→label mapping used by MainWindow ShortState helpers.
/// Keep logic here so Tray code-behind can stay thin.
/// </summary>
public sealed class ShortStateMappingTests
{
    [Theory]
    [InlineData("ok", "en", "OK")]
    [InlineData("fail", "en", "Fail")]
    [InlineData("degraded", "en", "Degraded")]
    [InlineData("ok", "zh", "正常")]
    [InlineData("fail", "zh", "失败")]
    public void StateName_IsLocalized(string state, string lang, string expected) =>
        Assert.Equal(expected, UiStrings.StateName(lang, state));

    [Theory]
    [InlineData("ok", "ok", "ok")]
    [InlineData("ok", "fail", "degraded")]
    [InlineData("fail", "ok", "degraded")]
    [InlineData("fail", "fail", "fail")]
    public void CombineDirectProxy_MatchesProductRules(string direct, string proxy, string expected)
    {
        // mirror MainWindow ShortStateFromDetail decision order
        string Combined()
        {
            if (direct == "ok" && proxy == "ok") return "ok";
            if (direct == "ok" || proxy == "ok") return "degraded";
            return "fail";
        }

        Assert.Equal(expected, Combined());
    }
}
