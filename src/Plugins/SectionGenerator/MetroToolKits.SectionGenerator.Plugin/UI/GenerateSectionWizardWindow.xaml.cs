using System.Windows;
using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public partial class GenerateSectionWizardWindow : Window
{
    private readonly ILogger<GenerateSectionWizardWindow> _logger;
    private readonly GenerateSectionWizardState _state;

    public GenerateSectionWizardWindow(
        GenerateSectionWizardState state,
        ILogger<GenerateSectionWizardWindow> logger)
    {
        InitializeComponent();
        _state = state;
        _logger = logger;

        BindFloorOptions();
        LoadStateIntoForm();
        UpdateStepUi();
    }

    public GenerateSectionWizardState State => _state;

    private void BindFloorOptions()
    {
        TargetFloorComboBox.ItemsSource = _state.PreflightResult.ConfigDocument.Config.Floors;
    }

    private void LoadStateIntoForm()
    {
        _state.Normalize();

        ViewDepthBox.Text = _state.ViewDepth.ToString("F0");
        AllFloorsRadio.IsChecked = !_state.GenerateSingleFloor;
        SingleFloorRadio.IsChecked = _state.GenerateSingleFloor;
        TargetFloorComboBox.SelectedItem = _state.PreflightResult.ConfigDocument.Config.Floors
            .FirstOrDefault(floor => string.Equals(floor.Name, _state.TargetFloorName, StringComparison.OrdinalIgnoreCase));
        UseLocalScopeCheckBox.IsChecked = _state.UseLocalScope;
    }

    private bool SaveForm()
    {
        if (!double.TryParse(ViewDepthBox.Text, out var viewDepth) || viewDepth <= 0)
        {
            MessageBox.Show("请输入有效的视图深度。", "参数无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _state.ViewDepth = viewDepth;
        _state.GenerateSingleFloor = SingleFloorRadio.IsChecked == true;
        _state.TargetFloorName = _state.GenerateSingleFloor
            ? (TargetFloorComboBox.SelectedItem as FloorConfig)?.Name
            : null;
        _state.UseLocalScope = UseLocalScopeCheckBox.IsChecked == true;
        _state.CurrentStep = Math.Clamp(_state.CurrentStep, 0, 3);

        if (_state.GenerateSingleFloor && string.IsNullOrWhiteSpace(_state.TargetFloorName))
        {
            MessageBox.Show("请选择目标楼层。", "参数无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private void UpdateStepUi()
    {
        PreflightPanel.Visibility = _state.CurrentStep == 0 ? Visibility.Visible : Visibility.Collapsed;
        ParametersPanel.Visibility = _state.CurrentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        CadSelectionPanel.Visibility = _state.CurrentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        ConfirmPanel.Visibility = _state.CurrentStep == 3 ? Visibility.Visible : Visibility.Collapsed;

        PrevButton.IsEnabled = _state.CurrentStep > 0;
        NextButton.Visibility = _state.CurrentStep < 3 ? Visibility.Visible : Visibility.Collapsed;
        GenerateButton.Visibility = _state.CurrentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        GenerateButton.IsEnabled = _state.CanGenerate;
        StepProgressText.Text = $"步骤 {_state.CurrentStep + 1}/4";

        switch (_state.CurrentStep)
        {
            case 0:
                StepTitleText.Text = "预检";
                StepHintText.Text = "检查当前图纸是否已经补齐生成剖面所需的楼层配置，并提醒现有剖面是否存在需要更新的内容。";
                RenderPreflight();
                break;
            case 1:
                StepTitleText.Text = "生成参数";
                StepHintText.Text = "设置本次剖面生成的运行参数。";
                RenderParameters();
                break;
            case 2:
                StepTitleText.Text = "CAD 选取";
                StepHintText.Text = "选择本次生成所需的剖切线、局部范围和插入点。";
                RenderCadSelection();
                break;
            default:
                StepTitleText.Text = "确认并生成";
                StepHintText.Text = "确认当前预检、参数和 CAD 选取信息后执行生成。";
                RenderConfirmation();
                break;
        }
    }

    private void RenderPreflight()
    {
        var runtimeState = _state.PreflightResult.ConfigDocument.RuntimeState;
        ConfigSourceText.Text = runtimeState.Source switch
        {
            SectionConfigStorageSource.EmbeddedDwg =>
                $"当前图纸 [{runtimeState.DrawingDisplayName}] 使用 DWG 内嵌楼层配置。",
            SectionConfigStorageSource.TransientUnsavedDrawing when runtimeState.IsCurrentDrawingSaved =>
                $"当前图纸 [{runtimeState.DrawingDisplayName}] 正在使用尚未写入 DWG 的临时楼层配置。",
            SectionConfigStorageSource.TransientUnsavedDrawing =>
                $"当前图纸 [{runtimeState.DrawingDisplayName}] 尚未保存，当前配置仅在本次会话有效。",
            _ =>
                $"当前图纸 [{runtimeState.DrawingDisplayName}] 尚未建立图内楼层配置。"
        };

        ConfigStatusText.Text = _state.PreflightResult.CanGenerate
            ? "当前图纸已满足剖面生成的最小配置要求。"
            : "当前图纸仍缺少必配项，需先补齐后才能生成。";

        MissingRequirementsList.ItemsSource = _state.PreflightResult.MissingRequirements
            .Select(item => item.Message)
            .DefaultIfEmpty("无");

        var existingSections = _state.PreflightResult.ExistingSections;
        ExistingSectionSummaryText.Text = string.IsNullOrWhiteSpace(existingSections.ErrorMessage)
            ? $"共 {existingSections.TotalCount} 个剖面：最新 {existingSections.UpToDateCount}，需更新 {existingSections.OutdatedCount}，未知 {existingSections.UnknownCount}，部分检查 {existingSections.PartialCount}。"
            : $"无法检查现有剖面状态：{existingSections.ErrorMessage}";

        ViewUpdatesButton.IsEnabled = existingSections.CheckResult != null;
        PreflightHintText.Text = _state.PreflightResult.CanGenerate
            ? "你可以继续设置本次生成参数；如果已有剖面存在需更新/未知状态，这里只做提醒，不阻止新剖面生成。"
            : "请先通过“配置楼层...”补齐基准层、基准点或整层范围，再继续生成。";
    }

    private void RenderParameters()
    {
        TargetFloorComboBox.IsEnabled = SingleFloorRadio.IsChecked == true;
    }

    private void RenderCadSelection()
    {
        CutLineStatusText.Text = _state.HasCutLine
            ? $"已选择剖切线：Handle={_state.CutLineHandle}，起点=({_state.CutLineStart.X:F0},{_state.CutLineStart.Y:F0})，终点=({_state.CutLineEnd.X:F0},{_state.CutLineEnd.Y:F0})"
            : "未选择剖切线。";

        PickLocalScopeButton.IsEnabled = _state.UseLocalScope;
        LocalScopeStatusText.Text = !_state.UseLocalScope
            ? "未启用局部范围。"
            : _state.LocalScopeBounds.HasValue
                ? $"已选择局部范围：{_state.LocalScopeBounds.Value}"
                : "已启用局部范围，但尚未选择图元。";

        InsertionPointStatusText.Text = _state.HasInsertionPoint
            ? $"已选择插入点：({_state.InsertionPoint.X:F0},{_state.InsertionPoint.Y:F0},{_state.InsertionPoint.Z:F0})"
            : "未选择插入点。";
    }

    private void RenderConfirmation()
    {
        var existingSections = _state.PreflightResult.ExistingSections;
        ConfirmPreflightText.Text =
            $"配置状态：{(_state.PreflightResult.CanGenerate ? "已满足生成要求" : "仍缺少必配项")}\n" +
            $"已有剖面：共 {existingSections.TotalCount} 个，需更新 {existingSections.OutdatedCount} 个，未知 {existingSections.UnknownCount} 个。";

        ConfirmParametersText.Text =
            $"视图深度：{_state.ViewDepth:F0} mm\n" +
            $"生成范围：{(_state.GenerateSingleFloor ? $"单楼层（{_state.TargetFloorName}）" : "全部楼层")}\n" +
            $"局部范围：{(_state.UseLocalScope ? "启用" : "未启用")}";

        ConfirmCadSelectionText.Text =
            $"剖切线：{(_state.HasCutLine ? _state.CutLineHandle : "未选择")}\n" +
            $"局部范围：{(_state.UseLocalScope ? (_state.LocalScopeBounds.HasValue ? _state.LocalScopeBounds.Value.ToString() : "未选择") : "未启用")}\n" +
            $"插入点：{(_state.HasInsertionPoint ? $"({_state.InsertionPoint.X:F0},{_state.InsertionPoint.Y:F0},{_state.InsertionPoint.Z:F0})" : "未选择")}";

        var outputConfig = _state.PreflightResult.ConfigDocument.OutputConfig;
        ConfirmOutputText.Text =
            $"标注与标高：{(outputConfig.AnnotationOptions.GenerateAnnotations ? "生成" : "不生成")}\n" +
            $"剖面填充：{(outputConfig.HatchOptions.Enabled ? "启用" : "关闭")}\n" +
            $"结构图层：{outputConfig.LayerOptions.StructuralLayer}\n" +
            $"装修图层：{outputConfig.LayerOptions.FinishLayer}";
    }

    private void OpenFloorConfig_Click(object sender, RoutedEventArgs e)
        => CloseForAction(GenerateSectionWizardAction.OpenFloorConfig);

    private void ViewUpdates_Click(object sender, RoutedEventArgs e)
        => CloseForAction(GenerateSectionWizardAction.ViewUpdateDetails);

    private void PickCutLine_Click(object sender, RoutedEventArgs e)
        => CloseForAction(GenerateSectionWizardAction.PickCutLine);

    private void PickLocalScope_Click(object sender, RoutedEventArgs e)
        => CloseForAction(GenerateSectionWizardAction.PickLocalScope);

    private void PickInsertionPoint_Click(object sender, RoutedEventArgs e)
        => CloseForAction(GenerateSectionWizardAction.PickInsertionPoint);

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveForm())
        {
            return;
        }

        _state.CurrentStep = Math.Max(0, _state.CurrentStep - 1);
        UpdateStepUi();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveForm())
        {
            return;
        }

        _state.CurrentStep = Math.Min(3, _state.CurrentStep + 1);
        UpdateStepUi();
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveForm())
        {
            return;
        }

        if (!_state.CanGenerate)
        {
            MessageBox.Show("当前向导状态尚不能生成，请先补齐缺失项和 CAD 选取。", "无法生成", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Tag = GenerateSectionWizardAction.Generate;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Tag = GenerateSectionWizardAction.None;
        DialogResult = false;
        Close();
    }

    private void GenerationModeChanged(object sender, RoutedEventArgs e)
    {
        TargetFloorComboBox.IsEnabled = SingleFloorRadio.IsChecked == true;
    }

    private void UseLocalScopeChanged(object sender, RoutedEventArgs e)
    {
        PickLocalScopeButton.IsEnabled = UseLocalScopeCheckBox.IsChecked == true;
    }

    private void CloseForAction(GenerateSectionWizardAction action)
    {
        if (!SaveForm())
        {
            return;
        }

        Tag = action;
        DialogResult = false;
        Close();
    }
}
