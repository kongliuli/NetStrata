using System.IO;
using System.Windows;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using NetStrata.Core.Config;
using NetStrata.Core.Storage;
using NetStrata.Core.Tray;
using NetStrata.Core.Ui;
using NetStrata.Tray.Services;
using HcWindow = HandyControl.Controls.Window;

namespace NetStrata.Tray.Views;

public partial class SettingsWindow : HcWindow
{
    private readonly string _configPath = DataDirectory.ConfigPath;
    private readonly StartupShortcutService _startup = new(new WindowsStartupLinkWriter());
    private readonly Func<NetStrataOptions, Task<(bool Ok, string? Error)>>? _restartDaemon;

    public SettingsWindow(Func<NetStrataOptions, Task<(bool Ok, string? Error)>>? restartDaemon = null)
    {
        _restartDaemon = restartDaemon;
        InitializeComponent();
        ThemeApplier.Apply(this);
        LoadForm();
    }

    private void LoadForm()
    {
        var form = SettingsMapper.ToForm(UserConfigLoader.Load(_configPath));
        IntervalBox.Text = form.IntervalMs;
        TlsTargetsBox.Text = form.TlsStackTargets;
        SelectCombo(LangBox, form.Lang);
        SelectCombo(ThemeBox, form.Theme);
        StartupCheck.IsChecked = _startup.IsEnabled();
        StartMinimizedCheck.IsChecked = form.StartMinimized;
        StatusText.Text = _configPath;
        LocalizeChrome(form.Lang);
    }

    private void LocalizeChrome(string lang)
    {
        lang = LangResolver.Resolve(lang);
        Title = UiStrings.SettingsTitle(lang);
        HeaderText.Text = UiStrings.SettingsTitle(lang);
        LangLabel.Text = UiStrings.Language(lang);
        ThemeLabel.Text = UiStrings.Theme(lang);
        IntervalLabel.Text = UiStrings.T(lang, "探测间隔 (ms)", "Interval (ms)");
        TlsLabel.Text = UiStrings.T(lang, "TLS 栈目标", "TLS stack targets");
        StartupCheck.Content = UiStrings.T(lang, "登录时启动 NetStrata", "Start NetStrata at login");
        StartMinimizedCheck.Content = UiStrings.T(lang,
            "启动时最小化到托盘（不弹出主窗口）",
            "Start minimized to tray (no main window)");
        TargetsNote.Text = UiStrings.T(lang,
            "自定义目标请在「自定义目标」页维护。关闭主窗口将继续在托盘运行；退出请用托盘菜单。",
            "Manage custom targets on the Targets tab. Closing the main window keeps NetStrata in the tray; use the tray menu to quit.");
        CancelButton.Content = UiStrings.T(lang, "取消", "Cancel");
        SaveButton.Content = UiStrings.T(lang, "保存", "Save");
    }

    private static void SelectCombo(WpfComboBox box, string tag)
    {
        foreach (WpfComboBoxItem item in box.Items)
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
        box.SelectedIndex = 0;
    }

    private static string SelectedTag(WpfComboBox box, string fallback) =>
        (box.SelectedItem as WpfComboBoxItem)?.Tag?.ToString() ?? fallback;

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var existing = UserConfigLoader.Load(_configPath);
            var config = SettingsMapper.FromForm(new SettingsFormModel
            {
                IntervalMs = IntervalBox.Text,
                Port = "8787",
                // keep existing ping/https targets (edited on Targets tab)
                PingExtra = "",
                PingLabels = "",
                TlsStackTargets = TlsTargetsBox.Text,
                Lang = SelectedTag(LangBox, "zh"),
                Theme = SelectedTag(ThemeBox, "system"),
                StartMinimized = StartMinimizedCheck.IsChecked == true
            }, existing);

            DataDirectory.EnsureExists();
            UserConfigLoader.Save(_configPath, config);

            var trayExe = Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, "NetStrata.exe");
            _startup.SetEnabled(StartupCheck.IsChecked == true, trayExe);

            ThemeApplier.Apply(this);
            LocalizeChrome(config.Lang ?? "zh");

            if (_restartDaemon is not null)
            {
                var lang = config.Lang ?? "zh";
                StatusText.Text = UiStrings.T(lang, "已保存 — 正在热重载 Daemon…", "Saved — reloading daemon…");
                var (ok, err) = await _restartDaemon(NetStrataOptions.FromEnvironment());
                StatusText.Text = ok
                    ? UiStrings.T(lang, "已保存 — Daemon 已按新配置重启", "Saved — daemon restarted")
                    : UiStrings.T(lang, $"已保存 — Daemon 重启失败: {err}", $"Saved — daemon restart failed: {err}");
            }
            else
            {
                StatusText.Text = UiStrings.T(config.Lang ?? "zh", "已保存", "Saved");
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "保存失败: " + ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
