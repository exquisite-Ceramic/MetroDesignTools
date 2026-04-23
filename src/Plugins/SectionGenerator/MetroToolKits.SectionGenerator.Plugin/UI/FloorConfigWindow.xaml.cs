using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 楼层配置管理窗口。
/// </summary>
public partial class FloorConfigWindow : Window
{
    private readonly ILogger<FloorConfigWindow> _logger;
    private readonly IReadOnlyList<TemplateOption> _slabTemplates;
    private readonly ObservableCollection<FloorConfig> _floors = new();
    private LoadedSectionConfig _document = new();
    private FloorConfig? _currentFloor;

    public LoadedSectionConfig CurrentDocument => _document;

    public FloorConfigWindow(
        LoadedSectionConfig document,
        IReadOnlyList<SlabAssemblyTemplate> slabTemplates,
        ILogger<FloorConfigWindow> logger)
    {
        InitializeComponent();
        _document = document;
        _logger = logger;
        _slabTemplates = BuildTemplateOptions(slabTemplates);

        FloorListBox.ItemsSource = _floors;
        BaseFloorComboBox.ItemsSource = _floors;
        TopBoundaryTemplateBox.ItemsSource = _slabTemplates;
        BottomBoundaryTemplateBox.ItemsSource = _slabTemplates;
        LoadConfig();
    }

