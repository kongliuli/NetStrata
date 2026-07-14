using NetStrata.Core.Config;

namespace NetStrata.Core.Cli;

public sealed record DaemonStatus(bool OwnedRunning, string Mode)
{
    public string Label => Mode switch
    {
        "owned" => "Daemon 运行中",
        _ => "Daemon 未运行"
    };
}

public interface IDaemonLifecycle
{
    DaemonStatus GetStatus();
    Task<(bool Ok, string? Error)> StartAsync(CancellationToken ct = default);
    void Stop();
    Task<(bool Ok, string? Error)> RestartAsync(NetStrataOptions options, CancellationToken ct = default);
}

/// <summary>
/// Runs the probe loop in-process via an injected delegate (no Process.Start).
/// </summary>
public sealed class InProcessDaemonController : IDaemonLifecycle, IDisposable
{
    private readonly Func<NetStrataOptions, CancellationToken, Task> _runLoop;
    private readonly object _gate = new();
    private NetStrataOptions _options;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public InProcessDaemonController(
        Func<NetStrataOptions, CancellationToken, Task> runLoop,
        NetStrataOptions? options = null)
    {
        _runLoop = runLoop ?? throw new ArgumentNullException(nameof(runLoop));
        _options = options ?? NetStrataOptions.FromEnvironment();
    }

    public DaemonStatus GetStatus()
    {
        lock (_gate)
        {
            var running = _cts is not null && _loopTask is { IsCompleted: false };
            return new DaemonStatus(running, running ? "owned" : "stopped");
        }
    }

    public Task<(bool Ok, string? Error)> StartAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_cts is not null && _loopTask is { IsCompleted: false })
                return Task.FromResult<(bool, string?)>((false, "Daemon 已在运行"));

            CleanupUnlocked();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var options = _options;
            _loopTask = Task.Run(async () =>
            {
                try
                {
                    await _runLoop(options, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // expected on Stop
                }
            }, CancellationToken.None);
        }

        return Task.FromResult<(bool, string?)>((true, null));
    }

    public void Stop()
    {
        lock (_gate)
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
                // ponytail: best-effort stop
            }

            var task = _loopTask;
            if (task is not null)
            {
                try
                {
                    task.Wait(TimeSpan.FromSeconds(3));
                }
                catch
                {
                    // ignore aggregate cancel
                }
            }

            CleanupUnlocked();
        }
    }

    public async Task<(bool Ok, string? Error)> RestartAsync(NetStrataOptions options, CancellationToken ct = default)
    {
        Stop();
        lock (_gate)
            _options = options;
        return await StartAsync(ct);
    }

    public void Dispose() => Stop();

    private void CleanupUnlocked()
    {
        _cts?.Dispose();
        _cts = null;
        _loopTask = null;
    }
}
