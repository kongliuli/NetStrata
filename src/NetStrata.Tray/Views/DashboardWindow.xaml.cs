using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using NetStrata.Core.Storage;
using NetStrata.Core.Tui;

namespace NetStrata.Tray.Views;

public partial class DashboardWindow : Window
{
    private readonly JsonSampleStorage _storage = new();
    private readonly DispatcherTimer _timer;

    public DashboardWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) =>
        {
            await RefreshAsync();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    public async Task RefreshAsync()
    {
        try
        {
            var state = await _storage.ReadStateAsync(CancellationToken.None);
            Apply(DashboardMapper.FromState(state));
        }
        catch (Exception ex)
        {
            HeadlineText.Text = "Error: " + ex.Message;
        }
    }

    internal void Apply(DashboardViewModel vm)
    {
        TitleText.Text = $"NetStrata  {vm.Overall}";
        HeadlineText.Text = vm.Headline;
        MetaText.Text = vm.Meta;
        ProxyText.Text = vm.ProxySummary;

        if (string.IsNullOrWhiteSpace(vm.AlertsSummary))
            AlertsPanel.Visibility = Visibility.Collapsed;
        else
        {
            AlertsPanel.Visibility = Visibility.Visible;
            AlertsText.Text = vm.AlertsSummary;
        }

        LayersList.ItemsSource = vm.Layers.Select(l => new LayerCardView
        {
            Layer = l.Layer,
            State = l.State,
            BorderBrush = ToBrush(l.BorderColor)
        }).ToList();
    }

    private static System.Windows.Media.Brush ToBrush(string hex) =>
        new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);

    private sealed class LayerCardView
    {
        public required string Layer { get; init; }
        public required string State { get; init; }
        public required System.Windows.Media.Brush BorderBrush { get; init; }
    }
}
