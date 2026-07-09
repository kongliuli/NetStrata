using System.Windows;
using NetStrata.Tray.Services;

namespace NetStrata.Tray;

public partial class App : System.Windows.Application
{
    private TrayHost? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _tray = new TrayHost();
        _tray.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
