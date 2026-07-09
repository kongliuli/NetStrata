using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using NetStrata.Core.Cli;
using NetStrata.Core.Config;
using NetStrata.Core.Storage;
using NetStrata.Core.Tui;

namespace NetStrata.Tray.Services;

internal sealed class TrayHost : IDisposable
{
    private readonly NotifyIcon _icon = new() { Visible = true, Text = "NetStrata" };
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private readonly JsonSampleStorage _storage = new();
    private readonly IOnceProbeRunner _probeRunner;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _probeItem;
    private bool _probing;

    public TrayHost(IOnceProbeRunner? probeRunner = null)
    {
        _probeRunner = probeRunner ?? new OnceProbeRunner();
        _menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("等待数据…") { Enabled = false };
        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new ToolStripSeparator());

        _probeItem = new ToolStripMenuItem("立即探测 (--once)", null, (_, _) => _ = RunOnceAsync());
        _menu.Items.Add(_probeItem);
        _menu.Items.Add("打开仪表盘", null, (_, _) => OpenDashboard());
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
                ? "等待 Daemon 数据…"
                : $"周期 #{state.Cycle} · {state.Latest?.Verdict?.Overall ?? "unknown"}";
            _lastHeadline = state?.Latest?.Verdict?.Headline;

            TrayIconFactory.Apply(_icon, tray);
        }
        catch
        {
            _statusItem.Text = "读取 state.json 失败";
            TrayIconFactory.Apply(_icon, TrayStatusMapper.Map(null));
        }
    }

    private string? _lastHeadline;

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

    private static void OpenDashboard()
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
    }
}
