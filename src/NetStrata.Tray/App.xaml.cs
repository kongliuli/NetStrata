using System.IO;
using System.Windows;
using System.Windows.Threading;
using NetStrata.Tray.Cli;
using NetStrata.Tray.Services;
using NetStrata.Tray.Views;

namespace NetStrata.Tray;

public partial class App : System.Windows.Application
{
    private TrayHost? _tray;
    private MainWindow? _main;
    private static bool _errorShown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogFatal(args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));

        base.OnStartup(e);

        if (CommandDispatcher.IsCliMode(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var code = await CommandDispatcher.RunAsync(e.Args);
            Shutdown(code);
            return;
        }

        NativeConsole.FreeConsole();
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        try
        {
            ThemeApplier.ApplyHandySkin();

            _tray = new TrayHost();
            _main = new MainWindow(
                probeNow: () => _tray.RequestProbe(),
                restartDaemon: opts => _tray.RestartDaemonWithOptionsAsync(opts));
            MainWindow = _main;
            _main.Show();
            _tray.AttachMainWindow(_main);
            _tray.Start();
        }
        catch (Exception ex)
        {
            LogFatal(ex);
            ShowOnce(ex);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogFatal(e.Exception);
        ShowOnce(e.Exception);
        e.Handled = true;
    }

    private static void ShowOnce(Exception ex)
    {
        if (_errorShown)
            return;
        _errorShown = true;
        var msg = ex.GetBaseException().Message;
        System.Windows.MessageBox.Show(
            msg + "\n\n详情已写入 %TEMP%\\netstrata-crash.log",
            "NetStrata",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void LogFatal(Exception? ex)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "netstrata-crash.log");
            File.WriteAllText(path, (ex?.ToString() ?? "null") + Environment.NewLine);
        }
        catch
        {
            // ignore
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
