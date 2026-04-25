using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Contracts.Common;
using MetroToolKits.SectionGenerator.Contracts.Floors;
using MetroToolKits.SectionGenerator.Contracts.Output;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public partial class FloorConfigPanel : UserControl
{
    private readonly ObservableCollection<FloorConfig> _floors = new();
    private readonly ObservableCollection<FloorSummaryDto> _floorSummaries = new();
    private ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    private LoadedSectionConfig _document = new();
    private ISlabAssemblyTemplateCatalog? _slabTemplateCatalog;
    private IFloorConfigDocumentAssembler? _floorConfigDocumentAssembler;
    private IFloorConfigSaveRequestMapper? _saveRequestMapper;
    private ISectionOutputConfigMapper? _sectionOutputConfigMapper;
    private FloorConfig? _currentFloor;
    private IReadOnlyList<TemplateOption> _slabTemplates = Array.Empty<TemplateOption>();
    private bool _isRefreshingTemplateSelectors;
    private bool _isRefreshingFloorSummaryList;
    private bool _initialized;

    public event EventHandler? SaveCompleted;

    public event EventHandler? CancelRequested;

    public event EventHandler<FloorConfigSelectionRequestedEventArgs>? PickAlignmentRequested;

    public event EventHandler<FloorConfigSelectionRequestedEventArgs>? PickScopeRequested;

    public LoadedSectionConfig CurrentDocument => _document;

    public FloorConfigPanel()
    {
        InitializeComponent();
        FloorListBox.ItemsSource = _floorSummaries;
        BaseFloorComboBox.ItemsSource = _floors;
    }

    public void Initialize(
        LoadedSectionConfig document,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IFloorConfigDocumentAssembler floorConfigDocumentAssembler,
        IFloorConfigSaveRequestMapper saveRequestMapper,
        ISectionOutputConfigMapper sectionOutputConfigMapper,
        ILogger logger)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("FloorConfigPanel.Initialize(...) 只能调用一次。");
        }

        _initialized = true;
        _document = document;
        _slabTemplateCatalog = slabTemplateCatalog;
        _floorConfigDocumentAssembler = floorConfigDocumentAssembler;
        _saveRequestMapper = saveRequestMapper;
        _sectionOutputConfigMapper = sectionOutputConfigMapper;
        _logger = logger;

        RefreshTemplateOptions();
        LoadConfig();
    }

    private static IReadOnlyList<TemplateOption> BuildTemplateOptions(IReadOnlyList<SlabAssemblyTemplate> slabTemplates)
    {
        var options = new List<TemplateOption>
        {
            new()
            {
                TemplateId = string.Empty,
                TemplateName = "（未绑定模板）"
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

        RefreshFloorSummaryList();
        _logger.LogDebug("楼层配置面板加载，楼层数: {Count}", _floors.Count);
    }

    private void RefreshFloorSummaryList(string? selectedFloorName = null)
    {
        ArgumentNullException.ThrowIfNull(_floorConfigDocumentAssembler);

        var displayDocument = new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                GlobalSlopeEnabled = _document.Config.GlobalSlopeEnabled,
                GlobalSlopeValue = _document.Config.GlobalSlopeValue,
                GlobalSlopeTarget = _document.Config.GlobalSlopeTarget,
                GlobalTopSlopeEnabled = _document.Config.GlobalTopSlopeEnabled,
                GlobalTopSlopeValue = _document.Config.GlobalTopSlopeValue,
                GlobalTopSlopeTarget = _document.Config.GlobalTopSlopeTarget,
                GlobalBottomSlopeEnabled = _document.Config.GlobalBottomSlopeEnabled,
                GlobalBottomSlopeValue = _document.Config.GlobalBottomSlopeValue,
                GlobalBottomSlopeTarget = _document.Config.GlobalBottomSlopeTarget,
                AlignmentBaseFloorName = (BaseFloorComboBox.SelectedItem as FloorConfig)?.Name
                    ?? _document.Config.AlignmentBaseFloorName,
                Floors = _floors.ToList()
            },
            OutputConfig = _document.OutputConfig,
            RuntimeDiagnostics = _document.RuntimeDiagnostics,
            RuntimeState = _document.RuntimeState
        };

        var dto = _floorConfigDocumentAssembler.Assemble(displayDocument);
        ConfigSourceStatus.Text = dto.ConfigSourceStatusText;

        _isRefreshingFloorSummaryList = true;
        try
        {
            _floorSummaries.Clear();
            foreach (var summary in dto.FloorSummaries)
            {
                _floorSummaries.Add(summary);
            }

            var targetFloorName = selectedFloorName ?? _currentFloor?.Name;
            FloorListBox.SelectedItem = targetFloorName == null
                ? null
                : _floorSummaries.FirstOrDefault(summary =>
                    string.Equals(summary.FloorName, targetFloorName, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isRefreshingFloorSummaryList = false;
        }
    }

    private void RefreshTemplateOptions()
    {
        ArgumentNullException.ThrowIfNull(_slabTemplateCatalog);
        _slabTemplates = BuildTemplateOptions(_slabTemplateCatalog.GetAllTemplates());
        TopBoundaryTemplateBox.ItemsSource = _slabTemplates;
        BottomBoundaryTemplateBox.ItemsSource = _slabTemplates;
    }

    private static string NormalizeTemplateId(string? templateId, IReadOnlyList<TemplateOption> options)
    {
        if (!string.IsNullOrWhiteSpace(templateId) &&
            options.Any(option => string.Equals(option.TemplateId, templateId, StringComparison.OrdinalIgnoreCase)))
        {
            return templateId;
        }

        return string.Empty;
    }

    private SlabAssemblyTemplate? ResolveTemplate(string? templateId)
    {
        ArgumentNullException.ThrowIfNull(_slabTemplateCatalog);

        if (string.IsNullOrWhiteSpace(templateId))
        {
            return null;
        }

        return _slabTemplateCatalog.GetAllTemplates()
            .FirstOrDefault(template => string.Equals(template.TemplateId, templateId, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatThicknessValue(double thickness)
        => thickness.ToString("F0");

    private static string BuildTemplateDisplayValue(
        string? templateId,
        SlabAssemblyTemplate? template,
        Func<SlabAssemblyTemplate, double> selector)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return "未绑定模板";
        }

        if (template == null)
        {
            return "模板不存在";
        }

        return FormatThicknessValue(selector(template));
    }

    private void ApplyBoundaryTemplateSelection(FloorConfig floor, string topTemplateId, string bottomTemplateId)
    {
        var normalizedTop = NormalizeTemplateId(topTemplateId, _slabTemplates);
        var normalizedBottom = NormalizeTemplateId(bottomTemplateId, _slabTemplates);

        floor.TopBoundarySlab.TemplateId = normalizedTop;
        floor.BottomBoundarySlab.TemplateId = normalizedBottom;

        _isRefreshingTemplateSelectors = true;
        try
        {
            TopBoundaryTemplateBox.SelectedValue = normalizedTop;
            BottomBoundaryTemplateBox.SelectedValue = normalizedBottom;
        }
        finally
        {
            _isRefreshingTemplateSelectors = false;
        }
    }

    private void RefreshBoundaryTemplateDisplays(FloorConfig floor)
    {
        var topTemplate = ResolveTemplate(floor.TopBoundarySlab.TemplateId);
        var bottomTemplate = ResolveTemplate(floor.BottomBoundarySlab.TemplateId);

        BottomSlabBox.Text = BuildTemplateDisplayValue(
            floor.BottomBoundarySlab.TemplateId,
            bottomTemplate,
            template => template.CoreRule.Thickness);
        TopSlabBox.Text = BuildTemplateDisplayValue(
            floor.TopBoundarySlab.TemplateId,
            topTemplate,
            template => template.CoreRule.Thickness);
        FinishBox.Text = BuildTemplateDisplayValue(
            floor.TopBoundarySlab.TemplateId,
            topTemplate,
            template => template.TopLayers.Sum(layer => layer.Thickness));
    }

    private void LoadOutputConfig(SectionOutputConfig? outputConfig)
    {
        ArgumentNullException.ThrowIfNull(_sectionOutputConfigMapper);
        var dto = _sectionOutputConfigMapper.ToDto(outputConfig);

        GenerateAnnotationsCheck.IsChecked = dto.AnnotationOptions?.GenerateAnnotations == true;
        EnableHatchCheck.IsChecked = dto.HatchOptions?.Enabled == true;

        BindHatchStyle(dto.HatchOptions?.WallHatch, WallHatchPatternBox, WallHatchScaleBox, WallHatchAngleBox, WallHatchByLayerCheck);
        BindHatchStyle(dto.HatchOptions?.ColumnHatch, ColumnHatchPatternBox, ColumnHatchScaleBox, ColumnHatchAngleBox, ColumnHatchByLayerCheck);
        BindHatchStyle(dto.HatchOptions?.SlabHatch, SlabHatchPatternBox, SlabHatchScaleBox, SlabHatchAngleBox, SlabHatchByLayerCheck);

        CutLineLayerBox.Text = dto.LayerOptions?.CutLineLayer ?? string.Empty;
        SightLineLayerBox.Text = dto.LayerOptions?.SightLineLayer ?? string.Empty;
        AnnotationLayerBox.Text = dto.LayerOptions?.AnnotationLayer ?? string.Empty;
        WallHatchLayerBox.Text = dto.LayerOptions?.WallHatchLayer ?? string.Empty;
        ColumnHatchLayerBox.Text = dto.LayerOptions?.ColumnHatchLayer ?? string.Empty;
        SlabHatchLayerBox.Text = dto.LayerOptions?.SlabHatchLayer ?? string.Empty;
        StructuralLayerBox.Text = dto.LayerOptions?.StructuralLayer ?? string.Empty;
        FinishLayerBox.Text = dto.LayerOptions?.FinishLayer ?? string.Empty;
    }

    private static void BindHatchStyle(
        HatchStyleDto? style,
        TextBox patternBox,
        TextBox scaleBox,
        TextBox angleBox,
        CheckBox byLayerCheck)
    {
        var resolvedStyle = style ?? new HatchStyleDto();
        patternBox.Text = resolvedStyle.PatternName;
        scaleBox.Text = resolvedStyle.Scale.ToString("F2");
        angleBox.Text = resolvedStyle.Angle.ToString("F2");
        byLayerCheck.IsChecked = resolvedStyle.UseByLayer;
    }

    private void SaveOutputConfig()
    {
        ArgumentNullException.ThrowIfNull(_sectionOutputConfigMapper);

        var existingDto = _sectionOutputConfigMapper.ToDto(_document.OutputConfig);
        var dto = new SectionOutputConfigDto
        {
            AnnotationOptions = new AnnotationOptionsDto
            {
                GenerateAnnotations = GenerateAnnotationsCheck.IsChecked == true
            },
            HatchOptions = new HatchOptionsDto
            {
                Enabled = EnableHatchCheck.IsChecked == true,
                WallHatch = BuildHatchStyleDto(existingDto.HatchOptions?.WallHatch, WallHatchPatternBox, WallHatchScaleBox, WallHatchAngleBox, WallHatchByLayerCheck),
                ColumnHatch = BuildHatchStyleDto(existingDto.HatchOptions?.ColumnHatch, ColumnHatchPatternBox, ColumnHatchScaleBox, ColumnHatchAngleBox, ColumnHatchByLayerCheck),
                SlabHatch = BuildHatchStyleDto(existingDto.HatchOptions?.SlabHatch, SlabHatchPatternBox, SlabHatchScaleBox, SlabHatchAngleBox, SlabHatchByLayerCheck)
            },
            LayerOptions = new LayerOptionsDto
            {
                CutLineLayer = CutLineLayerBox.Text.Trim(),
                SightLineLayer = SightLineLayerBox.Text.Trim(),
                AnnotationLayer = AnnotationLayerBox.Text.Trim(),
                WallHatchLayer = WallHatchLayerBox.Text.Trim(),
                ColumnHatchLayer = ColumnHatchLayerBox.Text.Trim(),
                SlabHatchLayer = SlabHatchLayerBox.Text.Trim(),
                StructuralLayer = StructuralLayerBox.Text.Trim(),
                FinishLayer = FinishLayerBox.Text.Trim()
            }
        };

        _sectionOutputConfigMapper.ApplyToDocument(dto, _document);
    }

    private static HatchStyleDto BuildHatchStyleDto(
        HatchStyleDto? existingStyle,
        TextBox patternBox,
        TextBox scaleBox,
        TextBox angleBox,
        CheckBox byLayerCheck)
    {
        var resolvedStyle = existingStyle ?? new HatchStyleDto();
        var resolvedScale = resolvedStyle.Scale;
        var resolvedAngle = resolvedStyle.Angle;

        if (double.TryParse(scaleBox.Text, out var parsedScale))
        {
            resolvedScale = parsedScale;
        }

        if (double.TryParse(angleBox.Text, out var parsedAngle))
        {
            resolvedAngle = parsedAngle;
        }

        return new HatchStyleDto
        {
            PatternName = patternBox.Text.Trim(),
            Scale = resolvedScale,
            Angle = resolvedAngle,
            UseByLayer = byLayerCheck.IsChecked == true
        };
    }

    private void FloorListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingFloorSummaryList)
        {
            return;
        }

        if (FloorListBox.SelectedItem is FloorSummaryDto summary)
        {
            SaveCurrentEdits();
            var floor = _floors.FirstOrDefault(item =>
                string.Equals(item.Name, summary.FloorName, StringComparison.OrdinalIgnoreCase));
            if (floor != null)
            {
                _currentFloor = floor;
                BindFloorToUI(floor);
                EditPanel.IsEnabled = true;
                RefreshFloorSummaryList(floor.Name);
                return;
            }
        }

        _currentFloor = null;
        EditPanel.IsEnabled = false;
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

        _currentFloor = newFloor;
        RefreshFloorSummaryList(newFloor.Name);
        BindFloorToUI(newFloor);
        EditPanel.IsEnabled = true;
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

        _currentFloor = _floors.FirstOrDefault();
        RefreshFloorSummaryList(_currentFloor?.Name);
        if (_currentFloor != null)
        {
            BindFloorToUI(_currentFloor);
            EditPanel.IsEnabled = true;
        }
        else
        {
            EditPanel.IsEnabled = false;
        }
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
        RefreshFloorSummaryList(_currentFloor?.Name);
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var index = FloorListBox.SelectedIndex;
        if (index < 0 || index >= _floors.Count - 1)
        {
            return;
        }

        _floors.Move(index, index + 1);
        RefreshFloorSummaryList(_currentFloor?.Name);
    }

    private void BindFloorToUI(FloorConfig floor)
    {
        NameBox.Text = floor.Name;
        HeightBox.Text = floor.Height.ToString("F0");
        SlopeCheck.IsChecked = floor.HasSlope;
        SlopeValueBox.Text = (floor.SlopeValue * 100).ToString("F2");
        SlopeValueBox.IsEnabled = floor.HasSlope;
        TopSlopeCheck.IsChecked = floor.TopBoundarySlab.SlopeEnabled;
        TopSlopeValueBox.Text = (floor.TopBoundarySlab.SlopeValue * 100).ToString("F2");
        TopSlopeValueBox.IsEnabled = floor.TopBoundarySlab.SlopeEnabled;
        BottomSlopeCheck.IsChecked = floor.BottomBoundarySlab.SlopeEnabled;
        BottomSlopeValueBox.Text = (floor.BottomBoundarySlab.SlopeValue * 100).ToString("F2");
        BottomSlopeValueBox.IsEnabled = floor.BottomBoundarySlab.SlopeEnabled;
        RefreshTemplateOptions();
        ApplyBoundaryTemplateSelection(floor, floor.TopBoundarySlab.TemplateId, floor.BottomBoundarySlab.TemplateId);
        RefreshBoundaryTemplateDisplays(floor);

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

    private void TopBoundaryTemplateBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingTemplateSelectors || _currentFloor == null)
        {
            return;
        }

        _currentFloor.TopBoundarySlab.TemplateId = TopBoundaryTemplateBox.SelectedValue?.ToString() ?? string.Empty;
        RefreshBoundaryTemplateDisplays(_currentFloor);
    }

    private void BottomBoundaryTemplateBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingTemplateSelectors || _currentFloor == null)
        {
            return;
        }

        _currentFloor.BottomBoundarySlab.TemplateId = BottomBoundaryTemplateBox.SelectedValue?.ToString() ?? string.Empty;
        RefreshBoundaryTemplateDisplays(_currentFloor);
    }

    private void EditBottomTemplate_Click(object sender, RoutedEventArgs e)
        => OpenTemplateManager(SlabTemplateLaunchContext.BottomThickness);

    private void EditTopTemplate_Click(object sender, RoutedEventArgs e)
        => OpenTemplateManager(SlabTemplateLaunchContext.TopThickness);

    private void EditFinishTemplate_Click(object sender, RoutedEventArgs e)
        => OpenTemplateManager(SlabTemplateLaunchContext.Finish);

    private void OpenTemplateManager(SlabTemplateLaunchContext context)
    {
        ArgumentNullException.ThrowIfNull(_slabTemplateCatalog);

        if (_currentFloor == null)
        {
            return;
        }

        var existingTemplateIds = _slabTemplateCatalog.GetAllTemplates()
            .Select(template => template.TemplateId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentTopTemplateId = _currentFloor.TopBoundarySlab.TemplateId;
        var currentBottomTemplateId = _currentFloor.BottomBoundarySlab.TemplateId;
        var manager = new SlabAssemblyTemplateManager(_slabTemplateCatalog);
        var owner = Window.GetWindow(this);
        if (owner != null)
        {
            manager.Owner = owner;
        }

        if (manager.ShowDialog() != true)
        {
            return;
        }

        RefreshTemplateOptions();

        var selectedTemplateId = manager.SelectedTemplateId;
        var nextTopTemplateId = currentTopTemplateId;
        var nextBottomTemplateId = currentBottomTemplateId;
        var selectedTemplateIsNew = !string.IsNullOrWhiteSpace(selectedTemplateId) &&
                                    !existingTemplateIds.Contains(selectedTemplateId);

        if (selectedTemplateIsNew)
        {
            if (context == SlabTemplateLaunchContext.BottomThickness)
            {
                nextBottomTemplateId = selectedTemplateId;
            }
            else
            {
                nextTopTemplateId = selectedTemplateId;
            }
        }

        ApplyBoundaryTemplateSelection(_currentFloor, nextTopTemplateId, nextBottomTemplateId);
        RefreshBoundaryTemplateDisplays(_currentFloor);
    }

    private void BaseFloorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_currentFloor != null)
        {
            BindFloorToUI(_currentFloor);
            RefreshFloorSummaryList(_currentFloor.Name);
        }
    }

    private SaveFloorConfigRequestDto BuildSaveRequestFromUi()
    {
        SaveCurrentEdits();

        var globalSlopePercent = _document.Config.GlobalSlopeValue * 100.0;
        if (double.TryParse(GlobalSlopeValueBox.Text, out var parsedGlobalSlopePercent))
        {
            globalSlopePercent = parsedGlobalSlopePercent;
        }

        var globalSlopeEnabled = GlobalSlopeCheck.IsChecked == true;
        var globalSlopeTarget = (GlobalSlopeTargetBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? _document.Config.GlobalSlopeTarget
            ?? "StructuralSlab";

        return new SaveFloorConfigRequestDto
        {
            AlignmentBaseFloorName = (BaseFloorComboBox.SelectedItem as FloorConfig)?.Name ?? string.Empty,
            GlobalSlopeEnabled = globalSlopeEnabled,
            GlobalSlopePercent = globalSlopePercent,
            GlobalSlopeTarget = globalSlopeTarget,
            GlobalTopSlopeEnabled = globalSlopeEnabled,
            GlobalTopSlopePercent = globalSlopePercent,
            GlobalTopSlopeTarget = globalSlopeTarget,
            GlobalBottomSlopeEnabled = _document.Config.GlobalBottomSlopeEnabled,
            GlobalBottomSlopePercent = _document.Config.GlobalBottomSlopeValue * 100.0,
            GlobalBottomSlopeTarget = _document.Config.GlobalBottomSlopeTarget,
            Floors = _floors.Select(floor => new FloorConfigEditDto
            {
                Name = floor.Name,
                Height = floor.Height,
                FinishThickness = floor.FinishThickness,
                BottomSlabThickness = floor.BottomSlabThickness,
                TopSlabThickness = floor.TopSlabThickness,
                LegacySlopeEnabled = floor.HasSlope,
                LegacySlopePercent = floor.SlopeValue * 100.0,
                LegacySlopeTarget = floor.SlopeTarget,
                AlignmentPoints = floor.AlignmentPoints
                    .Select(point => new Point3DDto
                    {
                        X = point.X,
                        Y = point.Y,
                        Z = point.Z
                    })
                    .ToArray(),
                ScopeBounds = floor.ScopeBounds.HasValue
                    ? new ScopeBoundsDto
                    {
                        MinX = floor.ScopeBounds.Value.MinX,
                        MinY = floor.ScopeBounds.Value.MinY,
                        MaxX = floor.ScopeBounds.Value.MaxX,
                        MaxY = floor.ScopeBounds.Value.MaxY
                    }
                    : null,
                TopBoundarySlab = new BoundarySlabEditDto
                {
                    TemplateId = floor.TopBoundarySlab.TemplateId,
                    SlopeEnabled = floor.TopBoundarySlab.SlopeEnabled,
                    SlopePercent = floor.TopBoundarySlab.SlopeValue * 100.0,
                    SlopeTarget = floor.TopBoundarySlab.SlopeTarget
                },
                BottomBoundarySlab = new BoundarySlabEditDto
                {
                    TemplateId = floor.BottomBoundarySlab.TemplateId,
                    SlopeEnabled = floor.BottomBoundarySlab.SlopeEnabled,
                    SlopePercent = floor.BottomBoundarySlab.SlopeValue * 100.0,
                    SlopeTarget = floor.BottomBoundarySlab.SlopeTarget
                }
            }).ToArray()
        };
    }

    private void SyncEditingFloorsFromDocument(string? currentFloorName, int currentFloorIndex)
    {
        _floors.Clear();
        foreach (var floor in _document.Config.Floors)
        {
            _floors.Add(floor);
        }

        _currentFloor = null;
        BaseFloorComboBox.SelectedItem = _floors.FirstOrDefault(floor =>
            string.Equals(floor.Name, _document.Config.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase));
        if (BaseFloorComboBox.SelectedItem == null && _floors.Count == 1)
        {
            BaseFloorComboBox.SelectedItem = _floors[0];
        }

        var resolvedFloor = !string.IsNullOrWhiteSpace(currentFloorName)
            ? _floors.FirstOrDefault(floor =>
                string.Equals(floor.Name, currentFloorName, StringComparison.OrdinalIgnoreCase))
            : null;

        if (resolvedFloor == null && currentFloorIndex >= 0 && currentFloorIndex < _floors.Count)
        {
            resolvedFloor = _floors[currentFloorIndex];
        }

        _currentFloor = resolvedFloor ?? _floors.FirstOrDefault();
        if (_currentFloor != null)
        {
            BindFloorToUI(_currentFloor);
            EditPanel.IsEnabled = true;
        }
        else
        {
            EditPanel.IsEnabled = false;
        }

        RefreshFloorSummaryList(_currentFloor?.Name);
    }

    private FloorConfig? ResolveCurrentDocumentFloor()
    {
        if (_currentFloor == null)
        {
            return null;
        }

        var currentIndex = _floors.IndexOf(_currentFloor);
        if (currentIndex >= 0 && currentIndex < _document.Config.Floors.Count)
        {
            return _document.Config.Floors[currentIndex];
        }

        return _document.Config.Floors.FirstOrDefault(floor =>
            string.Equals(floor.Name, _currentFloor.Name, StringComparison.OrdinalIgnoreCase));
    }

    private void PickAlignment_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null)
        {
            return;
        }

        SaveFormEdits();
        var floor = ResolveCurrentDocumentFloor();
        if (floor == null)
        {
            return;
        }

        PickAlignmentRequested?.Invoke(this, new FloorConfigSelectionRequestedEventArgs(floor));
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
        RefreshFloorSummaryList(_currentFloor.Name);
        _logger.LogInformation("清除楼层 {FloorName} 对齐点", _currentFloor.Name);
    }

    private void PickScope_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null)
        {
            return;
        }

        SaveFormEdits();
        var floor = ResolveCurrentDocumentFloor();
        if (floor == null)
        {
            return;
        }

        PickScopeRequested?.Invoke(this, new FloorConfigSelectionRequestedEventArgs(floor));
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
        RefreshFloorSummaryList(_currentFloor.Name);
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
        SaveCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogDebug("楼层配置面板请求取消");
        CancelRequested?.Invoke(this, EventArgs.Empty);
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
        ArgumentNullException.ThrowIfNull(_saveRequestMapper);

        var currentFloorIndex = _currentFloor == null ? -1 : _floors.IndexOf(_currentFloor);
        var request = BuildSaveRequestFromUi();
        var savedFloorName = currentFloorIndex >= 0 && currentFloorIndex < request.Floors.Count
            ? request.Floors[currentFloorIndex].Name
            : _currentFloor?.Name;

        _saveRequestMapper.ApplyToDocument(request, _document);
        SyncEditingFloorsFromDocument(savedFloorName, currentFloorIndex);
        SaveOutputConfig();
    }
}

public sealed class FloorConfigSelectionRequestedEventArgs : EventArgs
{
    public FloorConfigSelectionRequestedEventArgs(FloorConfig floor)
    {
        Floor = floor;
    }

    public FloorConfig Floor { get; }
}

internal sealed class TemplateOption
{
    public string TemplateId { get; init; } = string.Empty;

    public string TemplateName { get; init; } = string.Empty;
}

internal enum SlabTemplateLaunchContext
{
    BottomThickness,
    TopThickness,
    Finish
}
