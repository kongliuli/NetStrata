using NetStrata.Core.Judge;
using NetStrata.Core.Tui;

using NetStrata.Core.Tests.Judge;

namespace NetStrata.Core.Tests.Tui;

public sealed class StatusFormatterTests
{
    [Fact]
    public void FormatLayers_ExcludesAiLayer()
    {
        var verdict = new VerdictEngine().Judge(SampleBuilder.Healthy());
        var layers = StatusFormatter.FormatLayers(verdict);
        Assert.DoesNotContain(layers, l => l.Layer == "ai");
        Assert.Equal(5, layers.Count);
    }

    [Fact]
    public void StateGlyph_Ok_IsGreenMarker()
    {
        Assert.Contains("OK", StatusFormatter.StateGlyph("ok"));
    }
}
