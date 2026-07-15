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
    private readonly DispatcherTimer _timer;
    private readonly IBrowserLauncher _browser;
    private readonly Action? _probeNow;
    private readonly Func<NetStrataOptions, Task<(bool Ok, string? Error)>>? _restartDaemon;

    public MainWindow(
        IBrowserLauncher? browser = null,
        Action? probeNow = null,
        Func<NetStrataOptions, Task<(bool Ok, string? Error)>>? restartDaemon = null)
    {
        _browser = browser ?? new ShellBrowserLauncher();
        _probeNow = probeNow;
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
        Closed += (_, _) => _timer.Stop();
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

        OverviewAiList.Tag = aiCard;
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
        PageLocal.Visibility = tag == "local" ? Visibility.Visible : Visibility.Collapsed;
        if (tag == "trend")
            _ = RefreshTrendAsync();
    }

    private void ApplyDensityToCards()
    {
        foreach (var list in new[] { OverviewAiList, AiList, OverviewTargetsList, TargetsList, LayersList })
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
        OverviewAiExpander.IsExpanded = LoadSectionExpanded(nameof(OverviewAiExpander), true);
        OverviewTargetsExpander.IsExpanded = LoadSectionExpanded(nameof(OverviewTargetsExpander), true);
        OverviewLocalExpander.IsExpanded = LoadSectionExpanded(nameof(OverviewLocalExpander), true);
        AiExpander.IsExpanded = LoadSectionExpanded(nameof(AiExpander), true);
        _sectionStateLoaded = true;
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
        var win = new SettingsWindow(_restartDaemon);
        win.Owner = this;
        win.ShowDialog();
        _ = RefreshAsync();
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
            NetworkFlow.SetTraces(FlowTraceBuilder.FromState(state, lang));

            ApplyAi(overview, lang);
            ApplyLocal(local);
            ApplyTargets(overview, lang);
            if (PageTrend.Visibility == Visibility.Visible)
                await RefreshTrendAsync(lang);
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

    private static System.Windows.Media.Brush StateBrush(string? state) => state switch
    {
        "ok" => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x34, 0xA8, 0x53)),
        "degraded" => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFB, 0xBC, 0x04)),
        "fail" => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEA, 0x43, 0x35)),
        _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9A, 0xA0, 0xA6))
    };

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
        OverviewProxy.Text = "";
        ApplyOverallBadge(vm.Overall);
        HeaderSubtitle.Text = UiStrings.T(lang, "分层网络健康", "Layered network health");
        NavOverview.Header = UiStrings.T(lang, "总览", "Overview");
        NavChain.Header = UiStrings.T(lang, "探测链路", "Probe chain");
        NavAi.Header = UiStrings.T(lang, "AI / API", "AI / API");
        NavTargets.Header = UiStrings.T(lang, "自定义目标", "Targets");
        NavTrend.Header = UiStrings.SectionTrend(lang);
        NavLocal.Header = UiStrings.SectionLocal(lang);
        RefreshButton.Content = vm.RefreshLabel;
        ProbeButton.Content = UiStrings.T(lang, "立即探测", "Probe now");
        SettingsButton.Content = UiStrings.SettingsTitle(lang);
        LayersTitle.Text = vm.LayersTitle;
        OverviewAiTitle.Text = vm.AiTitle;
        OverviewAiHint.Text = UiStrings.OpenSiteHint(lang);
        OverviewLocalTitle.Text = local.Title;

        if (string.IsNullOrWhiteSpace(vm.AlertsSummary))
            AlertsPanel.Visibility = Visibility.Collapsed;
        else
        {
            AlertsPanel.Visibility = Visibility.Visible;
            AlertsText.Text = vm.AlertsSummary;
        }

        LayersList.ItemsSource = vm.Layers.Select(l => new CardVm
        {
            Title = l.DisplayName,
            Subtitle = l.StateLabel,
            AccentBrush = Brush(l.BorderColor),
            Badge = l.StateLabel,
            BadgeBg = SoftBrush(l.BorderColor)
        }.WithDensity(_density)).ToList();

        OverviewAiList.ItemsSource = BuildAiCards(vm, lang);

        OverviewTargetsTitle.Text = vm.PingTitle;
        OverviewTargetsExpanderHost.Visibility = vm.HasCustomPings ? Visibility.Visible : Visibility.Collapsed;
        OverviewTargetsList.ItemsSource = vm.CustomPings.Select(p => new CardVm
        {
            Title = p.Label,
            Badge = ShortState(p.State),
            Subtitle = p.Detail,
            AccentBrush = Brush(p.BorderColor),
            BadgeBg = SoftBrush(p.BorderColor),
            Url = p.Url ?? (p.Kind == "https" ? p.Target : null)
        }.WithDensity(_density)).ToList();

        OverviewLocalList.ItemsSource = local.Rows.Select(r => new KvVm { Label = r.Label, Value = r.Value }).ToList();
    }

    private void ApplyChain(ChainViewModel vm)
    {
        ChainOverall.Text = $"{vm.Overall}  ·  {vm.Headline}";
        ChainAi.Text = vm.AiHeadline;
        ChainMeta.Text = vm.Meta;
        ChainList.ItemsSource = vm.Rows.Select(r => new ChainRowVm
        {
            Title = r.DisplayName,
            Badge = r.StateLabel,
            Reasons = r.Reasons.Count == 0 ? "—" : string.Join("\n", r.Reasons),
            Metrics = r.MetricsSummary,
            AccentBrush = Brush(r.BorderColor),
            BadgeBg = SoftBrush(r.BorderColor)
        }).ToList();
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
            Badge = ShortStateFromDetail(a.Detail, a.DirectState, a.ProxyState),
            Detail = a.Detail,
            BadgeBg = SoftBrush(a.BorderColor),
            AccentBrush = Brush(a.BorderColor),
            DirectLabel = UiStrings.Direct(lang),
            ProxyLabel = UiStrings.ViaProxy(lang),
            DirectValue = $"{a.DirectState} · {a.DirectMs}",
            ProxyValue = $"{a.ProxyState} · {a.ProxyMs}",
            Url = a.OpenUrl
        }.WithDensity(_density)).ToList();

    private static string ShortState(string state) => state switch
    {
        "ok" => "正常",
        "fail" => "失败",
        "degraded" => "降级",
        "skipped" => "跳过",
        _ => state
    };

    private static string ShortStateFromDetail(string detail, string direct, string proxy)
    {
        if (detail.Contains("均可", StringComparison.Ordinal) || detail.Contains("direct + proxy", StringComparison.OrdinalIgnoreCase))
            return "正常";
        if (detail.Contains("不可达", StringComparison.Ordinal) || detail.Contains("unreachable", StringComparison.OrdinalIgnoreCase))
            return "失败";
        if (detail.Contains("仅", StringComparison.Ordinal) || detail.Contains("only", StringComparison.OrdinalIgnoreCase))
            return "降级";
        if (direct.Contains("正常", StringComparison.Ordinal) || proxy.Contains("正常", StringComparison.Ordinal)
            || direct.Equals("OK", StringComparison.OrdinalIgnoreCase) || proxy.Equals("OK", StringComparison.OrdinalIgnoreCase))
            return "正常";
        return "失败";
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
            Badge = ShortState(p.State),
            Subtitle = p.Detail,
            AccentBrush = Brush(p.BorderColor),
            BadgeBg = SoftBrush(p.BorderColor),
            Url = p.Url ?? (p.Kind == "https" ? p.Target : null),
            TargetKey = p.Kind == "https" ? $"https:{p.Target}" : $"ping:{p.Target}"
        }.WithDensity(_density)).ToList();
        TargetsEmptyPanel.Visibility = vm.HasCustomPings ? Visibility.Collapsed : Visibility.Visible;
        TargetsEmpty.Text = UiStrings.T(lang, "尚无自定义目标。添加 Ping 主机或 https:// URL。", "No custom targets yet. Add a ping host or https:// URL.");
    }

    private void ApplyOverallBadge(string overall)
    {
        var dark = IsDarkTheme();
        var key = overall.Trim();
        var (bg, fg, border) = key switch
        {
            "健康" or "healthy" => dark
                ? ("#1E3A2F", "#81C995", "#34A853")
                : ("#E6F4EA", "#137333", "#34A853"),
            "降级" or "degraded" => dark
                ? ("#3C2F1E", "#FDD663", "#FBBC04")
                : ("#FEF7E0", "#B06000", "#F9AB00"),
            _ when key.Contains("异常", StringComparison.Ordinal)
                || key.Contains("bad", StringComparison.OrdinalIgnoreCase)
                || key.Contains("失败", StringComparison.Ordinal)
                => dark
                    ? ("#3C1F1E", "#F28B82", "#EA4335")
                    : ("#FCE8E6", "#C5221F", "#EA4335"),
            _ => dark
                ? ("#1A2A3C", "#8AB4F8", "#8AB4F8")
                : ("#E8F0FE", "#1967D2", "#1A73E8")
        };
        OverallBadge.Background = Brush(bg);
        OverallBadge.BorderBrush = Brush(border);
        OverviewOverallBadge.Foreground = Brush(fg);
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
            Theme = config.Theme
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

    private static SolidColorBrush Brush(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);

    private SolidColorBrush SoftBrush(string borderHex)
    {
        var dark = IsDarkTheme();
        return borderHex.ToLowerInvariant() switch
        {
            "#34a853" or "#0f9d58" => Brush(dark ? "#1E3A2F" : "#E6F4EA"),
            "#fbbc04" or "#f9ab00" => Brush(dark ? "#3C2F1E" : "#FEF7E0"),
            "#ea4335" or "#d93025" => Brush(dark ? "#3C1F1E" : "#FCE8E6"),
            "#9aa0a6" or "#5f6368" or "#2d323c" => Brush(dark ? "#2A2F38" : "#F1F3F4"),
            _ => Brush(dark ? "#1A2A3C" : "#E8F0FE")
        };
    }

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
        public required System.Windows.Media.Brush AccentBrush { get; init; }
        public required System.Windows.Media.Brush BadgeBg { get; init; }
    }

    private sealed class KvVm
    {
        public required string Label { get; init; }
        public required string Value { get; init; }
    }
}
