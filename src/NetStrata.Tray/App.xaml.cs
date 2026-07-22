using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using NetStrata.Core.Config;
using NetStrata.Core.Storage;
using NetStrata.Core.Tray;
using NetStrata.Core.Ui;
using NetStrata.Tray.Cli;
using NetStrata.Tray.Services;
using NetStrata.Tray.Views;

namespace NetStrata.Tray;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Global\NetStrata.Tray";
    private TrayHost? _tray;
    private MainWindow? _main;
    private Mutex? _singleInstance;
    private EventWaitHandle? _showMainEvent;
    private CancellationTokenSource? _showMainCts;
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

        // ponytail: GUI only — CLI (--once/--export/…) may run alongside tray
        _singleInstance = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            // W8c: wake the first instance instead of only showing a dialog
            if (!ShowMainSignal.TrySignalExisting())
            {
                System.Windows.MessageBox.Show(
                    UiStrings.AlreadyRunning("zh"),
                    "NetStrata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            Shutdown(0);
            return;
        }

        NativeConsole.FreeConsole();
        // ponytail: tray-resident — close main window hides; only tray Exit shuts down
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        StartShowMainWatcher();

        try
        {
            ThemeApplier.ApplyHandySkin();

            _tray = new TrayHost();
            _main = new MainWindow(
                probeNow: () => _tray.RequestProbe(),
                openSettings: () => _tray.OpenSettings(),
                restartDaemon: opts => _tray.RestartDaemonWithOptionsAsync(opts));
            MainWindow = _main;
            _tray.AttachMainWindow(_main);
            // W11b: ProgressButton busy + Growl share tray probe pipeline
            _tray.ProbeBusyChanged += busy => _main.SetProbeBusy(busy);
            _tray.ProbeFinished += (ok, msg) => _main.ShowProbeResult(ok, msg);
            // W8d: login / quiet start — tray + daemon only until user opens from tray
            var startMinimized = UserConfigLoader.Load(DataDirectory.ConfigPath).StartMinimized;
            if (!startMinimized)
                _main.Show();
            _tray.Start();
        }
        catch (Exception ex)
        {
            LogFatal(ex);
            ShowOnce(ex);
            Shutdown(1);
        }
    }

    private void StartShowMainWatcher()
    {
        _showMainEvent = ShowMainSignal.CreateListener();
        _showMainCts = new CancellationTokenSource();
        var ct = _showMainCts.Token;
        var ev = _showMainEvent;
        // ponytail: background WaitOne; ceiling = process lifetime (dispose on Exit)
        _ = Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                if (!ev.WaitOne(500))
                    continue;
                Dispatcher.BeginInvoke(() => _tray?.ShowMain());
            }
        }, ct);
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
        _showMainCts?.Cancel();
        _showMainCts?.Dispose();
        _showMainCts = null;
        _showMainEvent?.Dispose();
        _showMainEvent = null;
        _tray?.Dispose();
        if (_singleInstance is not null)
        {
            try { _singleInstance.ReleaseMutex(); } catch { /* ignore */ }
            _singleInstance.Dispose();
            _singleInstance = null;
        }
        base.OnExit(e);
    }
}