    private static IReadOnlyList<TemplateOption> BuildTemplateOptions(IReadOnlyList<SlabAssemblyTemplate> slabTemplates)
    {
        var options = new List<TemplateOption>
        {
            new()
            {
                TemplateId = string.Empty,
                TemplateName = "（不绑定模板，沿用厚度参数）"
            }
        };
        options.AddRange(slabTemplates.Select(template => new TemplateOption
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName
        }));
        return options;
    }

    private void LoadConfig()
    {
        _floors.Clear();
        foreach (var floor in _document.Config.Floors)
        {
            _floors.Add(floor);
        }

        ConfigSourceStatus.Text = BuildConfigSourceStatus(_document.RuntimeState);
        GlobalSlopeCheck.IsChecked = _document.Config.GlobalSlopeEnabled;
        GlobalSlopeValueBox.Text = (_document.Config.GlobalSlopeValue * 100).ToString("F2");
        GlobalSlopeTargetBox.SelectedIndex = _document.Config.GlobalSlopeTarget == "FinishLayer" ? 1 : 0;
        BaseFloorComboBox.SelectedItem = _floors.FirstOrDefault(floor =>
            string.Equals(floor.Name, _document.Config.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase));
        LoadOutputConfig(_document.OutputConfig);

        if (BaseFloorComboBox.SelectedItem == null && _floors.Count == 1)
        {
            BaseFloorComboBox.SelectedItem = _floors[0];
        }

        _logger.LogDebug("楼层配置窗口加载，楼层数: {Count}", _floors.Count);
    }

    private void LoadOutputConfig(SectionOutputConfig outputConfig)
    {
        outputConfig ??= new SectionOutputConfig();
        outputConfig.AnnotationOptions ??= new AnnotationOptions();
        outputConfig.HatchOptions ??= new HatchOptions();
        outputConfig.HatchOptions.WallHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.HatchOptions.ColumnHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.HatchOptions.SlabHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.LayerOptions ??= new LayerOptions();

        GenerateAnnotationsCheck.IsChecked = outputConfig.AnnotationOptions.GenerateAnnotations;
        EnableHatchCheck.IsChecked = outputConfig.HatchOptions.Enabled;

        BindHatchStyle(outputConfig.HatchOptions.WallHatch, WallHatchPatternBox, WallHatchScaleBox, WallHatchAngleBox, WallHatchByLayerCheck);
        BindHatchStyle(outputConfig.HatchOptions.ColumnHatch, ColumnHatchPatternBox, ColumnHatchScaleBox, ColumnHatchAngleBox, ColumnHatchByLayerCheck);
        BindHatchStyle(outputConfig.HatchOptions.SlabHatch, SlabHatchPatternBox, SlabHatchScaleBox, SlabHatchAngleBox, SlabHatchByLayerCheck);

        CutLineLayerBox.Text = outputConfig.LayerOptions.CutLineLayer;
        SightLineLayerBox.Text = outputConfig.LayerOptions.SightLineLayer;
        AnnotationLayerBox.Text = outputConfig.LayerOptions.AnnotationLayer;
        WallHatchLayerBox.Text = outputConfig.LayerOptions.WallHatchLayer;
        ColumnHatchLayerBox.Text = outputConfig.LayerOptions.ColumnHatchLayer;
        SlabHatchLayerBox.Text = outputConfig.LayerOptions.SlabHatchLayer;
        StructuralLayerBox.Text = outputConfig.LayerOptions.StructuralLayer;
        FinishLayerBox.Text = outputConfig.LayerOptions.FinishLayer;
    }

    private static void BindHatchStyle(
        HatchStyleOptions style,
        TextBox patternBox,
        TextBox scaleBox,
        TextBox angleBox,
        CheckBox byLayerCheck)
    {
        patternBox.Text = style.PatternName;
        scaleBox.Text = style.Scale.ToString("F2");
        angleBox.Text = style.Angle.ToString("F2");
        byLayerCheck.IsChecked = style.UseByLayer;
    }

    private void SaveOutputConfig()
    {
        _document.OutputConfig ??= new SectionOutputConfig();
        _document.OutputConfig.AnnotationOptions ??= new AnnotationOptions();
        _document.OutputConfig.HatchOptions ??= new HatchOptions();
        _document.OutputConfig.HatchOptions.WallHatch ??= HatchStyleOptions.CreateDefault();
        _document.OutputConfig.HatchOptions.ColumnHatch ??= HatchStyleOptions.CreateDefault();
        _document.OutputConfig.HatchOptions.SlabHatch ??= HatchStyleOptions.CreateDefault();
        _document.OutputConfig.LayerOptions ??= new LayerOptions();

        _document.OutputConfig.AnnotationOptions.GenerateAnnotations = GenerateAnnotationsCheck.IsChecked == true;
        _document.OutputConfig.HatchOptions.Enabled = EnableHatchCheck.IsChecked == true;

        SaveHatchStyle(_document.OutputConfig.HatchOptions.WallHatch, WallHatchPatternBox, WallHatchScaleBox, WallHatchAngleBox, WallHatchByLayerCheck);
        SaveHatchStyle(_document.OutputConfig.HatchOptions.ColumnHatch, ColumnHatchPatternBox, ColumnHatchScaleBox, ColumnHatchAngleBox, ColumnHatchByLayerCheck);
        SaveHatchStyle(_document.OutputConfig.HatchOptions.SlabHatch, SlabHatchPatternBox, SlabHatchScaleBox, SlabHatchAngleBox, SlabHatchByLayerCheck);

        _document.OutputConfig.LayerOptions.CutLineLayer = CutLineLayerBox.Text.Trim();
        _document.OutputConfig.LayerOptions.SightLineLayer = SightLineLayerBox.Text.Trim();
        _document.OutputConfig.LayerOptions.AnnotationLayer = AnnotationLayerBox.Text.Trim();
        _document.OutputConfig.LayerOptions.WallHatchLayer = WallHatchLayerBox.Text.Trim();
        _document.OutputConfig.LayerOptions.ColumnHatchLayer = ColumnHatchLayerBox.Text.Trim();
        _document.OutputConfig.LayerOptions.SlabHatchLayer = SlabHatchLayerBox.Text.Trim();
        _document.OutputConfig.LayerOptions.StructuralLayer = StructuralLayerBox.Text.Trim();
        _document.OutputConfig.LayerOptions.FinishLayer = FinishLayerBox.Text.Trim();
    }

    private static void SaveHatchStyle(
        HatchStyleOptions style,
        TextBox patternBox,
        TextBox scaleBox,
        TextBox angleBox,
        CheckBox byLayerCheck)
    {
        style.PatternName = patternBox.Text.Trim();
        if (double.TryParse(scaleBox.Text, out var scale))
        {
            style.Scale = scale;
        }

        if (double.TryParse(angleBox.Text, out var angle))
        {
            style.Angle = angle;
        }

        style.UseByLayer = byLayerCheck.IsChecked == true;
    }

    private static string BuildConfigSourceStatus(SectionConfigRuntimeState state)
    {
        return state.Source switch
        {
            SectionConfigStorageSource.EmbeddedDwg =>
                $"当前图纸 [{state.DrawingDisplayName}] 使用 DWG 内嵌楼层配置。",
            SectionConfigStorageSource.TransientUnsavedDrawing when state.IsCurrentDrawingSaved =>
                $"当前图纸 [{state.DrawingDisplayName}] 正在使用尚未写入 DWG 的临时楼层配置，请执行一次保存把配置写入图内。",
            SectionConfigStorageSource.TransientUnsavedDrawing =>
                $"当前图纸 [{state.DrawingDisplayName}] 尚未保存，楼层配置仅在本次会话有效，保存图纸后再保存配置才会写入 DWG。",
            _ when state.IsCurrentDrawingSaved =>
                $"当前图纸 [{state.DrawingDisplayName}] 尚未配置基准点/整层范围，保存后会直接写入该 DWG。",
            _ =>
                $"当前图纸 [{state.DrawingDisplayName}] 尚未建立图内楼层配置。"
        };
    }

    private void FloorListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FloorListBox.SelectedItem is FloorConfig floor)
        {
            SaveCurrentEdits();
            _currentFloor = floor;
            BindFloorToUI(floor);
            EditPanel.IsEnabled = true;
        }
        else
        {
            EditPanel.IsEnabled = false;
        }
    }

    private void AddFloor_Click(object sender, RoutedEventArgs e)
    {
        var newFloor = new FloorConfig
        {
            Name = $"F{_floors.Count + 1}",
            Height = 3000,
            BottomSlabThickness = 800,
            TopSlabThickness = 600,
            FinishThickness = 120,
            TopBoundarySlab = new BoundarySlabConfig
            {
                SlopeEnabled = false,
                SlopeValue = 0,
                SlopeTarget = "StructuralSlab"
            },
            BottomBoundarySlab = new BoundarySlabConfig
            {
                SlopeEnabled = false,
                SlopeValue = 0,
                SlopeTarget = "StructuralSlab"
            }
        };

        _floors.Add(newFloor);
        if (_floors.Count == 1)
        {
            BaseFloorComboBox.SelectedItem = newFloor;
        }

        FloorListBox.SelectedItem = newFloor;
        _logger.LogInformation("添加楼层: {FloorName}", newFloor.Name);
    }

    private void DeleteFloor_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null)
        {
            return;
        }

        var name = _currentFloor.Name;
        _floors.Remove(_currentFloor);
        if (BaseFloorComboBox.SelectedItem is FloorConfig selectedBase &&
            string.Equals(selectedBase.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            BaseFloorComboBox.SelectedItem = _floors.FirstOrDefault();
        }

        _currentFloor = null;
        EditPanel.IsEnabled = false;
        _logger.LogInformation("删除楼层: {FloorName}", name);
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var index = FloorListBox.SelectedIndex;
        if (index <= 0)
        {
            return;
        }

        _floors.Move(index, index - 1);
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var index = FloorListBox.SelectedIndex;
        if (index < 0 || index >= _floors.Count - 1)
        {
            return;
        }

        _floors.Move(index, index + 1);
    }

    private void BindFloorToUI(FloorConfig floor)
    {
        NameBox.Text = floor.Name;
        HeightBox.Text = floor.Height.ToString("F0");
        BottomSlabBox.Text = floor.BottomSlabThickness.ToString("F0");
        TopSlabBox.Text = floor.TopSlabThickness.ToString("F0");
        FinishBox.Text = floor.FinishThickness.ToString("F0");
        SlopeCheck.IsChecked = floor.HasSlope;
        SlopeValueBox.Text = (floor.SlopeValue * 100).ToString("F2");
        SlopeValueBox.IsEnabled = floor.HasSlope;
        TopSlopeCheck.IsChecked = floor.TopBoundarySlab.SlopeEnabled;
        TopSlopeValueBox.Text = (floor.TopBoundarySlab.SlopeValue * 100).ToString("F2");
        TopSlopeValueBox.IsEnabled = floor.TopBoundarySlab.SlopeEnabled;
        BottomSlopeCheck.IsChecked = floor.BottomBoundarySlab.SlopeEnabled;
        BottomSlopeValueBox.Text = (floor.BottomBoundarySlab.SlopeValue * 100).ToString("F2");
        BottomSlopeValueBox.IsEnabled = floor.BottomBoundarySlab.SlopeEnabled;
        TopBoundaryTemplateBox.SelectedValue = floor.TopBoundarySlab.TemplateId;
        BottomBoundaryTemplateBox.SelectedValue = floor.BottomBoundarySlab.TemplateId;

        var pointCount = floor.AlignmentPoints.Count;
        AlignmentLabel.Content = BaseFloorComboBox.SelectedItem is FloorConfig baseFloor &&
                                 string.Equals(baseFloor.Name, floor.Name, StringComparison.OrdinalIgnoreCase)
            ? "基准点:"
            : "本层对齐点:";
        AlignmentStatus.Text = pointCount >= 3 ? "已设置（3点）" : "未设置";
        AlignmentStatus.Foreground = pointCount >= 3
            ? System.Windows.Media.Brushes.Green
            : System.Windows.Media.Brushes.Gray;

        var hasScope = floor.ScopeBounds.HasValue && floor.ScopeBounds.Value.IsValid();
        ScopeStatus.Text = hasScope
            ? $"已设置（{floor.ScopeBounds!.Value.MinX:F0},{floor.ScopeBounds.Value.MinY:F0} ~ {floor.ScopeBounds.Value.MaxX:F0},{floor.ScopeBounds.Value.MaxY:F0}）"
            : "未设置";
        ScopeStatus.Foreground = hasScope
            ? System.Windows.Media.Brushes.Green
            : System.Windows.Media.Brushes.Gray;
    }

    private void SaveCurrentEdits()
    {
        if (_currentFloor == null)
        {
            return;
        }

        _currentFloor.Name = NameBox.Text.Trim();
        if (double.TryParse(HeightBox.Text, out var height))
        {
            _currentFloor.Height = height;
        }

        if (double.TryParse(BottomSlabBox.Text, out var bottomSlab))
        {
            _currentFloor.BottomSlabThickness = bottomSlab;
        }

        if (double.TryParse(TopSlabBox.Text, out var topSlab))
        {
            _currentFloor.TopSlabThickness = topSlab;
        }

        if (double.TryParse(FinishBox.Text, out var finish))
        {
            _currentFloor.FinishThickness = finish;
        }

        _currentFloor.HasSlope = SlopeCheck.IsChecked == true;
        if (double.TryParse(SlopeValueBox.Text, out var legacySlope))
        {
            _currentFloor.SlopeValue = legacySlope / 100.0;
        }

        _currentFloor.TopBoundarySlab.SlopeEnabled = TopSlopeCheck.IsChecked == true;
        _currentFloor.BottomBoundarySlab.SlopeEnabled = BottomSlopeCheck.IsChecked == true;
        _currentFloor.TopBoundarySlab.TemplateId = TopBoundaryTemplateBox.SelectedValue?.ToString() ?? string.Empty;
        _currentFloor.BottomBoundarySlab.TemplateId = BottomBoundaryTemplateBox.SelectedValue?.ToString() ?? string.Empty;
        if (double.TryParse(TopSlopeValueBox.Text, out var topSlope))
        {
            _currentFloor.TopBoundarySlab.SlopeValue = topSlope / 100.0;
        }

        if (double.TryParse(BottomSlopeValueBox.Text, out var bottomSlope))
        {
            _currentFloor.BottomBoundarySlab.SlopeValue = bottomSlope / 100.0;
        }
    }

    private void SlopeCheck_Changed(object sender, RoutedEventArgs e)
        => SlopeValueBox.IsEnabled = SlopeCheck.IsChecked == true;

    private void TopSlopeCheck_Changed(object sender, RoutedEventArgs e)
        => TopSlopeValueBox.IsEnabled = TopSlopeCheck.IsChecked == true;

    private void BottomSlopeCheck_Changed(object sender, RoutedEventArgs e)
        => BottomSlopeValueBox.IsEnabled = BottomSlopeCheck.IsChecked == true;

    private void BaseFloorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_currentFloor != null)
        {
            BindFloorToUI(_currentFloor);
        }
    }

    private void PickAlignment_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null)
        {
            return;
        }

        SaveFormEdits();
        DialogResult = null;
        Tag = ("PickAlignment", _currentFloor);
        Close();
    }

    private void ClearAlignment_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null)
        {
            return;
        }

        _currentFloor.AlignmentPoints.Clear();
        AlignmentStatus.Text = "未设置";
        AlignmentStatus.Foreground = System.Windows.Media.Brushes.Gray;
        _logger.LogInformation("清除楼层 {FloorName} 对齐点", _currentFloor.Name);
    }

    private void PickScope_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null)
        {
            return;
        }

        SaveFormEdits();
        DialogResult = null;
        Tag = ("PickScope", _currentFloor);
        Close();
    }

    private void ClearScope_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null)
        {
            return;
        }

        _currentFloor.ScopeBounds = null;
        ScopeStatus.Text = "未设置";
        ScopeStatus.Foreground = System.Windows.Media.Brushes.Gray;
        _logger.LogInformation("清除楼层 {FloorName} 整层范围", _currentFloor.Name);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveFormEdits();

        if (!ValidateBeforeSave(out var validationMessage))
        {
            MessageBox.Show(validationMessage, "配置无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _logger.LogInformation("楼层配置编辑完成，待命令层保存，楼层数: {Count}", _document.Config.Floors.Count);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogDebug("楼层配置窗口取消");
        DialogResult = false;
        Close();
    }

    private bool ValidateBeforeSave(out string message)
    {
        if (_document.Config.Floors.Count <= 1)
        {
            message = string.Empty;
            return true;
        }

        if (BaseFloorComboBox.SelectedItem is not FloorConfig baseFloor)
        {
            message = "多楼层模式下必须选择基准层。";
            return false;
        }

        if (!FloorSectionLineTransformer.TryValidateAlignmentPoints(baseFloor.AlignmentPoints, out var baseError))
        {
            message = $"基准层 {baseFloor.Name} 的对齐点无效: {baseError}";
            return false;
        }

        if (!baseFloor.ScopeBounds.HasValue)
        {
            message = $"基准层 {baseFloor.Name} 缺少整层范围框。";
            return false;
        }

        if (!baseFloor.ScopeBounds.Value.IsValid())
        {
            message = $"基准层 {baseFloor.Name} 的整层范围框无效。";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void SaveFormEdits()
    {
        SaveCurrentEdits();

        _document.Config.Floors = _floors.ToList();
        _document.Config.GlobalSlopeEnabled = GlobalSlopeCheck.IsChecked == true;
        if (double.TryParse(GlobalSlopeValueBox.Text, out var globalSlope))
        {
            _document.Config.GlobalSlopeValue = globalSlope / 100.0;
        }

        _document.Config.GlobalSlopeTarget = (GlobalSlopeTargetBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                                    ?? "StructuralSlab";
        _document.Config.GlobalTopSlopeEnabled = _document.Config.GlobalSlopeEnabled;
        _document.Config.GlobalTopSlopeValue = _document.Config.GlobalSlopeValue;
        _document.Config.GlobalTopSlopeTarget = _document.Config.GlobalSlopeTarget;
        _document.Config.AlignmentBaseFloorName = (BaseFloorComboBox.SelectedItem as FloorConfig)?.Name ?? string.Empty;
        SaveOutputConfig();
    }
}

internal sealed class TemplateOption
{
    public string TemplateId { get; init; } = string.Empty;

    public string TemplateName { get; init; } = string.Empty;
}
