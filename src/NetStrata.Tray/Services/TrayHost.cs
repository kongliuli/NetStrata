using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using NetStrata.Core.Cli;
using NetStrata.Core.Config;
using NetStrata.Core.Storage;
using NetStrata.Core.Models;
using NetStrata.Core.Tui;

namespace NetStrata.Tray.Services;

internal sealed class TrayHost : IDisposable
{
    private readonly NotifyIcon _icon = new() { Visible = true, Text = "NetStrata" };
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private readonly JsonSampleStorage _storage = new();
    private readonly IOnceProbeRunner _probeRunner;
    private readonly IDaemonLifecycle _daemon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _daemonItem;
    private readonly ToolStripMenuItem _probeItem;
    private Views.DashboardWindow? _dashboard;
    private readonly Views.SettingsWindow? _settings;
    private readonly AlertWatchState _alerts = new();
    private bool _probing;

    public TrayHost(IOnceProbeRunner? probeRunner = null, IDaemonLifecycle? daemon = null)
    {
        _probeRunner = probeRunner ?? new OnceProbeRunner();
        _daemon = daemon ?? new DaemonLifecycleManager();
        _menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("等待数据…") { Enabled = false };
        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new ToolStripSeparator());

        _daemonItem = new ToolStripMenuItem("启动 Daemon", null, (_, _) => _ = ToggleDaemonAsync());
        _menu.Items.Add(_daemonItem);
        _probeItem = new ToolStripMenuItem("立即探测 (--once)", null, (_, _) => _ = RunOnceAsync());
        _menu.Items.Add(_probeItem);
        _menu.Items.Add("打开 Dashboard", null, (_, _) => OpenWpfDashboard());
        _menu.Items.Add("打开 Web 仪表盘", null, (_, _) => OpenWebDashboard());
        _menu.Items.Add("设置…", null, (_, _) => OpenSettings());
        _menu.Items.Add("复制 headline", null, (_, _) => CopyHeadline());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => Shutdown());
        _icon.ContextMenuStrip = _menu;

        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public void Start()
    {
        _ = RefreshAsync();
        _timer.Start();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var state = await _storage.ReadStateAsync(CancellationToken.None);
            var tray = TrayStatusMapper.MapFromState(state);
            if (state?.Latest?.Verdict?.Headline is { } headline)
                tray = tray with { Tooltip = headline };

            _statusItem.Text = state is null
                ? BuildDaemonStatusLine(null)
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
        var port = NetStrataOptions.FromEnvironment().Port;
        var ds = _daemon.GetStatus(port);
        if (ds.OwnedRunning)
        {
            _daemonItem.Text = "停止 Daemon";
            _daemonItem.Enabled = true;
        }
        else if (ds.Mode == "external")
        {
            _daemonItem.Text = $"Daemon 外部占用 :{port}";
            _daemonItem.Enabled = false;
        }
        else
        {
            _daemonItem.Text = "启动 Daemon (--web)";
            _daemonItem.Enabled = true;
        }
    }

    private string BuildDaemonStatusLine(DaemonState? state)
    {
        var port = NetStrataOptions.FromEnvironment().Port;
        var ds = _daemon.GetStatus(port);
        if (state is not null)
            return $"周期 #{state.Cycle} · {state.Latest?.Verdict?.Overall ?? "unknown"}";
        return ds.Label;
    }

    private async Task ToggleDaemonAsync()
    {
        var port = NetStrataOptions.FromEnvironment().Port;
        var ds = _daemon.GetStatus(port);
        if (ds.OwnedRunning)
        {
            _daemon.StopOwned();
            _icon.ShowBalloonTip(3000, "NetStrata", "Daemon 已停止", ToolTipIcon.Info);
            UpdateDaemonMenu();
            return;
        }

        _daemonItem.Enabled = false;
        _daemonItem.Text = "启动中…";
        var (ok, err) = await _daemon.StartAsync(port);
        if (ok)
            _icon.ShowBalloonTip(3000, "NetStrata", $"Daemon 已启动 :{port}", ToolTipIcon.Info);
        else
            _icon.ShowBalloonTip(5000, "NetStrata", err ?? "启动失败", ToolTipIcon.Error);

        UpdateDaemonMenu();
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
        }
        finally
        {
            _probing = false;
            _probeItem.Enabled = true;
            _probeItem.Text = "立即探测 (--once)";
        }
    }

    private void OpenSettings()
    {
        if (_settings is { IsLoaded: true })
        {
            _settings.Activate();
            return;
        }

        _settings = new Views.SettingsWindow();
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
    }

    private void OpenWpfDashboard()
    {
        if (_dashboard is { IsLoaded: true })
        {
            _dashboard.Activate();
            _dashboard.Focus();
            return;
        }

        _dashboard = new Views.DashboardWindow();
        _dashboard.Closed += (_, _) => _dashboard = null;
        _dashboard.Show();
    }

    private static void OpenWebDashboard()
    {
        var port = NetStrataOptions.FromEnvironment().Port;
        Process.Start(new ProcessStartInfo($"http://localhost:{port}") { UseShellExecute = true });
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
        _dashboard?.Close();
        _settings?.Close();
        _daemon.StopOwned();
    }
}
