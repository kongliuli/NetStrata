using NetStrata.Core.Config;

namespace NetStrata.Core.Tests.Config;

public sealed class PingTargetValidatorTests
{
    [Theory]
    [InlineData("223.5.5.5", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("::1", true)]
    [InlineData("2001:db8::1", true)]
    [InlineData("nas.local", true)]
    [InlineData("a-b.c", true)]
    [InlineData("", false)]
    [InlineData("not a host!", false)]
    [InlineData("-bad.com", false)]
    public void IsValid_AcceptsExpectedTargets(string target, bool expected) =>
        Assert.Equal(expected, PingTargetValidator.IsValid(target));

    [Fact]
    public void Filter_Dedupes_CapsAndSkipsInvalid()
    {
        var skipped = new List<string>();
        var result = PingTargetValidator.Filter(
            ["223.5.5.5", "223.5.5.5", "bad host", "1.1.1.1", "8.8.8.8", "9.9.9.9", "10.0.0.1",
                "10.0.0.2", "10.0.0.3", "10.0.0.4", "10.0.0.5", "10.0.0.6", "10.0.0.7"],
            max: 10,
            skipped.Add);

        Assert.Equal(10, result.Count);
        Assert.Contains("invalid ping target skipped: bad host", skipped);
        Assert.Contains(skipped, s => s.Contains("cap 10"));
    }
}
