using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HandyControl.Controls;
using HandyControl.Data;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using NetStrata.Core.Cli;
using NetStrata.Core.Config;
using NetStrata.Core.Flow;
using NetStrata.Core.Models;
using NetStrata.Core.Probes;
using NetStrata.Core.Storage;
using NetStrata.Core.Tui;
using NetStrata.Core.Ui;
using NetStrata.Tray.Services;
using SkiaSharp;
using HcWindow = HandyControl.Controls.Window;

namespace NetStrata.Tray.Views;

public partial class MainWindow : HcWindow
{
    private readonly JsonSampleStorage _storage = new();
    private readonly JsonAlertStorage _alertStorage = new();
    private readonly DispatcherTimer _timer;
    private readonly IBrowserLauncher _browser;
    private readonly Action? _probeNow;
    private readonly Action? _openSettings;
    private readonly Func<NetStrataOptions, Task<(bool Ok, string? Error)>>? _restartDaemon;
    private bool _forceClose;
    private bool _probeBusy;
    private int _alertsSeenCount;
    private int _alertsUnread;

    public MainWindow(
        IBrowserLauncher? browser = null,
        Action? probeNow = null,
        Action? openSettings = null,
        Func<NetStrataOptions, Task<(bool Ok, string? Error)>>? restartDaemon = null)
    {
        _browser = browser ?? new ShellBrowserLauncher();
        _probeNow = probeNow;
        _openSettings = openSettings;
        _restartDaemon = restartDaemon;
        InitializeComponent();
        ThemeApplier.Apply(this);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) =>
        {
            UpdateAdaptiveLayout();
            await RefreshAsync();
            _timer.Start();
        };
        Closing += OnClosingToTray;
        Closed += (_, _) => _timer.Stop();
        NetworkFlow.OpenTrendRequested += OpenTrendForTarget;
        NetworkFlow.ProbeRequested += () => _probeNow?.Invoke();
    }

    private void OpenTrendForTarget(string targetTitle)
    {
        var lang = NetStrataOptions.FromEnvironment().Lang;
        NavTrend.IsSelected = true;
        ShowPage("trend");
        TrendFocusBanner.Visibility = Visibility.Visible;
        TrendFocusHint.Text = UiStrings.T(lang,
            $"关注目标：{targetTitle}（当前趋势为网关/国内/海外汇总；分目标 HTTPS 序列后续接入）",
            $"Focus: {targetTitle} (trend shows gateway/domestic/overseas aggregates; per-target HTTPS series TBD)");
        _ = RefreshTrendAsync(lang);
    }

    /// <summary>Allow the next Close / app Shutdown to actually tear down the window.</summary>
    public void AllowClose() => _forceClose = true;

    /// <summary>W11b: mirror tray probing state onto ProgressButton + disable re-entry.</summary>
    public void SetProbeBusy(bool busy)
    {
        _probeBusy = busy;
        var lang = NetStrataOptions.FromEnvironment().Lang;
        ProbeButton.IsEnabled = !busy;
        ProbeButton.IsChecked = busy;
        ProbeButton.Content = busy
            ? UiStrings.T(lang, "探测中…", "Probing…")
            : UiStrings.T(lang, "立即探测", "Probe now");
    }

    /// <summary>W11b: in-window Growl after manual probe (tray still shows BalloonTip).</summary>
    public void ShowProbeResult(bool ok, string message)
    {
        if (ok)
            Growl.Success(message);
        else
            Growl.Error(message);
    }

    private void OnClosingToTray(object? sender, CancelEventArgs e)
    {
        if (_forceClose)
            return;
        e.Cancel = true;
        Hide();
    }

    private int _density = 3; // 1=name+status, 2=+summary, 3=+latency
    private bool _sectionStateLoaded;
    private CartesianChart? _trendPingChart;
    private LineSeries<double?>[]? _trendPingSeries;
    private string? _trendDataFingerprint;

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateAdaptiveLayout();

    private void Section_ExpandedChanged(object sender, RoutedEventArgs e)
    {
        if (!_sectionStateLoaded || sender is not Expander ex || string.IsNullOrEmpty(ex.Name))
            return;
        SaveSectionExpanded(ex.Name, ex.IsExpanded);
    }

    /// <summary>
    /// Density by content width + wrap card sizes. Overview sections stack independently.
    /// </summary>
    private void UpdateAdaptiveLayout()
    {
        var w = ActualWidth;
        if (w <= 0)
            return;

        EnsureSectionStateLoaded();

        // L1 <640, L2 <900, L3 otherwise (window width)
        var density = w < 640 ? 1 : w < 900 ? 2 : 3;
        var densityChanged = density != _density;
        _density = density;

        // sidebar 208 + page margin 32
        var content = Math.Max(280, w - 240);
        var aiCard = density switch
        {
            1 => content >= 520 ? (content - 20) / 2 : content - 10,
            2 => content >= 700 ? (content - 20) / 2 : content - 10,
            _ => content >= 900 ? (content - 30) / 3 : content >= 620 ? (content - 20) / 2 : content - 10
        };
        aiCard = Math.Clamp(aiCard, density == 1 ? 160 : 180, 420);

        var targetCard = content >= 720 ? (content - 30) / 3
            : content >= 480 ? (content - 20) / 2
            : content - 10;
        targetCard = Math.Clamp(targetCard, 160, 360);

        AiList.Tag = aiCard;
        TargetsList.Tag = targetCard;

        if (densityChanged)
            ApplyDensityToCards();
    }

    private void NavMenu_SelectionChanged(object sender, FunctionEventArgs<object> e)
    {
        var tag = e.Info switch
        {
            SideMenuItem item => item.Tag?.ToString(),
            _ => e.Info?.ToString()
        };
        ShowPage(tag ?? "overview");
    }

    private void ShowPage(string tag)
    {
        PageOverview.Visibility = tag == "overview" ? Visibility.Visible : Visibility.Collapsed;
        PageChain.Visibility = tag == "chain" ? Visibility.Visible : Visibility.Collapsed;
        PageAi.Visibility = tag == "ai" ? Visibility.Visible : Visibility.Collapsed;
        PageTargets.Visibility = tag == "targets" ? Visibility.Visible : Visibility.Collapsed;
        PageTrend.Visibility = tag == "trend" ? Visibility.Visible : Visibility.Collapsed;
        PageAlerts.Visibility = tag == "alerts" ? Visibility.Visible : Visibility.Collapsed;
        PageLocal.Visibility = tag == "local" ? Visibility.Visible : Visibility.Collapsed;
        if (tag == "trend")
            _ = RefreshTrendAsync();
        if (tag == "alerts")
        {
            ClearAlertsBadge();
            _ = RefreshAlertsAsync();
        }
    }

    private void AlertsPanel_Click(object sender, MouseButtonEventArgs e)
    {
        NavAlerts.IsSelected = true;
        ShowPage("alerts");
    }

    private void GotoAi_Click(object sender, MouseButtonEventArgs e)
    {
        NavAi.IsSelected = true;
        ShowPage("ai");
        e.Handled = true;
    }

    private void GotoTargets_Click(object sender, MouseButtonEventArgs e)
    {
        NavTargets.IsSelected = true;
        ShowPage("targets");
        e.Handled = true;
    }

    private void GotoLocal_Click(object sender, MouseButtonEventArgs e)
    {
        NavLocal.IsSelected = true;
        ShowPage("local");
        e.Handled = true;
    }

    private void ApplyDensityToCards()
    {
        foreach (var list in new[] { AiList, TargetsList, LayersList })
        {
            if (list.ItemsSource is not System.Collections.IEnumerable items)
                continue;
            foreach (var item in items)
            {
                if (item is CardVm card)
                    card.SetDensity(_density);
            }
        }
    }

    private void EnsureSectionStateLoaded()
    {
        if (_sectionStateLoaded)
            return;
        LayersExpander.IsExpanded = LoadSectionExpanded(nameof(LayersExpander), true);
        AiExpander.IsExpanded = LoadSectionExpanded(nameof(AiExpander), true);
        _sectionStateLoaded = true;
    }

    private void UpdateAlertsBadge(int totalAlerts)
    {
        if (totalAlerts > _alertsSeenCount)
            _alertsUnread += totalAlerts - _alertsSeenCount;
        _alertsSeenCount = totalAlerts;
        if (_alertsUnread <= 0 || PageAlerts.Visibility == Visibility.Visible)
        {
            ClearAlertsBadge();
            return;
        }

        AlertsBadge.Value = _alertsUnread;
        AlertsBadge.Visibility = Visibility.Visible;
    }

    private void ClearAlertsBadge()
    {
        _alertsUnread = 0;
        AlertsBadge.Value = 0;
        AlertsBadge.Visibility = Visibility.Collapsed;
    }

    private static bool LoadSectionExpanded(string key, bool fallback)
    {
        try
        {
            return Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\NetStrata\Ui", key, fallback is true ? 1 : 0) is int i
                ? i != 0
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static void SaveSectionExpanded(string key, bool expanded)
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\NetStrata\Ui");
            k?.SetValue(key, expanded ? 1 : 0);
        }
        catch
        {
            // ignore
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void Probe_Click(object sender, RoutedEventArgs e) => _probeNow?.Invoke();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_openSettings is not null)
            _openSettings();
        else
        {
            // fallback when constructed without tray host
            var win = new SettingsWindow(_restartDaemon) { Owner = this };
            win.Show();
        }
    }

    public async Task RefreshAsync()
    {
        try
        {
            ThemeApplier.Apply(this);
            var options = NetStrataOptions.FromEnvironment();
            var lang = options.Lang;
            var state = await _storage.ReadStateAsync(CancellationToken.None);

            var overview = DashboardMapper.FromState(state, lang);
            var local = LocalNetMapper.FromState(state, lang);
            ApplyOverview(overview, local, lang);

            var chain = ChainMapper.FromState(state, lang);
            ApplyChain(chain);
            NetworkFlow.SetBlocks(MultiTargetFlowBuilder.FromState(state, lang), lang);

            ApplyAi(overview, lang);
            ApplyLocal(local);
            ApplyTargets(overview, lang);
            if (PageTrend.Visibility == Visibility.Visible)
                await RefreshTrendAsync(lang);
            if (PageAlerts.Visibility == Visibility.Visible)
                await RefreshAlertsAsync(lang, state);
        }
        catch (Exception ex)
        {
            OverviewHeadline.Text = "Error: " + ex.Message;
        }
    }

    private TimeSpan TrendWindowSpan() =>
        Trend6h.IsChecked == true ? TimeSpan.FromHours(6)
        : Trend24h.IsChecked == true ? TimeSpan.FromHours(24)
        : TimeSpan.FromHours(1);

    private void TrendWindow_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;
        _ = RefreshTrendAsync();
    }

    private async Task RefreshTrendAsync(string? lang = null)
    {
        lang ??= NetStrataOptions.FromEnvironment().Lang;
        TrendTitle.Text = UiStrings.SectionTrend(lang);
        TrendPingTitle.Text = UiStrings.T(lang, "Ping 延迟 (ms)", "Ping latency (ms)");
        TrendLayerHint.Text = UiStrings.T(lang, "分层状态时间线", "Layer status timeline");
        TrendLegendOk.Text = UiStrings.StateName(lang, "ok");
        TrendLegendDegraded.Text = UiStrings.StateName(lang, "degraded");
        TrendLegendFail.Text = UiStrings.StateName(lang, "fail");
        TrendLegendSkip.Text = UiStrings.T(lang, "跳过/未知", "Skipped / unknown");

        var window = TrendWindowSpan();
        var limit = TrendWindow.SuggestedTailLimit(window);
        var samples = await _storage.ReadTailAsync(limit, CancellationToken.None);
        var filtered = TrendWindow.Filter(samples, window);
        if (filtered.Count == 0)
        {
            TrendEmpty.Visibility = Visibility.Visible;
            TrendEmpty.Text = UiStrings.T(lang, "尚无足够样本。启动 Daemon 或点「立即探测」后稍等。",
                "Not enough samples yet. Start the daemon or probe once.");
            ClearTrendVisuals();
            return;
        }

        TrendEmpty.Visibility = Visibility.Collapsed;
        var chart = TrendChartBuilder.Build(filtered);
        // ponytail: skip redraw when sample set unchanged (5s timer / re-enter page)
        var fp = $"{filtered.Count}|{filtered[^1].T}|{window.TotalHours}|{lang}|{IsDarkTheme()}";
        if (fp == _trendDataFingerprint && _trendPingChart is not null)
            return;
        _trendDataFingerprint = fp;

        var dark = IsDarkTheme();
        ApplyPingChart(chart, dark, lang);
        ApplyLayerStrips(chart, lang);
    }

    private void ClearTrendVisuals()
    {
        _trendDataFingerprint = null;
        _trendPingChart = null;
        _trendPingSeries = null;
        TrendPingHost.Content = null;
        TrendLayerStrips.Children.Clear();
    }

    private List<AlertRowVm> _alertRows = [];
    private string _alertsLang = "zh";

    private async Task RefreshAlertsAsync(string? lang = null, DaemonState? state = null)
    {
        lang ??= NetStrataOptions.FromEnvironment().Lang;
        _alertsLang = lang;
        AlertsTitle.Text = UiStrings.T(lang, "通知告警", "Alerts");
        AlertsPageHint.Text = UiStrings.T(lang,
            "这里用白话记录网络变化（代理出口、路由器、网卡等）。点击条目可展开详情。",
            "Plain-language history of network changes (proxy exit, gateway, adapter). Expand a row for details.");
        AlertFilterAll.Content = UiStrings.T(lang, "全部", "All");
        AlertFilterFail.Content = UiStrings.T(lang, "重要", "Important");
        AlertFilterWarn.Content = UiStrings.T(lang, "提醒", "Notice");
        AlertFilterInfo.Content = UiStrings.T(lang, "提示", "Info");
        AlertsEmptyProbeButton.Content = UiStrings.T(lang, "立即探测", "Probe now");

        state ??= await _storage.ReadStateAsync(CancellationToken.None);
        var persisted = await _alertStorage.ReadTailAsync(100, CancellationToken.None);
        var merged = MergeAlerts(persisted, state?.RecentAlerts ?? []);
        _alertRows = merged
            .AsEnumerable()
            .Reverse()
            .Select(a =>
            {
                var view = AlertPresenter.Format(a, lang);
                var (accent, badge, soft) = AlertSeverityBrushes(view.Severity, lang);
                return new AlertRowVm
                {
                    Title = view.Title,
                    Detail = view.Detail,
                    When = view.WhenLocal,
                    Badge = badge,
                    Severity = view.Severity,
                    AccentBrush = accent,
                    BadgeBg = soft
                };
            }).ToList();

        ApplyAlertFilter();
    }

    private void AlertFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;
        ApplyAlertFilter();
    }

    private void ApplyAlertFilter()
    {
        var lang = _alertsLang;
        var filter = AlertFilterFail.IsChecked == true ? "fail"
            : AlertFilterWarn.IsChecked == true ? "warn"
            : AlertFilterInfo.IsChecked == true ? "info"
            : null;

        var shown = filter is null
            ? _alertRows
            : _alertRows.Where(r => r.Severity == filter).ToList();

        if (_alertRows.Count == 0)
        {
            AlertsEmptyPanel.Visibility = Visibility.Visible;
            AlertsEmptyProbeButton.Visibility = Visibility.Visible;
            AlertsEmpty.Text = UiStrings.T(lang,
                "暂无通知。网络出口或路由器变更时会自动记录在此。可先点「立即探测」刷新状态。",
                "No alerts yet. Changes to proxy exit or gateway will appear here. Tap Probe now to refresh.");
            AlertsList.ItemsSource = null;
            return;
        }

        if (shown.Count == 0)
        {
            AlertsEmptyPanel.Visibility = Visibility.Visible;
            AlertsEmptyProbeButton.Visibility = Visibility.Collapsed;
            AlertsEmpty.Text = UiStrings.T(lang,
                "当前筛选条件下没有通知，可切换到「全部」查看。",
                "No alerts match this filter. Switch to All to see everything.");
            AlertsList.ItemsSource = null;
            return;
        }

        AlertsEmptyPanel.Visibility = Visibility.Collapsed;
        AlertsList.ItemsSource = shown;
    }

    private static IReadOnlyList<Alert> MergeAlerts(IReadOnlyList<Alert> persisted, IReadOnlyList<Alert> recent)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<Alert>();
        foreach (var a in persisted.Concat(recent))
        {
            var key = AlertNotifier.Key(a);
            if (!seen.Add(key))
                continue;
            list.Add(a);
        }

        return list;
    }

    private static (System.Windows.Media.Brush Accent, string Badge, System.Windows.Media.Brush Soft)
        AlertSeverityBrushes(string severity, string lang)
    {
        var kind = StatusTokens.FromAlertSeverity(severity);
        var badge = kind switch
        {
            StatusKind.Fail => UiStrings.T(lang, "重要", "Important"),
            StatusKind.Degraded => UiStrings.T(lang, "提醒", "Notice"),
            _ => UiStrings.T(lang, "提示", "Info")
        };
        return (StatusBrushes.Accent(kind), badge, StatusBrushes.Soft(kind));
    }

    private void ApplyPingChart(TrendChartModel chart, bool dark, string lang)
    {
        var labels = chart.Labels
            .Select(t => DateTime.TryParse(t, out var dt) ? dt.ToLocalTime().ToString("HH:mm") : t)
            .ToArray();
        var paint = new SolidColorPaint(dark ? SKColors.LightGray : SKColors.DimGray);
        var series = new[]
        {
            MakeLine(UiStrings.T(lang, "网关", "Gateway"), chart.GatewayMs,
                dark ? SKColors.SkyBlue : SKColors.DodgerBlue),
            MakeLine(UiStrings.T(lang, "国内", "Domestic"), chart.DomesticMs,
                dark ? SKColors.LightGreen : SKColors.ForestGreen),
            MakeLine(UiStrings.T(lang, "海外", "Overseas"), chart.OverseasMs,
                dark ? SKColors.Orange : SKColors.DarkOrange)
        };

        if (_trendPingChart is null || _trendPingSeries is null)
        {
            _trendPingSeries = series;
            _trendPingChart = new CartesianChart
            {
                LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom,
                TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top,
                Series = series,
                XAxes =
                [
                    new Axis
                    {
                        Labels = labels,
                        LabelsRotation = 15,
                        TextSize = 11,
                        LabelsPaint = paint
                    }
                ],
                YAxes =
                [
                    new Axis
                    {
                        Name = "ms",
                        TextSize = 11,
                        LabelsPaint = paint
                    }
                ]
            };
            TrendPingHost.Content = _trendPingChart;
            return;
        }

        // in-place update — avoid tearing down the chart every refresh
        for (var i = 0; i < series.Length; i++)
        {
            _trendPingSeries[i].Name = series[i].Name;
            _trendPingSeries[i].Values = series[i].Values;
            _trendPingSeries[i].Stroke = series[i].Stroke;
        }

        if (_trendPingChart.XAxes.FirstOrDefault() is Axis x)
        {
            x.Labels = labels;
            x.LabelsPaint = paint;
        }

        if (_trendPingChart.YAxes.FirstOrDefault() is Axis y)
            y.LabelsPaint = paint;
    }

    private void ApplyLayerStrips(TrendChartModel chart, string lang)
    {
        TrendLayerStrips.Children.Clear();
        var order = new[] { "wifi", "lan", "broadband", "overseas_direct", "proxy" };
        foreach (var name in order)
        {
            if (!chart.LayerStates.TryGetValue(name, out var states) || states.Count == 0)
                continue;

            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var label = new TextBlock
            {
                Text = UiStrings.LayerName(lang, name),
                Width = 72,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12
            };
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);

            var strip = new Grid { Height = 18 };
            var n = states.Count;
            for (var i = 0; i < n; i++)
                strip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (var i = 0; i < n; i++)
            {
                var cell = new Border
                {
                    Background = StateBrush(states[i]),
                    Margin = new Thickness(i == 0 ? 0 : 0.5, 0, 0, 0),
                    CornerRadius = new CornerRadius(1),
                    ToolTip = $"{UiStrings.LayerName(lang, name)} · {StateLabel(lang, states[i])}"
                };
                Grid.SetColumn(cell, i);
                strip.Children.Add(cell);
            }

            row.Children.Add(strip);
            TrendLayerStrips.Children.Add(row);
        }
    }

    private static LineSeries<double?> MakeLine(string name, IReadOnlyList<double?> values, SKColor color) =>
        new()
        {
            Name = name,
            Values = values.ToArray(),
            GeometrySize = 0,
            Fill = null,
            Stroke = new SolidColorPaint(color, 2),
            LineSmoothness = 0.2
        };

    private static System.Windows.Media.Brush StateBrush(string? state) =>
        StatusBrushes.Border(StatusTokens.FromState(state));

    private static string StateLabel(string lang, string? state) => state switch
    {
        "ok" or "degraded" or "fail" => UiStrings.StateName(lang, state),
        "skipped" => UiStrings.T(lang, "跳过", "Skipped"),
        "unknown" => UiStrings.T(lang, "未知", "Unknown"),
        _ => UiStrings.T(lang, "无数据", "n/a")
    };

    private void ApplyOverview(DashboardViewModel vm, LocalNetViewModel local, string lang)
    {
        Title = "NetStrata";
        OverviewOverallBadge.Text = vm.Overall;
        OverviewTitle.Text = vm.Headline;
        OverviewHeadline.Text = vm.Meta;
        OverviewAiHeadline.Text = vm.AiHeadline;
        OverviewMeta.Text = vm.ProxySummary;
        ApplyOverallBadge(vm.Overall);
        HeaderSubtitle.Text = UiStrings.T(lang, "分层网络健康", "Layered network health");
        NavOverview.Header = UiStrings.T(lang, "总览", "Overview");
        NavChain.Header = UiStrings.T(lang, "探测链路", "Probe chain");
        NavAi.Header = UiStrings.T(lang, "AI / API", "AI / API");
        NavTargets.Header = UiStrings.T(lang, "自定义目标", "Targets");
        NavTrend.Header = UiStrings.SectionTrend(lang);
        NavAlertsText.Text = UiStrings.T(lang, "通知告警", "Alerts");
        NavLocal.Header = UiStrings.SectionLocal(lang);
        RefreshButton.Content = vm.RefreshLabel;
        if (!_probeBusy)
            ProbeButton.Content = UiStrings.T(lang, "立即探测", "Probe now");
        SettingsButton.Content = UiStrings.SettingsTitle(lang);
        LayersTitle.Text = vm.LayersTitle;
        OverviewAiTitle.Text = vm.AiTitle;
        OverviewAiMore.Text = UiStrings.T(lang, "详情 →", "Details →");
        OverviewTargetsMore.Text = UiStrings.T(lang, "详情 →", "Details →");
        OverviewLocalMore.Text = UiStrings.T(lang, "详情 →", "Details →");
        OverviewLocalTitle.Text = local.Title;
        AlertsPanelTitle.Text = UiStrings.T(lang, "近期通知", "Recent alerts");
        AlertsMoreHint.Text = UiStrings.T(lang, "查看全部 →", "See all →");

        UpdateAlertsBadge(vm.RecentAlertViews.Count);

        if (vm.RecentAlertViews.Count == 0)
        {
            AlertsPanel.Visibility = Visibility.Collapsed;
            OverviewAlertsList.ItemsSource = null;
        }
        else
        {
            AlertsPanel.Visibility = Visibility.Visible;
            // newest first for scanning; preserve expand state by title+when key
            var prevExpanded = new HashSet<string>(StringComparer.Ordinal);
            if (OverviewAlertsList.ItemsSource is IEnumerable<object> oldItems)
            {
                foreach (var o in oldItems)
                {
                    if (o is OverviewAlertVm a && a.IsExpanded)
                        prevExpanded.Add(a.Key);
                }
            }

            OverviewAlertsList.ItemsSource = vm.RecentAlertViews
                .Reverse()
                .Select(v => new OverviewAlertVm
                {
                    Title = v.Title,
                    Detail = v.Detail,
                    When = v.WhenLocal,
                    IsExpanded = prevExpanded.Contains($"{v.Title}|{v.WhenLocal}")
                }).ToList();
        }

        LayersList.ItemsSource = vm.Layers.Select(l => new CardVm
        {
            Title = l.DisplayName,
            Subtitle = l.StateLabel,
            Detail = UiStrings.T(lang, "点击查看探测链路", "Click to open probe chain"),
            AccentBrush = StatusBrushes.FromBorderHex(l.BorderColor),
            Badge = l.StateLabel,
            BadgeBg = StatusBrushes.FromBorderHex(l.BorderColor, soft: true)
        }.WithDensity(_density)).ToList();

        var aiOk = vm.AiApis.Count(a =>
            a.Detail.Contains("均可", StringComparison.Ordinal)
            || a.Detail.Contains("direct + proxy", StringComparison.OrdinalIgnoreCase));
        OverviewAiSummary.Text = vm.AiApis.Count == 0
            ? UiStrings.T(lang, "尚无 AI API 探测数据", "No AI API probe data yet")
            : UiStrings.T(lang,
                $"{vm.AiApis.Count} 个服务 · {aiOk} 个双路径可达 · {vm.AiHeadline}",
                $"{vm.AiApis.Count} services · {aiOk} dual-path OK · {vm.AiHeadline}");

        OverviewTargetsTitle.Text = vm.PingTitle;
        OverviewTargetsExpanderHost.Visibility = Visibility.Visible;
        OverviewTargetsSummary.Text = vm.HasCustomPings
            ? UiStrings.T(lang,
                $"{vm.CustomPings.Count} 个自定义目标（Ping / HTTPS）",
                $"{vm.CustomPings.Count} custom targets (ping / HTTPS)")
            : UiStrings.T(lang, "尚未添加自定义目标", "No custom targets yet");

        var localBits = local.Rows.Take(3).Select(r => $"{r.Label} {r.Value}");
        OverviewLocalSummary.Text = local.Rows.Count == 0
            ? UiStrings.T(lang, "暂无本机网络信息", "No local network info yet")
            : string.Join(" · ", localBits);
    }

    private void ApplyChain(ChainViewModel vm)
    {
        var lang = NetStrataOptions.FromEnvironment().Lang;
        ChainOverall.Text = $"{vm.Overall}  ·  {vm.Headline}";
        ChainAi.Text = vm.AiHeadline;
        ChainMeta.Text = vm.Meta;
        TrunkHint.Text = UiStrings.T(lang, "公共路径", "Shared path");
        // W9f: only shared trunk + egress hubs — target detail lives in NetworkFlow blocks
        var trunkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "wifi", "lan", "broadband", "overseas_direct", "proxy" };
        TrunkStrip.ItemsSource = vm.Rows
            .Where(r => trunkKeys.Contains(r.LayerKey))
            .Select(r =>
            {
                var reasons = string.Join(" · ", r.Reasons.Where(x => !string.IsNullOrWhiteSpace(x)));
                var tip = string.Join("\n", new[] { reasons, r.MetricsSummary }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
                return new ChainRowVm
                {
                    Title = r.DisplayName,
                    Badge = r.StateLabel,
                    Reasons = reasons,
                    Metrics = r.MetricsSummary ?? "",
                    Tip = string.IsNullOrWhiteSpace(tip)
                        ? r.StateLabel
                        : tip,
                    AccentBrush = StatusBrushes.FromBorderHex(r.BorderColor),
                    BadgeBg = StatusBrushes.FromBorderHex(r.BorderColor, soft: true)
                };
            }).ToList();
    }

    private void LayerCard_Click(object sender, MouseButtonEventArgs e)
    {
        NavChain.IsSelected = true;
        ShowPage("chain");
        e.Handled = true;
    }

    private void ApplyAi(DashboardViewModel vm, string lang)
    {
        AiTitle.Text = vm.AiTitle;
        AiHint.Text = UiStrings.OpenSiteHint(lang);
        AiList.ItemsSource = BuildAiCards(vm, lang);
    }

    private List<CardVm> BuildAiCards(DashboardViewModel vm, string lang) =>
        vm.AiApis.Select(a => new CardVm
        {
            Title = a.Name,
            // L1 badge = short status; L2 detail = path summary; L3 = latency rows
            Badge = ShortStateFromDetail(lang, a.Detail, a.DirectState, a.ProxyState),
            Detail = a.Detail,
            BadgeBg = StatusBrushes.FromBorderHex(a.BorderColor, soft: true),
            AccentBrush = StatusBrushes.FromBorderHex(a.BorderColor),
            DirectLabel = UiStrings.Direct(lang),
            ProxyLabel = UiStrings.ViaProxy(lang),
            DirectValue = $"{a.DirectState} · {a.DirectMs}",
            ProxyValue = $"{a.ProxyState} · {a.ProxyMs}",
            Url = a.OpenUrl
        }.WithDensity(_density)).ToList();

    private static string ShortState(string lang, string state) =>
        state switch
        {
            "ok" or "fail" or "degraded" => UiStrings.StateName(lang, state),
            "skipped" => UiStrings.T(lang, "跳过", "Skipped"),
            _ => state
        };

    /// <summary>W11d: prefer structured direct/proxy states; avoid Chinese substring matching.</summary>
    private static string ShortStateFromDetail(string lang, string detail, string direct, string proxy)
    {
        var d = NormalizeProbeState(direct);
        var p = NormalizeProbeState(proxy);
        if (d == "ok" && p == "ok")
            return UiStrings.StateName(lang, "ok");
        if (d == "ok" || p == "ok")
            return UiStrings.StateName(lang, "degraded");
        if (detail.Contains("direct + proxy", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("均可", StringComparison.Ordinal))
            return UiStrings.StateName(lang, "ok");
        if (detail.Contains("only", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("仅", StringComparison.Ordinal))
            return UiStrings.StateName(lang, "degraded");
        return UiStrings.StateName(lang, "fail");
    }

    private static string NormalizeProbeState(string raw)
    {
        var s = (raw ?? "").Trim();
        if (s.Equals("ok", StringComparison.OrdinalIgnoreCase)
            || s.Equals("OK", StringComparison.Ordinal)
            || s.Contains("正常", StringComparison.Ordinal))
            return "ok";
        if (s.Contains("降级", StringComparison.Ordinal)
            || s.Equals("degraded", StringComparison.OrdinalIgnoreCase))
            return "degraded";
        if (s.Contains("跳过", StringComparison.Ordinal)
            || s.Equals("skipped", StringComparison.OrdinalIgnoreCase))
            return "skipped";
        if (string.IsNullOrEmpty(s) || s is "—" or "-")
            return "skipped";
        return "fail";
    }

    private void ApplyLocal(LocalNetViewModel vm)
    {
        LocalTitle.Text = vm.Title;
        LocalList.ItemsSource = vm.Rows.Select(r => new KvVm { Label = r.Label, Value = r.Value }).ToList();
    }

    private void ApplyTargets(DashboardViewModel vm, string lang)
    {
        TargetsTitle.Text = vm.PingTitle;
        TargetsHint.Text = UiStrings.T(lang,
            "在下方添加主机或 URL（写入配置并热重载）。HTTPS 目标可点开浏览器验证。",
            "Add host or URL below (saved to config + daemon reload). HTTPS targets open in browser.");
        TargetsList.ItemsSource = vm.CustomPings.Select(p => new CardVm
        {
            Title = p.Label,
            Badge = ShortState(lang, p.State),
            Subtitle = p.Detail,
            AccentBrush = StatusBrushes.FromBorderHex(p.BorderColor),
            BadgeBg = StatusBrushes.FromBorderHex(p.BorderColor, soft: true),
            Url = p.Url ?? (p.Kind == "https" ? p.Target : null),
            TargetKey = p.Kind == "https" ? $"https:{p.Target}" : $"ping:{p.Target}"
        }.WithDensity(_density)).ToList();
        TargetsEmptyPanel.Visibility = vm.HasCustomPings ? Visibility.Collapsed : Visibility.Visible;
        TargetsEmpty.Text = UiStrings.T(lang, "尚无自定义目标。添加 Ping 主机或 https:// URL。", "No custom targets yet. Add a ping host or https:// URL.");
    }

    private void ApplyOverallBadge(string overall)
    {
        OverallBadge.Background = StatusBrushes.ForOverall(overall, out var fg, out var border);
        OverallBadge.BorderBrush = border;
        OverviewOverallBadge.Foreground = fg;
    }

    private static bool IsDarkTheme()
    {
        var p = ThemeApplier.CurrentPalette();
        return string.Equals(p.WindowBg, ThemeResolver.Dark.WindowBg, StringComparison.OrdinalIgnoreCase);
    }

    private void Card_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement el)
            el.Opacity = 0.92;
    }

    private void Card_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement el)
            el.Opacity = 1;
    }

    private async void AddTarget_Click(object sender, RoutedEventArgs e)
    {
        var raw = TargetInput.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var config = UserConfigLoader.Load(DataDirectory.ConfigPath);
        var pings = config.PingExtra.ToList();
        var labels = config.PingExtraLabels.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var https = config.HttpsExtra.ToList();

        if (ShellBrowserLauncher.TryNormalizeUrl(raw, out var url))
        {
            if (!https.Contains(url, StringComparer.OrdinalIgnoreCase))
                https.Add(url);
        }
        else if (!pings.Contains(raw, StringComparer.OrdinalIgnoreCase))
        {
            pings.Add(raw);
        }

        if (pings.Count > 10)
            pings = pings.Take(10).ToList();
        if (https.Count > 20)
            https = https.Take(20).ToList();

        SaveTargets(config, pings, labels, https);
        TargetInput.Text = "";

        if (_restartDaemon is not null)
            await _restartDaemon(NetStrataOptions.FromEnvironment());
        await RefreshAsync();
    }

    private async void RemoveTarget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CardVm { TargetKey: { } key } })
            return;

        var config = UserConfigLoader.Load(DataDirectory.ConfigPath);
        var pings = config.PingExtra.ToList();
        var labels = config.PingExtraLabels.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var https = config.HttpsExtra.ToList();

        if (key.StartsWith("https:", StringComparison.Ordinal))
        {
            var url = key["https:".Length..];
            https.RemoveAll(u => string.Equals(u, url, StringComparison.OrdinalIgnoreCase));
        }
        else if (key.StartsWith("ping:", StringComparison.Ordinal))
        {
            var host = key["ping:".Length..];
            pings.RemoveAll(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase));
            labels.Remove(host);
        }

        SaveTargets(config, pings, labels, https);
        if (_restartDaemon is not null)
            await _restartDaemon(NetStrataOptions.FromEnvironment());
        await RefreshAsync();
    }

    private static void SaveTargets(
        UserConfig config,
        List<string> pings,
        Dictionary<string, string> labels,
        List<string> https)
    {
        UserConfigLoader.Save(DataDirectory.ConfigPath, new UserConfig
        {
            IntervalMs = config.IntervalMs,
            Port = config.Port,
            PingExtra = pings,
            PingExtraLabels = labels,
            TlsStackTargets = config.TlsStackTargets,
            HttpsExtra = https,
            Lang = config.Lang,
            Theme = config.Theme,
            StartMinimized = config.StartMinimized,
            Judge = config.Judge
        });
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CardVm { Url: { } url } })
            TryOpen(url);
    }

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string url })
            TryOpen(url);
        else if (sender is MenuItem { DataContext: CardVm { Url: { } u } })
            TryOpen(u);
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CardVm { Url: { } url } })
            System.Windows.Clipboard.SetText(url);
    }

    private void TryOpen(string url)
    {
        try
        {
            _browser.Open(url);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "NetStrata");
        }
    }

    private static bool LooksLikeUrl(string value) =>
        value.Contains("://", StringComparison.Ordinal) || value.Contains('.', StringComparison.Ordinal);

    private sealed class CardVm : INotifyPropertyChanged
    {
        private int _density = 3;

        public required string Title { get; init; }
        public string Subtitle { get; init; } = "";
        public string Detail { get; init; } = "";
        public string Badge { get; init; } = "";
        public System.Windows.Media.Brush AccentBrush { get; init; } = System.Windows.Media.Brushes.Gray;
        public System.Windows.Media.Brush BadgeBg { get; init; } = System.Windows.Media.Brushes.Transparent;
        public string DirectLabel { get; init; } = "";
        public string ProxyLabel { get; init; } = "";
        public string DirectValue { get; init; } = "";
        public string ProxyValue { get; init; } = "";
        public string? Url { get; init; }
        public string? TargetKey { get; init; }
        public Visibility OpenVisible => string.IsNullOrEmpty(Url) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility RemoveVisible => string.IsNullOrEmpty(TargetKey) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility Level2Visible => _density >= 2 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility Level3Visible => _density >= 3 ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;

        public CardVm WithDensity(int density)
        {
            SetDensity(density);
            return this;
        }

        public void SetDensity(int density)
        {
            if (_density == density)
                return;
            _density = density;
            OnPropertyChanged(nameof(Level2Visible));
            OnPropertyChanged(nameof(Level3Visible));
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private sealed class ChainRowVm
    {
        public required string Title { get; init; }
        public required string Badge { get; init; }
        public required string Reasons { get; init; }
        public required string Metrics { get; init; }
        public string Tip { get; init; } = "";
        public Visibility ReasonsVisibility =>
            string.IsNullOrWhiteSpace(Reasons) ? Visibility.Collapsed : Visibility.Visible;
        public required System.Windows.Media.Brush AccentBrush { get; init; }
        public required System.Windows.Media.Brush BadgeBg { get; init; }
    }

    private sealed class KvVm
    {
        public required string Label { get; init; }
        public required string Value { get; init; }
    }

    private sealed class AlertRowVm
    {
        public required string Title { get; init; }
        public required string Detail { get; init; }
        public required string When { get; init; }
        public required string Badge { get; init; }
        public required string Severity { get; init; }
        public required System.Windows.Media.Brush AccentBrush { get; init; }
        public required System.Windows.Media.Brush BadgeBg { get; init; }
    }

    private sealed class OverviewAlertVm : INotifyPropertyChanged
    {
        private bool _isExpanded;
        public required string Title { get; init; }
        public required string Detail { get; init; }
        public required string When { get; init; }
        public string Key => $"{Title}|{When}";
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
