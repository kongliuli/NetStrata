using System.Windows.Forms;
using NetStrata.Core.Cli;
using NetStrata.Core.Collector;
using NetStrata.Core.Config;
using NetStrata.Core.Storage;
using NetStrata.Core.Tui;
using NetStrata.Core.Ui;
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
    private readonly ToolStripMenuItem _openMainItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _copyItem;
    private readonly ToolStripMenuItem _exitItem;
    private MainWindow? _main;
    private Views.SettingsWindow? _settings;
    private readonly AlertWatchState _alerts = new();
    private bool _probing;
    private string _lang = "zh";

    public TrayHost(IOnceProbeRunner? probeRunner = null, InProcessDaemonController? daemon = null)
    {
        _probeRunner = probeRunner ?? new OnceProbeRunner();
        _daemon = daemon ?? CreateDefaultDaemon();
        _lang = LangResolver.Resolve(NetStrataOptions.FromEnvironment().Lang);
        _menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem(UiStrings.TrayWaiting(_lang)) { Enabled = false };
        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new ToolStripSeparator());

        _daemonItem = new ToolStripMenuItem(UiStrings.TrayStartDaemon(_lang), null, (_, _) => _ = ToggleDaemonAsync());
        _menu.Items.Add(_daemonItem);
        _probeItem = new ToolStripMenuItem(UiStrings.TrayProbeNow(_lang), null, (_, _) => _ = RunOnceAsync());
        _menu.Items.Add(_probeItem);
        _openMainItem = new ToolStripMenuItem(UiStrings.TrayOpenMain(_lang), null, (_, _) => ShowMain());
        _menu.Items.Add(_openMainItem);
        _settingsItem = new ToolStripMenuItem(UiStrings.TraySettings(_lang), null, (_, _) => OpenSettings());
        _menu.Items.Add(_settingsItem);
        _copyItem = new ToolStripMenuItem(UiStrings.TrayCopyHeadline(_lang), null, (_, _) => CopyHeadline());
        _menu.Items.Add(_copyItem);
        _menu.Items.Add(new ToolStripSeparator());
        _exitItem = new ToolStripMenuItem(UiStrings.TrayExit(_lang), null, (_, _) => Shutdown());
        _menu.Items.Add(_exitItem);
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
        _lang = LangResolver.Resolve(NetStrataOptions.FromEnvironment().Lang);
        var ds = _daemon.GetStatus();
        if (ds.OwnedRunning)
        {
            _daemonItem.Text = UiStrings.TrayStopDaemon(_lang);
            _daemonItem.Enabled = true;
        }
        else
        {
            _daemonItem.Text = UiStrings.TrayStartDaemon(_lang);
            _daemonItem.Enabled = true;
        }

        _openMainItem.Text = UiStrings.TrayOpenMain(_lang);
        _settingsItem.Text = UiStrings.TraySettings(_lang);
        _copyItem.Text = UiStrings.TrayCopyHeadline(_lang);
        _exitItem.Text = UiStrings.TrayExit(_lang);
        if (!_probing)
            _probeItem.Text = UiStrings.TrayProbeNow(_lang);
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
            _probeItem.Text = UiStrings.TrayProbeNow(_lang);
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
