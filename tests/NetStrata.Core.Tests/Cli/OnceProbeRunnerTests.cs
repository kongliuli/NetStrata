using System.Diagnostics;
using NetStrata.Core.Cli;

namespace NetStrata.Core.Tests.Cli;

public sealed class OnceProbeRunnerTests
{
    [Fact]
    public async Task RunAsync_UsesResolvedExeAndParsesStdout()
    {
        const string json = """
            {"verdict":{"overall":"healthy","headline":"all green","layers":[],"ai":{"state":"ok","headline":"ok"}}}
            """;

        ProcessStartInfo? captured = null;
        var runner = new OnceProbeRunner(
            () => @"C:\fake\netstrata.exe",
            (info, _) =>
            {
                captured = info;
                return Task.FromResult((0, json));
            });

        var result = await runner.RunAsync();
        Assert.True(result.Ok);
        Assert.Equal("healthy", result.Overall);
        Assert.Equal(@"C:\fake\netstrata.exe", captured!.FileName);
    }

    [Fact]
    public void BuildStartInfo_Dll_UsesDotnetHost()
    {
        var info = OnceProbeRunner.BuildStartInfo(@"C:\app\netstrata.dll");
        Assert.NotNull(info);
        Assert.Equal("dotnet", info!.FileName);
        Assert.Contains("netstrata.dll", info.Arguments);
    }
}
