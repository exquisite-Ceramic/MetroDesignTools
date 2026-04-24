using System.Windows;
using System.Windows.Controls;
using MetroToolKits.SectionGenerator.App.UseCases;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// SectionGenerator 工具箱内容
/// </summary>
public partial class SectionToolboxControl : UserControl
{
    private readonly IGenerateSectionPreflightUseCase _preflightUseCase;
    private const string ReadyMessage = "这里会汇总当前图纸的配置、构件就绪度和剖面状态。";

    public SectionToolboxControl(IGenerateSectionPreflightUseCase preflightUseCase)
    {
        InitializeComponent();
        _preflightUseCase = preflightUseCase;
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
            var runtimeState = result.ConfigDocument.RuntimeState;
            var readiness = result.Readiness;
            var existingSections = result.ExistingSections;

            ConfigSummaryText.Text = result.CanGenerate
                ? $"配置：当前图纸 [{runtimeState.DrawingDisplayName}] 已满足最小生成要求。"
                : $"配置：当前图纸 [{runtimeState.DrawingDisplayName}] 仍缺少 {result.MissingRequirements.Count} 项必配内容。";

            ReadinessSummaryText.Text = readiness.HasRecognizableElements
                ? $"构件：已发现 {readiness.TotalRecognizableElementCount} 个可识别墙/柱/板（墙 {readiness.RecognizableWallCount}，柱 {readiness.RecognizableColumnCount}，板 {readiness.RecognizableSlabCount}）。模板模式：墙 {readiness.TemplatedWallCount}，板 {readiness.TemplatedSlabCount}；稳定模式：墙 {readiness.LegacyWallCount}，板 {readiness.LegacySlabCount}。"
                : "构件：当前图纸中尚未发现可识别的墙/柱/板。若图纸仍是原始图层，请先执行图层映射或区域转换。";

            SectionsSummaryText.Text = string.IsNullOrWhiteSpace(existingSections.ErrorMessage)
                ? $"剖面：共 {existingSections.TotalCount} 个，需更新 {existingSections.OutdatedCount} 个，未知 {existingSections.UnknownCount} 个，部分检查 {existingSections.PartialCount} 个。"
                : $"剖面：暂时无法检查已有剖面状态。{existingSections.ErrorMessage}";

            if (!result.CanGenerate)
            {
                PrimaryActionButton.Content = "补齐楼层配置";
                PrimaryActionButton.Tag = "FloorConfig";
                RecommendedActionText.Text = "当前图纸还缺少楼层基准层、基准点或整层范围等必配项，建议先补齐楼层配置。";
                return;
            }

            if (!readiness.HasRecognizableElements)
            {
                PrimaryActionButton.Content = "去做图层映射";
                PrimaryActionButton.Tag = "LayerMapping";
                RecommendedActionText.Text = "当前图纸还没有可识别构件，建议先完成图层映射或区域转换，再开始生成剖面。";
                return;
            }

            if (existingSections.OutdatedCount > 0 || existingSections.UnknownCount > 0)
            {
                PrimaryActionButton.Content = "开始生成剖面";
                PrimaryActionButton.Tag = "GenSection";
                RecommendedActionText.Text = "当前图纸已可生成新剖面，但已有剖面里存在需更新或未知项，建议生成前先留意更新状态。";
                return;
            }

            PrimaryActionButton.Content = "开始生成剖面";
            PrimaryActionButton.Tag = "GenSection";
            RecommendedActionText.Text = "当前图纸已满足生成条件，可以直接开始生成剖面。";
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
