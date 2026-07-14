using System.Windows.Forms;
using NetStrata.Core.Cli;
using NetStrata.Core.Collector;
using NetStrata.Core.Config;
using NetStrata.Core.Storage;
using NetStrata.Core.Tui;
using NetStrata.Daemon;
using NetStrata.Tray.Views;

namespace NetStrata.Tray.Services;

internal sealed class TrayHost : IDisposable
{
    private readonly NotifyIcon _icon = new() { Visible = true, Text = "NetStrata" };
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private readonly JsonSampleStorage _storage = new();
    private readonly IOnceProbeRunner _probeRunner;
    private readonly InProcessDaemonController _daemon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _daemonItem;
    private readonly ToolStripMenuItem _probeItem;
    private MainWindow? _main;
    private Views.SettingsWindow? _settings;
    private readonly AlertWatchState _alerts = new();
    private bool _probing;

    public TrayHost(IOnceProbeRunner? probeRunner = null, InProcessDaemonController? daemon = null)
    {
        _probeRunner = probeRunner ?? new OnceProbeRunner();
        _daemon = daemon ?? CreateDefaultDaemon();
        _menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("等待数据…") { Enabled = false };
        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new ToolStripSeparator());

        _daemonItem = new ToolStripMenuItem("启动 Daemon", null, (_, _) => _ = ToggleDaemonAsync());
        _menu.Items.Add(_daemonItem);
        _probeItem = new ToolStripMenuItem("立即探测", null, (_, _) => _ = RunOnceAsync());
        _menu.Items.Add(_probeItem);
        _menu.Items.Add("打开主窗口", null, (_, _) => ShowMain());
        _menu.Items.Add("设置…", null, (_, _) => OpenSettings());
        _menu.Items.Add("复制 headline", null, (_, _) => CopyHeadline());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => Shutdown());
        _icon.ContextMenuStrip = _menu;
        _icon.DoubleClick += (_, _) => ShowMain();

        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public void AttachMainWindow(MainWindow main) => _main = main;

    public void RequestProbe() => _ = RunOnceAsync();

    public void Start()
    {
        _ = StartDaemonOnLaunchAsync();
        _ = RefreshAsync();
        _timer.Start();
    }

    private async Task StartDaemonOnLaunchAsync()
    {
        var (ok, err) = await _daemon.StartAsync();
        if (!ok)
            _icon.ShowBalloonTip(5000, "NetStrata", err ?? "Daemon 启动失败", ToolTipIcon.Error);
        UpdateDaemonMenu();
        await RefreshAsync();
        _ = _main?.RefreshAsync();
    }

    private static InProcessDaemonController CreateDefaultDaemon() =>
        new((options, ct) =>
        {
            DataDirectory.EnsureExists();
            var storage = new JsonSampleStorage(options.DataDir);
            var daemon = new ProbeDaemon(new SampleCollector(), storage, options);
            return daemon.ProbeLoopAsync(ct);
        }, NetStrataOptions.FromEnvironment());

    private async Task RefreshAsync()
    {
        try
        {
            var state = await _storage.ReadStateAsync(CancellationToken.None);
            var tray = TrayStatusMapper.MapFromState(state);
            if (state?.Latest?.Verdict?.Headline is { } headline)
                tray = tray with { Tooltip = headline };

            _statusItem.Text = state is null
                ? BuildDaemonStatusLine()
                : $"周期 #{state.Cycle} · {state.Latest?.Verdict?.Overall ?? "unknown"}";
            _lastHeadline = state?.Latest?.Verdict?.Headline;

            foreach (var alert in _alerts.ConsumeNew(state))
            {
                _icon.ShowBalloonTip(
                    8000,
                    $"NetStrata 告警 · {alert.Type}",
                    alert.Message,
                    ToolTipIcon.Warning);
            }

            UpdateDaemonMenu();
            TrayIconFactory.Apply(_icon, tray);
        }
        catch
        {
            _statusItem.Text = "读取 state.json 失败";
            UpdateDaemonMenu();
            TrayIconFactory.Apply(_icon, TrayStatusMapper.Map(null));
        }
    }

    private string? _lastHeadline;

    private void UpdateDaemonMenu()
    {
        var ds = _daemon.GetStatus();
        if (ds.OwnedRunning)
        {
            _daemonItem.Text = "停止 Daemon";
            _daemonItem.Enabled = true;
        }
        else
        {
            _daemonItem.Text = "启动 Daemon";
            _daemonItem.Enabled = true;
        }
    }

    private string BuildDaemonStatusLine() => _daemon.GetStatus().Label;

    private async Task ToggleDaemonAsync()
    {
        var ds = _daemon.GetStatus();
        if (ds.OwnedRunning)
        {
            _daemon.Stop();
            _icon.ShowBalloonTip(3000, "NetStrata", "Daemon 已停止", ToolTipIcon.Info);
            UpdateDaemonMenu();
            return;
        }

        _daemonItem.Enabled = false;
        _daemonItem.Text = "启动中…";
        var (ok, err) = await _daemon.StartAsync();
        if (ok)
            _icon.ShowBalloonTip(3000, "NetStrata", "Daemon 已启动", ToolTipIcon.Info);
        else
            _icon.ShowBalloonTip(5000, "NetStrata", err ?? "启动失败", ToolTipIcon.Error);

        UpdateDaemonMenu();
    }

    internal Task<(bool Ok, string? Error)> RestartDaemonWithOptionsAsync(NetStrataOptions options) =>
        RestartDaemonCoreAsync(options);

    private async Task<(bool Ok, string? Error)> RestartDaemonCoreAsync(NetStrataOptions options)
    {
        var result = await _daemon.RestartAsync(options);
        UpdateDaemonMenu();
        _ = _main?.RefreshAsync();
        return result;
    }

    private async Task RunOnceAsync()
    {
        if (_probing)
            return;

        _probing = true;
        _probeItem.Enabled = false;
        _probeItem.Text = "探测中…";
        try
        {
            var result = await _probeRunner.RunAsync();
            if (result.Ok)
            {
                var msg = $"{result.Overall}: {result.Headline}";
                _icon.ShowBalloonTip(5000, "NetStrata 探测完成", msg, ToolTipIcon.Info);
                _lastHeadline = result.Headline;
            }
            else
            {
                _icon.ShowBalloonTip(5000, "NetStrata 探测失败", result.Error ?? "unknown", ToolTipIcon.Error);
            }

            await RefreshAsync();
            _ = _main?.RefreshAsync();
        }
        finally
        {
            _probing = false;
            _probeItem.Enabled = true;
            _probeItem.Text = "立即探测";
        }
    }

    private void ShowMain()
    {
        if (_main is null)
            return;
        if (!_main.IsVisible)
            _main.Show();
        if (_main.WindowState == System.Windows.WindowState.Minimized)
            _main.WindowState = System.Windows.WindowState.Normal;
        _main.Activate();
        _ = _main.RefreshAsync();
    }

    private void OpenSettings()
    {
        if (_settings is { IsLoaded: true })
        {
            _settings.Activate();
            return;
        }

        _settings = new Views.SettingsWindow(RestartDaemonWithOptionsAsync);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
    }

    private void CopyHeadline()
    {
        if (string.IsNullOrWhiteSpace(_lastHeadline))
            return;
        Clipboard.SetText(_lastHeadline);
        _icon.ShowBalloonTip(2000, "NetStrata", "已复制 headline", ToolTipIcon.None);
    }

    private static void Shutdown() => System.Windows.Application.Current.Shutdown();

    public void Dispose()
    {
        _timer.Stop();
        _icon.Visible = false;
        _icon.Icon?.Dispose();
        _icon.Dispose();
        _menu.Dispose();
        _settings?.Close();
        _daemon.Dispose();
    }
}
