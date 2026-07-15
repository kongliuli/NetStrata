using System.Windows;
using System.Windows.Controls;
using NetStrata.Core.Flow;
using NetStrata.Core.Ui;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace NetStrata.Tray.Views.Controls;

public partial class NetworkFlowControl : WpfUserControl
{
    private readonly Dictionary<string, TargetFlowBlock> _blocks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _prevLanes = new(StringComparer.Ordinal);
    private string? _lang;

    public event Action<string>? OpenTrendRequested;
    public event Action? ProbeRequested;

    public NetworkFlowControl() => InitializeComponent();

    public void SetBlocks(IReadOnlyList<TargetPathBlock> blocks, string? lang = null)
    {
        _lang = LangResolver.Resolve(lang);
        HeaderTitle.Text = UiStrings.T(_lang, "监控目标（点击展开）", "Monitored targets (click to expand)");
        HeaderHint.Text = UiStrings.T(_lang,
            "每个目标独立一块动画；标题上标明该目标的流量出口（直连 / 代理可不相同）。异常目标已置顶。",
            "Each target is its own animation block; the header states that target's egress. Failures sort first.");
        EmptyText.Text = UiStrings.T(_lang,
            "本轮采样尚无 HTTPS 监控目标。可先点「立即探测」刷新。",
            "No HTTPS monitored targets in this sample. Probe now to refresh.");
        EmptyProbeButton.Content = UiStrings.T(_lang, "立即探测", "Probe now");
        EmptyPanel.Visibility = blocks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var keep = new HashSet<string>(blocks.Select(b => b.Id), StringComparer.Ordinal);
        foreach (var stale in _blocks.Keys.Where(id => !keep.Contains(id)).ToList())
        {
            _blocks[stale].OpenTrendRequested -= OnBlockOpenTrend;
            BlocksHost.Children.Remove(_blocks[stale]);
            _blocks.Remove(stale);
            _prevLanes.Remove(stale);
        }

        foreach (var block in blocks)
        {
            if (!_blocks.TryGetValue(block.Id, out var view))
            {
                view = new TargetFlowBlock();
                view.OpenTrendRequested += OnBlockOpenTrend;
                _blocks[block.Id] = view;
            }

            // W9e: mark one-cycle egress flip vs previous sample
            var egressChanged = _prevLanes.TryGetValue(block.Id, out var prev)
                && !string.Equals(prev, block.Lane, StringComparison.Ordinal);
            view.Bind(block, egressChanged);
        }

        foreach (var block in blocks)
            _prevLanes[block.Id] = block.Lane;

        // W9b: reorder in place — never Clear()+re-add the whole list
        ReorderChildren(blocks);
    }

    private void ReorderChildren(IReadOnlyList<TargetPathBlock> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            var view = _blocks[blocks[i].Id];
            var at = BlocksHost.Children.IndexOf(view);
            if (at < 0)
            {
                BlocksHost.Children.Insert(i, view);
                continue;
            }

            if (at == i)
                continue;

            BlocksHost.Children.RemoveAt(at);
            BlocksHost.Children.Insert(i, view);
        }
    }

    private void OnBlockOpenTrend(string title) => OpenTrendRequested?.Invoke(title);

    private void EmptyProbe_Click(object sender, RoutedEventArgs e) => ProbeRequested?.Invoke();
}
