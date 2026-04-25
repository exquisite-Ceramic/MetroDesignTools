using System.Windows;
using System.Windows.Controls;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 工具箱剖面生成页展示面板。
/// 该面板只负责显示 snapshot 驱动的只读状态，并转发命令按钮事件，
/// 不负责 AutoCAD 命令桥接，也不负责 use case / assembler 调度。
/// </summary>
public partial class WorkbenchGenerationPanel : UserControl
{
    public event EventHandler<WorkbenchCommandRequestedEventArgs>? CommandRequested;

    public WorkbenchGenerationPanel()
    {
        InitializeComponent();
    }

    public void ApplyState(
        string generationStatusText,
        string generationSummaryText,
        string missingItemsSummaryText)
    {
        GenerationStatusTextBlock.Text = generationStatusText;
        GenerationSummaryTextBlock.Text = generationSummaryText;
        MissingItemsSummaryTextBlock.Text = missingItemsSummaryText;
    }

    public void ApplyFallbackState(
        string generationStatusText,
        string generationSummaryText,
        string missingItemsSummaryText)
    {
        ApplyState(generationStatusText, generationSummaryText, missingItemsSummaryText);
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
