using System.Diagnostics;

namespace NetStrata.Core.Cli;

public sealed record DaemonStatus(
    bool PortListening,
    bool OwnedRunning,
    string Mode)
{
    public bool IsRunning => PortListening;
    public string Label => Mode switch
    {
        "owned" => "Daemon 运行中（本托盘启动）",
        "external" => "Daemon 运行中（外部进程）",
        _ => "Daemon 未运行"
    };
}

public interface IOwnedProcessHandle : IDisposable
{
    bool HasExited { get; }
    void Kill(bool entireProcessTree = true);
    void WaitForExit(int milliseconds);
}

internal sealed class ProcessHandle(Process process) : IOwnedProcessHandle
{
    public bool HasExited => process.HasExited;

    public void Kill(bool entireProcessTree = true) => process.Kill(entireProcessTree);

    public void WaitForExit(int milliseconds) => process.WaitForExit(milliseconds);

    public void Dispose() => process.Dispose();
}

public interface IDaemonLifecycle
{
    DaemonStatus GetStatus(int port);
    Task<(bool Ok, string? Error)> StartAsync(int port, CancellationToken ct = default);
    void StopOwned();
}

public sealed class DaemonLifecycleManager : IDaemonLifecycle
{
    private readonly Func<string> _resolveExecutable;
    private readonly Func<ProcessStartInfo, IOwnedProcessHandle> _startProcess;
    private readonly Func<int, bool> _isPortListening;
    private IOwnedProcessHandle? _owned;

    public DaemonLifecycleManager(
        Func<string>? resolveExecutable = null,
        Func<ProcessStartInfo, IOwnedProcessHandle>? startProcess = null,
        Func<int, bool>? isPortListening = null)
    {
        _resolveExecutable = resolveExecutable ?? (() => CliPathResolver.ResolveExecutable());
        _startProcess = startProcess ?? (info => new ProcessHandle(Process.Start(info)!));
        _isPortListening = isPortListening ?? PortListener.IsTcpListening;
    }

    public DaemonStatus GetStatus(int port)
    {
        CleanupOwned();
        var listening = _isPortListening(port);
        var owned = _owned is not null && !_owned.HasExited;
        var mode = owned ? "owned" : listening ? "external" : "stopped";
        return new DaemonStatus(listening, owned, mode);
    }

    public Task<(bool Ok, string? Error)> StartAsync(int port, CancellationToken ct = default)
    {
        CleanupOwned();
        if (_isPortListening(port))
            return Task.FromResult<(bool, string?)>((false, $"端口 {port} 已被占用"));

        var info = BuildWebStartInfo(_resolveExecutable(), port);
        if (info is null)
            return Task.FromResult<(bool, string?)>((false, "找不到 netstrata 可执行文件"));

        try
        {
            _owned = _startProcess(info);
            return Task.FromResult<(bool, string?)>((true, null));
        }
        catch (Exception ex)
        {
            return Task.FromResult<(bool, string?)>((false, ex.Message));
        }
    }

    public void StopOwned()
    {
        if (_owned is null)
            return;

        try
        {
            if (!_owned.HasExited)
            {
                _owned.Kill(entireProcessTree: true);
                _owned.WaitForExit(3000);
            }
        }
        catch
        {
            // ponytail: best-effort stop
        }
        finally
        {
            _owned.Dispose();
            _owned = null;
        }
    }

    private void CleanupOwned()
    {
        if (_owned is not null && _owned.HasExited)
        {
            _owned.Dispose();
            _owned = null;
        }
    }

    public static ProcessStartInfo? BuildWebStartInfo(string exe, int port)
    {
        ProcessStartInfo? Base(string file, string args) => new(file, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (exe.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var info = Base("dotnet", $"\"{exe}\" --web");
            info.Environment["NETSTRATA_NO_OPEN"] = "1";
            info.Environment["NETSTRATA_PORT"] = port.ToString();
            return info;
        }

        if (exe.Equals("netstrata", StringComparison.OrdinalIgnoreCase)
            || exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || File.Exists(exe))
        {
            var info = Base(exe, "--web");
            info.Environment["NETSTRATA_NO_OPEN"] = "1";
            info.Environment["NETSTRATA_PORT"] = port.ToString();
            return info;
        }

        return null;
    }
}
