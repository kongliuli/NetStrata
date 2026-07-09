using System.IO;
using System.Windows;
using NetStrata.Core.Config;
using NetStrata.Core.Storage;
using NetStrata.Core.Tray;
using NetStrata.Tray.Services;

namespace NetStrata.Tray.Views;

public partial class SettingsWindow : Window
{
    private readonly string _configPath = DataDirectory.ConfigPath;
    private readonly StartupShortcutService _startup = new(new WindowsStartupLinkWriter());

    public SettingsWindow()
    {
        InitializeComponent();
        LoadForm();
    }

    private void LoadForm()
    {
        var form = SettingsMapper.ToForm(UserConfigLoader.Load(_configPath));
        IntervalBox.Text = form.IntervalMs;
        PortBox.Text = form.Port;
        PingExtraBox.Text = form.PingExtra;
        PingLabelsBox.Text = form.PingLabels;
        TlsTargetsBox.Text = form.TlsStackTargets;
        StartupCheck.IsChecked = _startup.IsEnabled();
        StatusText.Text = _configPath;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = SettingsMapper.FromForm(new SettingsFormModel
            {
                IntervalMs = IntervalBox.Text,
                Port = PortBox.Text,
                PingExtra = PingExtraBox.Text,
                PingLabels = PingLabelsBox.Text,
                TlsStackTargets = TlsTargetsBox.Text
            });

            DataDirectory.EnsureExists();
            UserConfigLoader.Save(_configPath, config);

            var trayExe = Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, "netstrata-tray.exe");
            _startup.SetEnabled(StartupCheck.IsChecked == true, trayExe);

            StatusText.Text = "已保存 — 配置需重启 Daemon；开机自启已更新";
        }
        catch (Exception ex)
        {
            StatusText.Text = "保存失败: " + ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
