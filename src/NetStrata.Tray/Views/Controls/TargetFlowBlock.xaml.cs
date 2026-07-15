using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using NetStrata.Core.Flow;
using NetStrata.Tray.Services;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfPanel = System.Windows.Controls.Panel;
using WpfPoint = System.Windows.Point;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace NetStrata.Tray.Views.Controls;

public partial class TargetFlowBlock : WpfUserControl
{
    private const double NodeWidth = 100;
    private const double NodeHeight = 56;
    private readonly Dictionary<string, FlowNodeState> _states = [];
    private readonly Dictionary<string, (Border Border, TextBlock Meta)> _nodeViews = [];
    private readonly Dictionary<(string From, string To), Line> _edgeViews = [];
    private IReadOnlyDictionary<string, WpfPoint> _points = new Dictionary<string, WpfPoint>();
    private TargetPathBlock? _block;
    private TargetPathBlock? _pending;
    private bool _pendingEgressChanged;
    private FlowTrace? _trace;
    private CancellationTokenSource? _playback;
    private int _stageIndex;
    private bool _isPlaying;
    private bool _isPaused;
    private bool _rendered;

    public TargetFlowBlock()
    {
        InitializeComponent();
        Unloaded += (_, _) => StopPlayback();
    }

    public string? BlockId => _block?.Id;
    public bool IsPlaying => _isPlaying;
    public event Action<string>? OpenTrendRequested;

    private bool _egressChanged;

    public void Bind(TargetPathBlock block, bool egressChanged = false)
    {
        _egressChanged = egressChanged;
        var outcomeFlipped = _block is not null
            && _block.Id == block.Id
            && _block.Outcome != block.Outcome;

        // W9a: never tear down an in-flight animation; apply after playback ends
        if (_isPlaying)
        {
            _pending = block;
            _pendingEgressChanged = egressChanged || _pendingEgressChanged;
            StatusText.Text = IsEnglish()
                ? "New sample ready after replay"
                : "新采样已就绪，将在重放结束后更新";
            return;
        }

        // Identical content — refresh header chrome only, keep canvas / expand state
        if (_block is not null
            && string.Equals(_block.Fingerprint, block.Fingerprint, StringComparison.Ordinal)
            && !egressChanged)
        {
            ApplyHeader(block);
            _block = block;
            _trace = block.Trace;
            return;
        }

        var wasExpanded = RootExpander.IsExpanded;
        var keepId = _block?.Id;
        ApplyHeader(block);
        _block = block;
        _trace = block.Trace;
        _pending = null;
        if (outcomeFlipped || egressChanged)
            PulseCard();

        if (keepId == block.Id && wasExpanded)
        {
            RootExpander.IsExpanded = true;
            ResetPlayback(render: true);
        }
        else if (keepId != block.Id)
        {
            RootExpander.IsExpanded = false;
            StopPlayback();
            _rendered = false;
        }
    }

    private void ApplyHeader(TargetPathBlock block)
    {
        TitleText.Text = block.Title;
        EgressText.Text = block.EgressLabel;
        SummaryText.Text = block.Summary;
        OutcomeDot.Background = StateBrush(block.Outcome);
        var (badge, brush) = BadgeFor(block);
        BadgeText.Text = badge;
        BadgeText.Foreground = brush;
        BadgeBorder.BorderBrush = brush;
        BadgeBorder.Background = SoftBrush(brush);
        EgressFlipBadge.Visibility = _egressChanged ? Visibility.Visible : Visibility.Collapsed;
        EgressFlipText.Text = IsEnglish() ? "Egress flipped ⇄" : "出口已切换 ⇄";
        TrendButton.Content = IsEnglish() ? "Trend" : "趋势";
    }

    private (string Text, SolidColorBrush Brush) BadgeFor(TargetPathBlock block)
    {
        var en = IsEnglish();
        return block.Outcome switch
        {
            FlowNodeState.Failed => (en ? "Fail" : "失败", StateBrush(FlowNodeState.Failed)),
            FlowNodeState.Degraded => (en ? "Degraded" : "降级", StateBrush(FlowNodeState.Degraded)),
            FlowNodeState.Passed when block.Lane == "proxy" =>
                (en ? "Proxy" : "代理", AccentBrush()),
            FlowNodeState.Passed => (en ? "OK" : "正常", StateBrush(FlowNodeState.Passed)),
            _ => (en ? "?" : "待确认", StateBrush(FlowNodeState.Unknown))
        };
    }

    private void RootExpander_Expanded(object sender, RoutedEventArgs e)
    {
        // W9d: expand shows final states immediately (static path), replay is optional
        if (!_rendered)
            ResetPlayback(render: true);
        else
            Render();
    }

