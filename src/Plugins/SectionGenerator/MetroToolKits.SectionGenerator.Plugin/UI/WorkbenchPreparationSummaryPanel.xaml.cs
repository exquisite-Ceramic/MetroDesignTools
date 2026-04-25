using System.Windows;
using System.Windows.Controls;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 工具箱图纸准备摘要展示面板。
/// 该面板只负责显示 snapshot 驱动的只读摘要和入口按钮，并转发按钮事件，
/// 不负责 AutoCAD 命令桥接，也不负责模板管理窗口打开或 layer mapping 宿主逻辑。
/// </summary>
public partial class WorkbenchPreparationSummaryPanel : UserControl
{
    public event EventHandler<WorkbenchCommandRequestedEventArgs>? CommandRequested;

    public WorkbenchPreparationSummaryPanel()
    {
        InitializeComponent();
    }

    public void ApplyState(
        string preparationSummaryText,
        string preparationCountsText,
        string preparationModeText,
        string preparationEntryHintText)
    {
        PreparationSummaryTextBlock.Text = preparationSummaryText;
        PreparationCountsTextBlock.Text = preparationCountsText;
        PreparationModeTextBlock.Text = preparationModeText;
        PreparationEntryHintTextBlock.Text = preparationEntryHintText;
    }

    public void ApplyFallbackState(
        string preparationSummaryText,
        string preparationCountsText,
        string preparationModeText,
        string preparationEntryHintText)
    {
        ApplyState(
            preparationSummaryText,
            preparationCountsText,
            preparationModeText,
            preparationEntryHintText);
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
