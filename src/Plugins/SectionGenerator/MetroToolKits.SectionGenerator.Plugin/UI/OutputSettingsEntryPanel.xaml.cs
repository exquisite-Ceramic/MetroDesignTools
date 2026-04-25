using System.Windows;
using System.Windows.Controls;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 工具箱中的输出设置入口/说明面板。
/// 该面板只负责展示说明文案并转发“前往楼层配置”入口，
/// 不承载真实的输出设置编辑逻辑。
/// </summary>
public partial class OutputSettingsEntryPanel : UserControl
{
    public event EventHandler? NavigateToFloorConfigRequested;

    public OutputSettingsEntryPanel()
    {
        InitializeComponent();
    }

    public void SetHintText(string? text)
    {
        HintTextBlock.Text = string.IsNullOrWhiteSpace(text)
            ? "输出设置仍在“楼层配置”Tab 的“输出设置”区域维护。"
            : text;
    }

    private void GoToFloorConfigButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToFloorConfigRequested?.Invoke(this, EventArgs.Empty);
    }
}
