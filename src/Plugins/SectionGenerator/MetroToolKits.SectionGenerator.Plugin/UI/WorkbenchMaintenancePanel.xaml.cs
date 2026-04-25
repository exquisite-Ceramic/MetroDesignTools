using System.Windows;
using System.Windows.Controls;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 工具箱剖面维护页展示面板。
/// 该面板只负责显示 snapshot 驱动的只读状态，并转发命令按钮事件，
/// 不负责 AutoCAD 命令桥接，也不负责 use case / assembler 调度。
/// </summary>
public partial class WorkbenchMaintenancePanel : UserControl
{
    public event EventHandler<WorkbenchCommandRequestedEventArgs>? CommandRequested;

    public WorkbenchMaintenancePanel()
    {
        InitializeComponent();
    }

    public void ApplyState(
        string maintenanceSummaryText,
        string maintenanceCountsText)
    {
        MaintenanceSummaryTextBlock.Text = maintenanceSummaryText;
        MaintenanceCountsTextBlock.Text = maintenanceCountsText;
    }

    public void ApplyFallbackState(
        string maintenanceSummaryText,
        string maintenanceCountsText)
    {
        ApplyState(maintenanceSummaryText, maintenanceCountsText);
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
