using System.Diagnostics;
using NetStrata.Core.Cli;

namespace NetStrata.Core.Tests.Cli;

public sealed class DaemonLifecycleManagerTests
{
    [Fact]
    public void GetStatus_PortListening_ExternalMode()
    {
        var mgr = new DaemonLifecycleManager(
            () => "netstrata",
            _ => throw new InvalidOperationException(),
            port => port == 8787);

        var status = mgr.GetStatus(8787);
        Assert.Equal("external", status.Mode);
        Assert.True(status.PortListening);
    }

    [Fact]
    public async Task StartAsync_PortBusy_Fails()
    {
        var mgr = new DaemonLifecycleManager(
            () => @"C:\app\netstrata.exe",
            _ => throw new InvalidOperationException(),
            _ => true);

        var (ok, err) = await mgr.StartAsync(8787);
        Assert.False(ok);
        Assert.Contains("8787", err);
    }

    [Fact]
    public async Task StartAsync_SpawnsWebProcess()
    {
        ProcessStartInfo? captured = null;
        var fake = new FakeHandle();
        var mgr = new DaemonLifecycleManager(
            () => @"C:\app\netstrata.exe",
            info =>
            {
                captured = info;
                return fake;
            },
            _ => false);

        var (ok, err) = await mgr.StartAsync(9001);
        Assert.True(ok);
        Assert.Null(err);
        Assert.Equal("--web", captured!.Arguments);
        Assert.Equal("1", captured.Environment["NETSTRATA_NO_OPEN"]);
        Assert.Equal("9001", captured.Environment["NETSTRATA_PORT"]);

        Assert.Equal("owned", mgr.GetStatus(9001).Mode);
        mgr.StopOwned();
        Assert.True(fake.Killed);
    }

    [Fact]
    public void BuildWebStartInfo_Dll_SetsEnv()
    {
        var info = DaemonLifecycleManager.BuildWebStartInfo(@"C:\x\netstrata.dll", 8787);
        Assert.NotNull(info);
        Assert.Equal("dotnet", info!.FileName);
        Assert.Equal("1", info.Environment["NETSTRATA_NO_OPEN"]);
    }

    private sealed class FakeHandle : IOwnedProcessHandle
    {
        public bool Killed { get; private set; }
        public bool HasExited => Killed;
        public void Kill(bool entireProcessTree = true) => Killed = true;
        public void WaitForExit(int milliseconds) { }
        public void Dispose() { }
    }
}
