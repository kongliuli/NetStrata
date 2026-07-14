using NetStrata.Core.Cli;
using NetStrata.Core.Config;

namespace NetStrata.Core.Tests.Cli;

public sealed class InProcessDaemonControllerTests
{
    [Fact]
    public async Task StartAsync_RunsInjectedLoop_StopCancels()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var mgr = new InProcessDaemonController(async (_, ct) =>
        {
            started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }
        }, new NetStrataOptions { IntervalMs = 1000 });

        Assert.Equal("stopped", mgr.GetStatus().Mode);

        var (ok, err) = await mgr.StartAsync();
        Assert.True(ok);
        Assert.Null(err);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(mgr.GetStatus().OwnedRunning);
        Assert.Equal("owned", mgr.GetStatus().Mode);

        var (againOk, againErr) = await mgr.StartAsync();
        Assert.False(againOk);
        Assert.Contains("已在运行", againErr);

        mgr.Stop();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(mgr.GetStatus().OwnedRunning);
    }

    [Fact]
    public async Task RestartAsync_PassesNewOptionsToLoop()
    {
        NetStrataOptions? seen = null;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var mgr = new InProcessDaemonController(async (opts, ct) =>
        {
            seen = opts;
            gate.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
        }, new NetStrataOptions { IntervalMs = 1000 });

        await mgr.StartAsync();
        await gate.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1000, seen!.IntervalMs);

        gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        seen = null;
        var (ok, _) = await mgr.RestartAsync(new NetStrataOptions { IntervalMs = 2500 });
        Assert.True(ok);
        await gate.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2500, seen!.IntervalMs);

        mgr.Dispose();
    }
}
