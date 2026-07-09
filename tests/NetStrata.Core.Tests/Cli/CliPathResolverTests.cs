using NetStrata.Core.Cli;

namespace NetStrata.Core.Tests.Cli;

public sealed class CliPathResolverTests
{
    [Fact]
    public void ResolveExecutable_PrefersColocatedExe()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netstrata-cli-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var exe = Path.Combine(dir, "netstrata.exe");
        File.WriteAllText(exe, "");
        try
        {
            Assert.Equal(exe, CliPathResolver.ResolveExecutable(dir));
        }
        finally
        {
            File.Delete(exe);
            Directory.Delete(dir);
        }
    }

    [Fact]
    public void CandidatePaths_WalksUpToRepoLayout()
    {
        var paths = CliPathResolver.CandidatePaths(AppContext.BaseDirectory).ToList();
        Assert.Contains(paths, p => p.Contains("NetStrata.Cli"));
    }
}
