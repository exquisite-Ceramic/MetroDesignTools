using System.Windows;
using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.ViewModels;
using MetroToolKits.SectionGenerator.Contracts.Preflight;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public partial class SectionPreflightWindow : Window
{
    private readonly SectionPreflightViewModel _viewModel = new();

    public SectionPreflightWindow(
        SectionPreflightReportDto report,
        ILogger<SectionPreflightWindow> logger)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(logger);

        InitializeComponent();
        LoadReport(report);
        logger.LogDebug("生成前检查窗口已加载，楼层数: {FloorCount}", report.FloorCount);
    }

    public void LoadReport(SectionPreflightReportDto report)
    {
        _viewModel.Load(report);

        SummaryTextBlock.Text = _viewModel.SummaryText;
        CountsTextBlock.Text = $"Blocking {_viewModel.BlockingCount}  Warning {_viewModel.WarningCount}  Info {_viewModel.InfoCount}";
        DrawingTextBlock.Text = _viewModel.DrawingDisplayName;
        SourceTextBlock.Text = _viewModel.ConfigSourceText;
        DrawingStatusTextBlock.Text = _viewModel.DrawingStatusText;
        FloorCountTextBlock.Text = _viewModel.FloorCount.ToString();
        BaseFloorTextBlock.Text = string.IsNullOrWhiteSpace(_viewModel.AlignmentBaseFloorName)
            ? "（未设置）"
            : _viewModel.AlignmentBaseFloorName;
        CanGenerateTextBlock.Text = _viewModel.CanGenerate ? "可以生成" : "当前不可生成";

        BlockingChecksGrid.ItemsSource = _viewModel.BlockingChecks;
        WarningChecksGrid.ItemsSource = _viewModel.WarningChecks;
        InfoChecksGrid.ItemsSource = _viewModel.InfoChecks;
        FloorsGrid.ItemsSource = _viewModel.Floors;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        Tag = SectionPreflightWindowAction.Refresh;
        DialogResult = true;
        Close();
    }

    private void OpenFloorConfig_Click(object sender, RoutedEventArgs e)
    {
        Tag = SectionPreflightWindowAction.OpenFloorConfig;
        DialogResult = true;
        Close();
    }

    private void OpenLayerMapping_Click(object sender, RoutedEventArgs e)
    {
        Tag = SectionPreflightWindowAction.OpenLayerMapping;
        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Tag = SectionPreflightWindowAction.Close;
        DialogResult = false;
        Close();
    }
}

public enum SectionPreflightWindowAction
{
    Close = 0,
    Refresh = 1,
    OpenFloorConfig = 2,
    OpenLayerMapping = 3
}
