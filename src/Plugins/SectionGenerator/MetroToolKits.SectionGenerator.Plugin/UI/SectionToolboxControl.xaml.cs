using System.Windows;
using System.Windows.Controls;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// SectionGenerator 工具箱内容
/// </summary>
public partial class SectionToolboxControl : UserControl
{
    private readonly IGenerateSectionPreflightUseCase _preflightUseCase;
    private readonly IWorkbenchSnapshotAssembler _workbenchSnapshotAssembler;
    private const string ReadyMessage = "这里会汇总当前图纸的配置、构件就绪度和剖面状态。";

    public SectionToolboxControl(
        IGenerateSectionPreflightUseCase preflightUseCase,
        IWorkbenchSnapshotAssembler workbenchSnapshotAssembler)
    {
        InitializeComponent();
        _preflightUseCase = preflightUseCase;
        _workbenchSnapshotAssembler = workbenchSnapshotAssembler;
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

        try
        {
            var result = _preflightUseCase.Execute();
            var snapshot = _workbenchSnapshotAssembler.Assemble(result);

            ConfigSummaryText.Text = snapshot.FloorConfig.SummaryText;
            ReadinessSummaryText.Text = snapshot.ElementReadiness.SummaryText;
            SectionsSummaryText.Text = snapshot.ExistingSections.SummaryText;
            RecommendedActionText.Text = snapshot.RecommendedAction.Message;
            PrimaryActionButton.Content = snapshot.RecommendedAction.ButtonText;
            PrimaryActionButton.Tag = snapshot.RecommendedAction.CommandTag;
        }
        catch (Exception ex)
        {
            ConfigSummaryText.Text = "配置：暂时无法读取当前图纸配置状态。";
            ReadinessSummaryText.Text = "构件：暂时无法检查当前图纸的构件就绪度。";
            SectionsSummaryText.Text = $"剖面：状态刷新失败。{ex.Message}";
            RecommendedActionText.Text = "状态刷新失败时，仍可直接进入楼层配置或重新刷新。";
            PrimaryActionButton.Content = "楼层配置";
            PrimaryActionButton.Tag = "FloorConfig";
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
    }
}
