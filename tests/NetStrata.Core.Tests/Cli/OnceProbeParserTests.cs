using NetStrata.Core.Cli;

namespace NetStrata.Core.Tests.Cli;

public sealed class OnceProbeParserTests
{
    [Fact]
    public void Parse_ValidJson_ExtractsVerdict()
    {
        const string json = """
            {"verdict":{"overall":"degraded","headline":"dns blocked","layers":[],"ai":{"state":"ok","headline":"ok"}}}
            """;

        var result = OnceProbeParser.Parse(json);
        Assert.True(result.Ok);
        Assert.Equal("degraded", result.Overall);
        Assert.Equal("dns blocked", result.Headline);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsError()
    {
        var result = OnceProbeParser.Parse("not json");
        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }
}
