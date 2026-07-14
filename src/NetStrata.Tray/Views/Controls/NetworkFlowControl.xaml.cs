using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using NetStrata.Core.Flow;
using NetStrata.Tray.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfPanel = System.Windows.Controls.Panel;
using WpfPoint = System.Windows.Point;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace NetStrata.Tray.Views.Controls;

public partial class NetworkFlowControl : WpfUserControl
{
    private const double NodeWidth = 124;
    private const double NodeHeight = 66;
    private readonly Dictionary<string, FlowNodeState> _states = [];
    private readonly Dictionary<string, (Border Border, TextBlock Meta)> _nodeViews = [];
    private readonly Dictionary<(string From, string To), Line> _edgeViews = [];
    private IReadOnlyList<FlowTrace> _traces = [];
    private IReadOnlyList<FlowTrace>? _pendingTraces;
    private IReadOnlyDictionary<string, WpfPoint> _points = new Dictionary<string, WpfPoint>();
    private FlowTrace? _trace;
    private CancellationTokenSource? _playback;
    private int _stageIndex;
    private bool _isPlaying;
    private bool _isPaused;

    public NetworkFlowControl()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => StopPlayback();
    }

    public void SetTraces(IReadOnlyList<FlowTrace> traces)
    {
        if (_isPlaying)
        {
            _pendingTraces = traces;
            FlowStatus.Text = IsEnglish() ? "A new sample is ready after playback" : "新采样已就绪，将在播放结束后更新";
            return;
        }

        ApplyTraces(traces);
    }

    private void ApplyTraces(IReadOnlyList<FlowTrace> traces)
    {
        _traces = traces;
        var mode = _trace?.Mode ?? FlowTraceMode.Layers;
        _trace = traces.FirstOrDefault(x => x.Mode == mode) ?? traces.FirstOrDefault();
        UpdateModeLabels();
        ResetPlayback();
    }

    private void UpdateModeLabels()
    {
        LayersModeButton.Content = _traces.FirstOrDefault(x => x.Mode == FlowTraceMode.Layers)?.Title ?? "分层诊断";
        RoutesModeButton.Content = _traces.FirstOrDefault(x => x.Mode == FlowTraceMode.Routes)?.Title ?? "直连 / 代理";
        TlsModeButton.Content = IsEnglish() ? "TLS stack" : "TLS 栈";
        UpdateSelectedMode();
    }

    private void Mode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: string tag }
            || !Enum.TryParse<FlowTraceMode>(tag, out var mode))
            return;

        var next = _traces.FirstOrDefault(x => x.Mode == mode);
        if (next is null || next == _trace)
            return;

        StopPlayback();
        _trace = next;
        UpdateSelectedMode();
        ResetPlayback();
    }

    private void UpdateSelectedMode()
    {
        foreach (var button in new[] { LayersModeButton, RoutesModeButton, TlsModeButton })
        {
            var selected = _trace is not null && string.Equals(
                button.Tag?.ToString(), _trace.Mode.ToString(), StringComparison.Ordinal);
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            button.BorderBrush = selected ? AccentBrush() : DefaultBorderBrush();
            AutomationProperties.SetName(button,
                $"{button.Content}, {(selected ? (IsEnglish() ? "selected" : "已选择") : "")}".TrimEnd(',', ' '));
        }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_trace is null || !_trace.HasData)
            return;
        if (_isPlaying)
        {
            PausePlayback();
            return;
        }

        var restart = !_isPaused && _stageIndex >= StageGroups().Count;
        _ = RunPlaybackAsync(restart);
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetPlayback();

    private async Task RunPlaybackAsync(bool restart)
    {
        if (_trace is null)
            return;
        if (restart)
        {
            _stageIndex = 0;
            ResetStates();
        }

        StopPlayback();
        _isPlaying = true;
        _isPaused = false;
        PlayButton.Content = IsEnglish() ? "Pause" : "暂停";
        _playback = new CancellationTokenSource();
        var token = _playback.Token;

        try
        {
            var groups = StageGroups();
            while (_stageIndex < groups.Count)
            {
                var group = groups[_stageIndex];
                foreach (var node in group)
                    _states[node.Id] = FlowNodeState.Active;
                UpdateVisuals();
                FlowStatus.Text = string.Join(IsEnglish() ? ", " : "，", group.Select(ActiveText));

                var duration = DurationFor(group);
                await AnimateIncomingAsync(group.Select(x => x.Id).ToHashSet(), duration, token);

                foreach (var node in group)
                    _states[node.Id] = node.State;
                UpdateVisuals();
                FlowStatus.Text = string.Join(IsEnglish() ? "; " : "；", group.Select(NodeDetail));
                _stageIndex++;
                await Task.Delay(SystemParameters.ClientAreaAnimation ? Scaled(80) : 20, token);
            }

            _isPlaying = false;
            PlayButton.Content = IsEnglish() ? "Replay" : "重放";
            var failed = _trace.Nodes.Where(x => x.State is FlowNodeState.Failed or FlowNodeState.Degraded).ToList();
            FlowStatus.Text = failed.Count == 0
                ? (IsEnglish() ? "Replay complete, all stages passed" : "结果重放完成，全部阶段通过")
                : $"{string.Join(IsEnglish() ? ", " : "、", failed.Select(x => x.Label))}{(IsEnglish() ? " needs attention" : "需要关注")}";
            ApplyPendingTraces();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void PausePlayback()
    {
        _playback?.Cancel();
        _isPlaying = false;
        _isPaused = true;
        var current = StageGroups().ElementAtOrDefault(_stageIndex);
        if (current is not null)
            foreach (var node in current)
                _states[node.Id] = FlowNodeState.Pending;
        RemoveMarkers();
        UpdateVisuals();
        PlayButton.Content = IsEnglish() ? "Resume" : "继续";
        FlowStatus.Text = IsEnglish() ? "Playback paused" : "播放已暂停";
    }

    private void StopPlayback()
    {
        _playback?.Cancel();
        _playback?.Dispose();
        _playback = null;
        _isPlaying = false;
        RemoveMarkers();
    }

    private void ResetPlayback()
    {
        StopPlayback();
        _isPaused = false;
        _stageIndex = 0;
        ResetStates();
        PlayButton.Content = IsEnglish() ? "Play" : "播放";
        FlowStatus.Text = _trace?.HasData == true
            ? (IsEnglish() ? "Choose a mode, then press Play" : "选择模式后点击播放")
            : (IsEnglish() ? "Waiting for diagnostic data" : "等待诊断数据");
        Render();
    }

    private void ResetStates()
    {
        _states.Clear();
        if (_trace is null)
            return;
        foreach (var node in _trace.Nodes)
            _states[node.Id] = FlowNodeState.Pending;
    }

    private void FlowCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isPlaying)
            PausePlayback();
        Render();
    }

    private void Render()
    {
        if (_trace is null || FlowCanvas.ActualWidth <= 0)
            return;

        var width = Math.Max(320, FlowCanvas.ActualWidth);
        var layout = NetworkFlowLayout.Calculate(_trace.Mode, width);
        _points = NetworkFlowLayout.ResolveRelativeX(layout, width);
        FlowCanvas.Height = layout.Height;
        FlowCanvas.Children.Clear();
        _nodeViews.Clear();
        _edgeViews.Clear();
        FlowTitle.Text = _trace.Title;
        FlowDisclosure.Text = _trace.Disclosure;
        CapturedAtText.Text = _trace.CapturedAt;

        foreach (var edge in _trace.Edges)
        {
            if (!_points.TryGetValue(edge.From, out var from) || !_points.TryGetValue(edge.To, out var to))
                continue;
            var line = new Line
            {
                X1 = from.X,
                Y1 = from.Y,
                X2 = to.X,
                Y2 = to.Y,
                StrokeThickness = 3,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };
            _edgeViews[(edge.From, edge.To)] = line;
            FlowCanvas.Children.Add(line);
        }

        foreach (var node in _trace.Nodes)
            AddNode(node);
        UpdateVisuals();
    }

    private void AddNode(FlowNode node)
    {
        if (!_points.TryGetValue(node.Id, out var point))
            return;
        var border = new Border
        {
            Width = NodeWidth,
            Height = NodeHeight,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            Padding = new Thickness(10, 7, 10, 6),
            Child = new StackPanel()
        };
        border.SetResourceReference(BackgroundProperty, "CardBg");
        var panel = (StackPanel)border.Child;
        var title = new TextBlock
        {
            Text = node.Label,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var meta = new TextBlock
        {
            Margin = new Thickness(0, 5, 0, 0),
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        meta.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
        panel.Children.Add(title);
        panel.Children.Add(meta);
        Canvas.SetLeft(border, point.X - NodeWidth / 2);
        Canvas.SetTop(border, point.Y - NodeHeight / 2);
        WpfPanel.SetZIndex(border, 2);
        _nodeViews[node.Id] = (border, meta);
        FlowCanvas.Children.Add(border);
    }

    private void UpdateVisuals()
    {
        if (_trace is null)
            return;
        foreach (var node in _trace.Nodes)
        {
            if (!_nodeViews.TryGetValue(node.Id, out var view))
                continue;
            var state = _states.GetValueOrDefault(node.Id, FlowNodeState.Pending);
            view.Border.BorderBrush = StateBrush(state);
            view.Border.Opacity = state == FlowNodeState.Skipped ? 0.68 : 1;
            view.Meta.Text = NodeMeta(node, state);
            AutomationProperties.SetName(view.Border, NodeDetail(node, state));
        }

        foreach (var edge in _trace.Edges)
        {
            if (!_edgeViews.TryGetValue((edge.From, edge.To), out var line))
                continue;
            line.Stroke = StateBrush(EdgeState(edge));
            line.StrokeDashArray = EdgeState(edge) == FlowNodeState.Skipped
                ? new DoubleCollection([4, 4])
                : null;
        }
    }

    private async Task AnimateIncomingAsync(HashSet<string> nodeIds, int duration, CancellationToken token)
    {
        if (_trace is null || !SystemParameters.ClientAreaAnimation)
        {
            await Task.Delay(20, token);
            return;
        }

        var markers = new List<Ellipse>();
        foreach (var edge in _trace.Edges.Where(x =>
                     nodeIds.Contains(x.To)
                     && _states.GetValueOrDefault(x.From, FlowNodeState.Pending)
                         is not (FlowNodeState.Failed or FlowNodeState.Skipped)))
        {
            if (!_points.TryGetValue(edge.From, out var from) || !_points.TryGetValue(edge.To, out var to))
                continue;
            var marker = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = AccentBrush(),
                IsHitTestVisible = false,
                Tag = "flow-marker",
                RenderTransform = new TranslateTransform()
            };
            Canvas.SetLeft(marker, from.X - 5);
            Canvas.SetTop(marker, from.Y - 5);
            WpfPanel.SetZIndex(marker, 3);
            FlowCanvas.Children.Add(marker);
            markers.Add(marker);
            var transform = (TranslateTransform)marker.RenderTransform;
            var animation = new DoubleAnimation(0, to.X - from.X, TimeSpan.FromMilliseconds(duration))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.Stop
            };
            transform.BeginAnimation(TranslateTransform.XProperty, animation);
            animation = new DoubleAnimation(0, to.Y - from.Y, TimeSpan.FromMilliseconds(duration))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.Stop
            };
            transform.BeginAnimation(TranslateTransform.YProperty, animation);
        }

        try
        {
            await Task.Delay(duration, token);
        }
        finally
        {
            foreach (var marker in markers)
                FlowCanvas.Children.Remove(marker);
        }
    }

    private IReadOnlyList<IReadOnlyList<FlowNode>> StageGroups() => _trace?.Nodes
        .GroupBy(x => x.Stage)
        .OrderBy(x => x.Key)
        .Select(x => (IReadOnlyList<FlowNode>)x.ToList())
        .ToList() ?? [];

    private int DurationFor(IReadOnlyList<FlowNode> group)
    {
        var measured = group.Max(x => x.DurationMs ?? 0);
        var mapped = measured <= 0 ? 320 : Math.Clamp(90 + Math.Sqrt(measured) * 45, 220, 900);
        return Scaled((int)mapped);
    }

    private int Scaled(int value)
    {
        var multiplier = SpeedBox.SelectedItem is ComboBoxItem { Tag: string tag }
            && double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 1;
        return Math.Max(20, (int)(value * multiplier));
    }

    private FlowNodeState EdgeState(FlowEdge edge)
    {
        var from = _states.GetValueOrDefault(edge.From, FlowNodeState.Pending);
        var to = _states.GetValueOrDefault(edge.To, FlowNodeState.Pending);
        if (from is FlowNodeState.Failed or FlowNodeState.Degraded)
            return from;
        if (to == FlowNodeState.Active)
            return FlowNodeState.Active;
        if (to == FlowNodeState.Skipped)
            return FlowNodeState.Skipped;
        return to is FlowNodeState.Passed or FlowNodeState.Failed or FlowNodeState.Degraded ? to : FlowNodeState.Pending;
    }

    private string NodeMeta(FlowNode node, FlowNodeState state)
    {
        var label = StateLabel(state);
        return state is FlowNodeState.Pending or FlowNodeState.Active or FlowNodeState.Skipped || node.DurationMs is null
            ? label
            : $"{label} · {node.DurationMs:F0} ms";
    }

    private string NodeDetail(FlowNode node) => NodeDetail(node, _states.GetValueOrDefault(node.Id, node.State));

    private string NodeDetail(FlowNode node, FlowNodeState state)
    {
        var duration = node.DurationMs is null ? "" : $", {node.DurationMs:F0} ms";
        return $"{node.Label}, {StateLabel(state)}{duration}, {node.Detail}";
    }

    private string ActiveText(FlowNode node) => IsEnglish() ? $"Probing {node.Label}" : $"正在重放 {node.Label}";

    private string StateLabel(FlowNodeState state) => (state, IsEnglish()) switch
    {
        (FlowNodeState.Pending, false) => "待播放",
        (FlowNodeState.Active, false) => "探测中",
        (FlowNodeState.Passed, false) => "通过",
        (FlowNodeState.Degraded, false) => "降级",
        (FlowNodeState.Failed, false) => "失败",
        (FlowNodeState.Skipped, false) => "未执行",
        (FlowNodeState.Unknown, false) => "待确认",
        (FlowNodeState.Pending, true) => "Pending",
        (FlowNodeState.Active, true) => "Probing",
        (FlowNodeState.Passed, true) => "Passed",
        (FlowNodeState.Degraded, true) => "Degraded",
        (FlowNodeState.Failed, true) => "Failed",
        (FlowNodeState.Skipped, true) => "Not run",
        _ => "Unknown"
    };

    private SolidColorBrush StateBrush(FlowNodeState state)
    {
        var dark = IsDark();
        return state switch
        {
            FlowNodeState.Active => AccentBrush(),
            FlowNodeState.Passed => Brush(dark ? "#81C995" : "#137333"),
            FlowNodeState.Degraded => Brush(dark ? "#FDD663" : "#B06000"),
            FlowNodeState.Failed => Brush(dark ? "#F28B82" : "#C5221F"),
            FlowNodeState.Skipped or FlowNodeState.Unknown => Brush(ThemeApplier.CurrentPalette().Muted),
            _ => DefaultBorderBrush()
        };
    }

    private SolidColorBrush AccentBrush() => Brush(ThemeApplier.CurrentPalette().Accent);
    private SolidColorBrush DefaultBorderBrush() => Brush(ThemeApplier.CurrentPalette().CardBorder);
    private bool IsDark() => ThemeApplier.CurrentPalette() == NetStrata.Core.Ui.ThemeResolver.Dark;
    private bool IsEnglish() => string.Equals(_trace?.Language, "en", StringComparison.OrdinalIgnoreCase);

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }

    private void RemoveMarkers()
    {
        var markers = FlowCanvas.Children.OfType<FrameworkElement>()
            .Where(x => Equals(x.Tag, "flow-marker"))
            .ToList();
        foreach (var marker in markers)
            FlowCanvas.Children.Remove(marker);
    }

    private void ApplyPendingTraces()
    {
        if (_pendingTraces is null)
            return;
        var pending = _pendingTraces;
        _pendingTraces = null;
        ApplyTraces(pending);
    }
}
