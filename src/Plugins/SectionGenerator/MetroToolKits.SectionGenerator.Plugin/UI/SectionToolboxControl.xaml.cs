using System.Windows;
using System.Windows.Controls;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// SectionGenerator 工具箱内容
/// </summary>
public partial class SectionToolboxControl : UserControl
{
    private const string ReadyMessage = "可从此面板直接启动常用命令。";

    public SectionToolboxControl()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshStatus();
    }

    private void CommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string commandName)
        {
            return;
        }

        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
        {
            MessageBox.Show("当前没有活动图纸。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        doc.SendStringToExecute($"{commandName} ", true, false, false);
    }

    private void RefreshStatus()
    {
        StatusText.Text = ReadyMessage;
    }
}
