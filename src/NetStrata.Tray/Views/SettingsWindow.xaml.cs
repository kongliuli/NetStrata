using System.Windows;
using NetStrata.Core.Config;
using NetStrata.Core.Storage;

namespace NetStrata.Tray.Views;

public partial class SettingsWindow : Window
{
    private readonly string _configPath = DataDirectory.ConfigPath;

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
            StatusText.Text = "已保存 — 重启 Daemon 后生效";
        }
        catch (Exception ex)
        {
            StatusText.Text = "保存失败: " + ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
