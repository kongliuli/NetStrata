using NetStrata.Core.Cli;

namespace NetStrata.Core.Tests.Cli;

public sealed class CliArgsTests
{
    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--once" }, true)]
    [InlineData(new[] { "--export" }, true)]
    [InlineData(new[] { "--help" }, true)]
    [InlineData(new[] { "-h" }, true)]
    [InlineData(new[] { "--web" }, true)]
    [InlineData(new[] { "-w" }, true)]
    [InlineData(new[] { "--tui" }, true)]
    [InlineData(new[] { "--follow" }, true)]
    public void IsCliMode_MatchesPlan(string[] args, bool expected)
    {
        Assert.Equal(expected, CliArgs.IsCliMode(args));
    }

    [Fact]
    public void ParsePing_SplitsCommaList()
    {
        var ping = CliArgs.ParsePing(["--once", "--ping", "1.1.1.1,8.8.8.8"]);
        Assert.Equal(["1.1.1.1", "8.8.8.8"], ping);
    }
}