    private void RootExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        StopPlayback();
        PlayButton.Content = IsEnglish() ? "Replay" : "重放";
        _isPaused = false;
        _stageIndex = 0;
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

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetPlayback(render: true);

    private void Trend_Click(object sender, RoutedEventArgs e)
    {
        if (_block is null)
            return;
        OpenTrendRequested?.Invoke(_block.Title);
    }

    /// <summary>W9h: brief flash when outcome / egress changes — event cue, not auto-play.</summary>
    private void PulseCard()
    {
        var anim = new DoubleAnimation(1, 0.35, TimeSpan.FromMilliseconds(160))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2)
        };
        CardBorder.BeginAnimation(OpacityProperty, anim);
    }

    private async Task RunPlaybackAsync(bool restart)
    {
        if (_trace is null)
            return;
        if (restart)
        {
            _stageIndex = 0;
            ResetStates(pending: true);
            UpdateVisuals();
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
                StatusText.Text = string.Join(IsEnglish() ? " → " : " → ", group.Select(n => n.Label));

                var duration = DurationFor(group);
                await AnimateIncomingAsync(group.Select(x => x.Id).ToHashSet(), duration, token);

                foreach (var node in group)
                    _states[node.Id] = node.State;
                UpdateVisuals();
                _stageIndex++;
                await Task.Delay(SystemParameters.ClientAreaAnimation ? Scaled(60) : 16, token);
            }

            _isPlaying = false;
            PlayButton.Content = IsEnglish() ? "Replay" : "重放";
            StatusText.Text = _block?.Outcome == FlowNodeState.Passed
                ? (IsEnglish() ? $"Reached via {_block.Trace.ActiveLane}" : $"已到达 · {_block!.EgressLabel}")
                : (IsEnglish() ? "Path failed" : "路径未通");
            ApplyPending();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ApplyPending()
    {
        if (_pending is null)
            return;
        var next = _pending;
        var flip = _pendingEgressChanged;
        _pending = null;
        _pendingEgressChanged = false;
        Bind(next, flip);
    }

    private void PausePlayback()
    {
        _playback?.Cancel();
        _isPlaying = false;
        _isPaused = true;
        RemoveMarkers();
        PlayButton.Content = IsEnglish() ? "Resume" : "继续";
        StatusText.Text = IsEnglish() ? "Paused" : "已暂停";
    }

    private void StopPlayback()
    {
        _playback?.Cancel();
        _playback?.Dispose();
        _playback = null;
        _isPlaying = false;
        RemoveMarkers();
    }

    private void ResetPlayback(bool render)
    {
        StopPlayback();
        _isPaused = false;
        _stageIndex = 0;
        // W9d: idle view shows final node states (static path); replay starts from Pending
        ResetStates(pending: false);
        PlayButton.Content = IsEnglish() ? "Replay" : "重放";
        StatusText.Text = IsEnglish()
            ? "Static path shown — press Replay to animate"
            : "已显示静态路径 — 点「重放」看动画";
        if (render && RootExpander.IsExpanded)
            Render();
    }

    private void ResetStates(bool pending)
    {
        _states.Clear();
        if (_trace is null)
            return;
        foreach (var node in _trace.Nodes)
            _states[node.Id] = pending ? FlowNodeState.Pending : node.State;
    }

    private void FlowCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!RootExpander.IsExpanded || _trace is null)
            return;
        if (_isPlaying)
            PausePlayback();
        Render();
    }

    private void Render()
    {
        if (_trace is null || FlowCanvas.ActualWidth <= 0)
            return;

        var width = Math.Max(280, FlowCanvas.ActualWidth);
        var ids = _trace.Nodes.Select(n => n.Id).ToList();
        var layout = NetworkFlowLayout.CalculateProbe(ids, width);
        _points = NetworkFlowLayout.ResolveRelativeX(layout, width);
        FlowCanvas.Height = Math.Min(220, Math.Max(160, layout.Height));
        FlowCanvas.Children.Clear();
        _nodeViews.Clear();
        _edgeViews.Clear();
        _rendered = true;

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
                StrokeThickness = 2.5,
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
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(2),
            Padding = new Thickness(6, 4, 6, 3),
            Child = new StackPanel()
        };
        border.SetResourceReference(BackgroundProperty, "CardBg");
        var panel = (StackPanel)border.Child;
        panel.Children.Add(new TextBlock
        {
            Text = node.Label,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var meta = new TextBlock
        {
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 10,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        meta.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
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
            view.Meta.Text = state is FlowNodeState.Pending or FlowNodeState.Active || node.DurationMs is null
                ? StateLabel(state)
                : $"{StateLabel(state)} · {node.DurationMs:F0} ms";
        }

        foreach (var edge in _trace.Edges)
        {
            if (!_edgeViews.TryGetValue((edge.From, edge.To), out var line))
                continue;
            var to = _states.GetValueOrDefault(edge.To, FlowNodeState.Pending);
            var from = _states.GetValueOrDefault(edge.From, FlowNodeState.Pending);
            var edgeState = from is FlowNodeState.Failed or FlowNodeState.Degraded ? from
                : to == FlowNodeState.Active ? FlowNodeState.Active
                : to is FlowNodeState.Passed or FlowNodeState.Failed or FlowNodeState.Degraded ? to
                : FlowNodeState.Pending;
            line.Stroke = StateBrush(edgeState);
        }
    }

    private async Task AnimateIncomingAsync(HashSet<string> nodeIds, int duration, CancellationToken token)
    {
        if (_trace is null || !SystemParameters.ClientAreaAnimation)
        {
            await Task.Delay(16, token);
            return;
        }

        var markers = new List<Ellipse>();
        foreach (var edge in _trace.Edges.Where(x =>
                     nodeIds.Contains(x.To)
                     && _states.GetValueOrDefault(x.From) is not (FlowNodeState.Failed or FlowNodeState.Skipped)))
        {
            if (!_points.TryGetValue(edge.From, out var from) || !_points.TryGetValue(edge.To, out var to))
                continue;
            var marker = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = AccentBrush(),
                Tag = "flow-marker",
                RenderTransform = new TranslateTransform()
            };
            Canvas.SetLeft(marker, from.X - 4);
            Canvas.SetTop(marker, from.Y - 4);
            WpfPanel.SetZIndex(marker, 3);
            FlowCanvas.Children.Add(marker);
            markers.Add(marker);
            var transform = (TranslateTransform)marker.RenderTransform;
            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(
                0, to.X - from.X, TimeSpan.FromMilliseconds(duration))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.Stop
            });
            transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(
                0, to.Y - from.Y, TimeSpan.FromMilliseconds(duration))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.Stop
            });
        }

        try { await Task.Delay(duration, token); }
        finally
        {
            foreach (var m in markers)
                FlowCanvas.Children.Remove(m);
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
        var mapped = measured <= 0 ? 260 : Math.Clamp(80 + Math.Sqrt(measured) * 40, 160, 800);
        return Scaled((int)mapped);
    }

    private int Scaled(int value)
    {
        var multiplier = SpeedBox.SelectedItem is ComboBoxItem { Tag: string tag }
            && double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 1;
        return Math.Max(16, (int)(value * multiplier));
    }

    private void RemoveMarkers()
    {
        foreach (var m in FlowCanvas.Children.OfType<FrameworkElement>().Where(x => Equals(x.Tag, "flow-marker")).ToList())
            FlowCanvas.Children.Remove(m);
    }

    private string StateLabel(FlowNodeState state) => (state, IsEnglish()) switch
    {
        (FlowNodeState.Pending, false) => "待播放",
        (FlowNodeState.Active, false) => "流转中",
        (FlowNodeState.Passed, false) => "通过",
        (FlowNodeState.Failed, false) => "失败",
        (FlowNodeState.Degraded, false) => "降级",
        (FlowNodeState.Pending, true) => "Pending",
        (FlowNodeState.Active, true) => "Active",
        (FlowNodeState.Passed, true) => "OK",
        (FlowNodeState.Failed, true) => "Fail",
        _ => "—"
    };

    private SolidColorBrush StateBrush(FlowNodeState state)
    {
        var dark = ThemeApplier.CurrentPalette() == NetStrata.Core.Ui.ThemeResolver.Dark;
        return state switch
        {
            FlowNodeState.Active => AccentBrush(),
            FlowNodeState.Passed => Brush(dark ? "#81C995" : "#137333"),
            FlowNodeState.Degraded => Brush(dark ? "#FDD663" : "#B06000"),
            FlowNodeState.Failed => Brush(dark ? "#F28B82" : "#C5221F"),
            _ => Brush(ThemeApplier.CurrentPalette().Muted)
        };
    }

    private SolidColorBrush AccentBrush() => Brush(ThemeApplier.CurrentPalette().Accent);
    private bool IsEnglish() =>
        string.Equals(_trace?.Language ?? _block?.Trace.Language, "en", StringComparison.OrdinalIgnoreCase);

    private static SolidColorBrush SoftBrush(SolidColorBrush solid)
    {
        var c = solid.Color;
        var soft = new SolidColorBrush(WpfColor.FromArgb(40, c.R, c.G, c.B));
        soft.Freeze();
        return soft;
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
