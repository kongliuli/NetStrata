using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using NetStrata.Core.Config;
using NetStrata.Core.Storage;
using NetStrata.Core.Tui;

namespace NetStrata.Tray.Services;

internal sealed class TrayHost : IDisposable
{
    private readonly NotifyIcon _icon = new() { Visible = true, Text = "NetStrata" };
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private readonly JsonSampleStorage _storage = new();
    private readonly ContextMenuStrip _menu;

    public TrayHost()
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("打开仪表盘", null, (_, _) => OpenDashboard());
        _menu.Items.Add("立即探测 (--once)", null, (_, _) => RunOnce());
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
            TrayIconFactory.Apply(_icon, tray);
        }
        catch
        {
            TrayIconFactory.Apply(_icon, TrayStatusMapper.Map(null));
        }
    }

    private static void OpenDashboard()
    {
        var port = NetStrataOptions.FromEnvironment().Port;
        Process.Start(new ProcessStartInfo($"http://localhost:{port}") { UseShellExecute = true });
    }

    private static void RunOnce()
    {
        var cli = Path.Combine(AppContext.BaseDirectory, "netstrata.exe");
        if (!File.Exists(cli))
            cli = "netstrata";
        Process.Start(new ProcessStartInfo(cli, "--once")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static void Shutdown()
    {
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _timer.Stop();
        _icon.Visible = false;
        _icon.Icon?.Dispose();
        _icon.Dispose();
        _menu.Dispose();
    }
}
