using System.Windows;
using System.Windows.Controls;
using MetroToolKits.SectionGenerator.Contracts.Workbench;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 工具箱总览页展示面板。
/// 该面板只负责显示 snapshot 驱动的只读状态，并转发按钮事件，
/// 不负责 AutoCAD 命令桥接，也不负责 use case / assembler 调度。
/// </summary>
public partial class WorkbenchOverviewPanel : UserControl
{
    public event EventHandler? RefreshRequested;

    public event EventHandler<WorkbenchCommandRequestedEventArgs>? CommandRequested;

    public WorkbenchOverviewPanel()
    {
        InitializeComponent();
    }

    public void SetStatusText(string text)
    {
        StatusTextBlock.Text = text;
    }

    public void ApplySnapshot(SectionWorkbenchSnapshotDto snapshot)
    {
        ConfigSummaryTextBlock.Text = snapshot.FloorConfig.SummaryText;
        ReadinessSummaryTextBlock.Text = snapshot.ElementReadiness.SummaryText;
        SectionsSummaryTextBlock.Text = snapshot.ExistingSections.SummaryText;
        RecommendedActionTextBlock.Text = snapshot.RecommendedAction.Message;
        PrimaryActionButton.Content = snapshot.RecommendedAction.ButtonText;
        PrimaryActionButton.Tag = snapshot.RecommendedAction.CommandTag;
    }

    public void ApplyFallbackState(
        string configSummary,
        string readinessSummary,
        string sectionsSummary,
        string recommendedActionText,
        string primaryActionText,
        string primaryActionCommandTag)
    {
        ConfigSummaryTextBlock.Text = configSummary;
        ReadinessSummaryTextBlock.Text = readinessSummary;
        SectionsSummaryTextBlock.Text = sectionsSummary;
        RecommendedActionTextBlock.Text = recommendedActionText;
        PrimaryActionButton.Content = primaryActionText;
        PrimaryActionButton.Tag = primaryActionCommandTag;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string commandName)
        {
            return;
        }

        CommandRequested?.Invoke(this, new WorkbenchCommandRequestedEventArgs(commandName));
    }
}

public sealed class WorkbenchCommandRequestedEventArgs : EventArgs
{
    public WorkbenchCommandRequestedEventArgs(string commandName)
    {
        CommandName = commandName ?? throw new ArgumentNullException(nameof(commandName));
    }

    public string CommandName { get; }
}
