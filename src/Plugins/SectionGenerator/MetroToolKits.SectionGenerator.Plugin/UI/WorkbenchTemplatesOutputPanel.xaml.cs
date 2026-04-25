using System.Windows;
using System.Windows.Controls;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 工具箱“模板与输出”页的入口展示面板。
/// 该面板只负责显示模板入口按钮与输出设置说明，并转发入口事件，
/// 不承载模板管理或输出设置编辑逻辑。
/// </summary>
public partial class WorkbenchTemplatesOutputPanel : UserControl
{
    private const string GoToFloorConfigAction = "GoToFloorConfigTab";

    public event EventHandler<WorkbenchCommandRequestedEventArgs>? CommandRequested;

    public WorkbenchTemplatesOutputPanel()
    {
        InitializeComponent();
        OutputSettingsEntryPanelHost.NavigateToFloorConfigRequested += OutputSettingsEntryPanelHost_NavigateToFloorConfigRequested;
    }

    private void CommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string commandName)
        {
            return;
        }

        CommandRequested?.Invoke(this, new WorkbenchCommandRequestedEventArgs(commandName));
    }

    private void OutputSettingsEntryPanelHost_NavigateToFloorConfigRequested(object? sender, EventArgs e)
    {
        CommandRequested?.Invoke(this, new WorkbenchCommandRequestedEventArgs(GoToFloorConfigAction));
    }
}
