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

    public event EventHandler<FloorConfigSaveRequestedEventArgs>? SaveRequested;

    public event EventHandler? CancelRequested;

    public event EventHandler<FloorConfigSelectionRequestedEventArgs>? PickAlignmentRequested;

    public event EventHandler<FloorConfigSelectionRequestedEventArgs>? PickScopeRequested;

    public LoadedSectionConfig CurrentDocument => _document;

    public FloorConfigPanel()
    {
        InitializeComponent();
        FloorListBox.ItemsSource = _floorSummaries;
        BaseFloorComboBox.ItemsSource = _floors;
        GlobalSlopeCheck.Checked += GlobalSlopeCheck_Changed;
        GlobalSlopeCheck.Unchecked += GlobalSlopeCheck_Changed;
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

    public void LoadDocument(LoadedSectionConfig document)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("FloorConfigPanel 尚未初始化。");
        }

        var selectedFloorName = _currentFloor?.Name;
        _document = document;
        RefreshTemplateOptions();
        LoadConfig(selectedFloorName);
    }

    public void ReloadTemplateOptions()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("FloorConfigPanel 尚未初始化。");
        }

        var currentTopTemplateId = _currentFloor?.TopBoundarySlab.TemplateId ?? string.Empty;
        var currentBottomTemplateId = _currentFloor?.BottomBoundarySlab.TemplateId ?? string.Empty;

        RefreshTemplateOptions();

        if (_currentFloor == null)
        {
            return;
        }

        ApplyBoundaryTemplateSelection(_currentFloor, currentTopTemplateId, currentBottomTemplateId);
        RefreshBoundaryTemplateDisplays(_currentFloor);
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

    private void LoadConfig(string? selectedFloorName = null)
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

        _currentFloor = !string.IsNullOrWhiteSpace(selectedFloorName)
            ? _floors.FirstOrDefault(floor =>
                string.Equals(floor.Name, selectedFloorName, StringComparison.OrdinalIgnoreCase))
            : null;

        if (_currentFloor != null)
        {
            BindFloorToUI(_currentFloor);
            EditPanel.IsEnabled = true;
        }
        else
        {
            EditPanel.IsEnabled = false;
        }

        UpdateSlopeControlAvailability();
        RefreshFloorSummaryList(_currentFloor?.Name);
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

    private IReadOnlyCollection<string> GetAvailableTemplateIds()
        => _slabTemplates
            .Where(option => !string.IsNullOrWhiteSpace(option.TemplateId))
            .Select(option => option.TemplateId)
            .ToArray();

    private string ReadBoundaryTemplateId(ComboBox templateBox, string currentTemplateId)
    {
        var selectedTemplateId = templateBox.SelectedValue?.ToString();
        if (string.IsNullOrWhiteSpace(selectedTemplateId) && templateBox.SelectedItem is TemplateOption selectedOption)
        {
            selectedTemplateId = selectedOption.TemplateId;
        }

        var isExplicitUnboundSelection = templateBox.SelectedItem is TemplateOption option &&
                                         string.IsNullOrWhiteSpace(option.TemplateId);

        return ResolveBoundaryTemplateIdForSave(
            selectedTemplateId,
            isExplicitUnboundSelection,
            currentTemplateId,
            GetAvailableTemplateIds());
    }

    public static string ResolveBoundaryTemplateIdForSave(
        string? selectedTemplateId,
        bool isExplicitUnboundSelection,
        string? currentTemplateId,
        IReadOnlyCollection<string> availableTemplateIds)
    {
        static bool ContainsTemplateId(IReadOnlyCollection<string> ids, string candidate)
            => ids.Any(id => string.Equals(id, candidate, StringComparison.OrdinalIgnoreCase));

        if (isExplicitUnboundSelection)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(selectedTemplateId) &&
            ContainsTemplateId(availableTemplateIds, selectedTemplateId))
        {
            return selectedTemplateId;
        }

        if (!string.IsNullOrWhiteSpace(currentTemplateId) &&
            ContainsTemplateId(availableTemplateIds, currentTemplateId))
        {
            return currentTemplateId;
        }

        return string.Empty;
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

    private bool TrySaveOutputConfig(bool strictNumericParsing, out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(_sectionOutputConfigMapper);

        var existingDto = _sectionOutputConfigMapper.ToDto(_document.OutputConfig);
        if (!TryBuildOutputConfigDto(existingDto, strictNumericParsing, out var dto, out errorMessage))
        {
            return false;
        }

        _sectionOutputConfigMapper.ApplyToDocument(dto, _document);
        return true;
    }

    private bool TryBuildOutputConfigDto(
        SectionOutputConfigDto existingDto,
        bool strictNumericParsing,
        out SectionOutputConfigDto dto,
        out string errorMessage)
    {
        if (!TryBuildHatchStyleDto(
                existingDto.HatchOptions?.WallHatch,
                WallHatchPatternBox,
                WallHatchScaleBox,
                WallHatchAngleBox,
                WallHatchByLayerCheck,
                strictNumericParsing,
                "墙填充",
                out var wallHatch,
                out errorMessage))
        {
            dto = new SectionOutputConfigDto();
            return false;
        }

        if (!TryBuildHatchStyleDto(
                existingDto.HatchOptions?.ColumnHatch,
                ColumnHatchPatternBox,
                ColumnHatchScaleBox,
                ColumnHatchAngleBox,
                ColumnHatchByLayerCheck,
                strictNumericParsing,
                "柱填充",
                out var columnHatch,
                out errorMessage))
        {
            dto = new SectionOutputConfigDto();
            return false;
        }

        if (!TryBuildHatchStyleDto(
                existingDto.HatchOptions?.SlabHatch,
                SlabHatchPatternBox,
                SlabHatchScaleBox,
                SlabHatchAngleBox,
                SlabHatchByLayerCheck,
                strictNumericParsing,
                "楼板填充",
                out var slabHatch,
                out errorMessage))
        {
            dto = new SectionOutputConfigDto();
            return false;
        }

        dto = new SectionOutputConfigDto
        {
            AnnotationOptions = new AnnotationOptionsDto
            {
                GenerateAnnotations = GenerateAnnotationsCheck.IsChecked == true
            },
            HatchOptions = new HatchOptionsDto
            {
                Enabled = EnableHatchCheck.IsChecked == true,
                WallHatch = wallHatch,
                ColumnHatch = columnHatch,
                SlabHatch = slabHatch
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
        errorMessage = string.Empty;
        return true;
    }

    private bool TryBuildHatchStyleDto(
        HatchStyleDto? existingStyle,
        TextBox patternBox,
        TextBox scaleBox,
        TextBox angleBox,
        CheckBox byLayerCheck,
        bool strictNumericParsing,
        string displayName,
        out HatchStyleDto dto,
        out string errorMessage)
    {
        var resolvedStyle = existingStyle ?? new HatchStyleDto();
        var resolvedScale = resolvedStyle.Scale;
        var resolvedAngle = resolvedStyle.Angle;

        if (double.TryParse(scaleBox.Text, out var parsedScale))
        {
            resolvedScale = parsedScale;
        }
        else if (strictNumericParsing)
        {
            dto = new HatchStyleDto();
            errorMessage = $"{displayName}填充比例必须为有效数字。";
            return false;
        }

        if (double.TryParse(angleBox.Text, out var parsedAngle))
        {
            resolvedAngle = parsedAngle;
        }
        else if (strictNumericParsing)
        {
            dto = new HatchStyleDto();
            errorMessage = $"{displayName}填充角度必须为有效数字。";
            return false;
        }

        dto = new HatchStyleDto
        {
            PatternName = patternBox.Text.Trim(),
            Scale = resolvedScale,
            Angle = resolvedAngle,
            UseByLayer = byLayerCheck.IsChecked == true
        };
        errorMessage = string.Empty;
        return true;
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
        TopSlopeCheck.IsChecked = floor.TopBoundarySlab.SlopeEnabled;
        TopSlopeValueBox.Text = (floor.TopBoundarySlab.SlopeValue * 100).ToString("F2");
        BottomSlopeCheck.IsChecked = floor.BottomBoundarySlab.SlopeEnabled;
        BottomSlopeValueBox.Text = (floor.BottomBoundarySlab.SlopeValue * 100).ToString("F2");
        RefreshTemplateOptions();
        ApplyBoundaryTemplateSelection(floor, floor.TopBoundarySlab.TemplateId, floor.BottomBoundarySlab.TemplateId);
        RefreshBoundaryTemplateDisplays(floor);
        UpdateSlopeControlAvailability();

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
        TrySaveCurrentEdits(strictNumericParsing: false, out _);
    }

    private bool TrySaveCurrentEdits(bool strictNumericParsing, out string errorMessage)
    {
        if (_currentFloor == null)
        {
            errorMessage = string.Empty;
            return true;
        }

        _currentFloor.Name = NameBox.Text.Trim();
        if (double.TryParse(HeightBox.Text, out var height))
        {
            _currentFloor.Height = height;
        }
        else if (strictNumericParsing)
        {
            errorMessage = "层高必须为有效数字。";
            return false;
        }

        _currentFloor.HasSlope = SlopeCheck.IsChecked == true;
        if (!_currentFloor.HasSlope || !SlopeValueBox.IsEnabled)
        {
            // Disabled slope editors keep the last committed numeric value.
        }
        else if (double.TryParse(SlopeValueBox.Text, out var legacySlope))
        {
            _currentFloor.SlopeValue = legacySlope / 100.0;
        }
        else if (strictNumericParsing)
        {
            errorMessage = "楼层兼容坡度必须为有效数字。";
            return false;
        }

        _currentFloor.TopBoundarySlab.SlopeEnabled = TopSlopeCheck.IsChecked == true;
        _currentFloor.BottomBoundarySlab.SlopeEnabled = BottomSlopeCheck.IsChecked == true;
        _currentFloor.TopBoundarySlab.TemplateId = ReadBoundaryTemplateId(
            TopBoundaryTemplateBox,
            _currentFloor.TopBoundarySlab.TemplateId);
        _currentFloor.BottomBoundarySlab.TemplateId = ReadBoundaryTemplateId(
            BottomBoundaryTemplateBox,
            _currentFloor.BottomBoundarySlab.TemplateId);
        if (!_currentFloor.TopBoundarySlab.SlopeEnabled || !TopSlopeValueBox.IsEnabled)
        {
            // Disabled slope editors keep the last committed numeric value.
        }
        else if (double.TryParse(TopSlopeValueBox.Text, out var topSlope))
        {
            _currentFloor.TopBoundarySlab.SlopeValue = topSlope / 100.0;
        }
        else if (strictNumericParsing)
        {
            errorMessage = "顶边界板坡度必须为有效数字。";
            return false;
        }

        if (!_currentFloor.BottomBoundarySlab.SlopeEnabled || !BottomSlopeValueBox.IsEnabled)
        {
            // Disabled slope editors keep the last committed numeric value.
        }
        else if (double.TryParse(BottomSlopeValueBox.Text, out var bottomSlope))
        {
            _currentFloor.BottomBoundarySlab.SlopeValue = bottomSlope / 100.0;
        }
        else if (strictNumericParsing)
        {
            errorMessage = "底边界板坡度必须为有效数字。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private void SlopeCheck_Changed(object sender, RoutedEventArgs e)
        => UpdateSlopeControlAvailability();

    private void TopSlopeCheck_Changed(object sender, RoutedEventArgs e)
        => UpdateSlopeControlAvailability();

    private void BottomSlopeCheck_Changed(object sender, RoutedEventArgs e)
        => UpdateSlopeControlAvailability();

    private void GlobalSlopeCheck_Changed(object sender, RoutedEventArgs e)
        => UpdateSlopeControlAvailability();

    private void UpdateSlopeControlAvailability()
    {
        var legacySlopeOverriddenByGlobal = GlobalSlopeCheck.IsChecked == true;
        var topSlopeOverriddenByGlobal = GlobalSlopeCheck.IsChecked == true;
        var bottomSlopeOverriddenByGlobal = _document.Config.GlobalBottomSlopeEnabled;

        ApplySlopeEditorState(
            SlopeCheck,
            SlopeValueBox,
            !legacySlopeOverriddenByGlobal,
            SlopeCheck.IsChecked == true,
            legacySlopeOverriddenByGlobal
                ? "已启用图纸级全局坡度，楼层级兼容坡度暂不生效。关闭全局坡度后可恢复编辑。"
                : null);

        ApplySlopeEditorState(
            TopSlopeCheck,
            TopSlopeValueBox,
            !topSlopeOverriddenByGlobal,
            TopSlopeCheck.IsChecked == true,
            topSlopeOverriddenByGlobal
                ? "已启用图纸级全局坡度，顶边界板坡度暂不生效。关闭全局坡度后可恢复编辑。"
                : null);

        ApplySlopeEditorState(
            BottomSlopeCheck,
            BottomSlopeValueBox,
            !bottomSlopeOverriddenByGlobal,
            BottomSlopeCheck.IsChecked == true,
            bottomSlopeOverriddenByGlobal
                ? "已启用图纸级全局底板坡度，底边界板坡度暂不生效。关闭全局底板坡度后可恢复编辑。"
                : null);
    }

    private static void ApplySlopeEditorState(
        CheckBox checkBox,
        TextBox valueBox,
        bool editorEnabled,
        bool valueEnabledWhenEditorAvailable,
        string? tooltip)
    {
        checkBox.IsEnabled = editorEnabled;
        valueBox.IsEnabled = editorEnabled && valueEnabledWhenEditorAvailable;
        checkBox.ToolTip = tooltip;
        valueBox.ToolTip = tooltip;
    }

    private void TopBoundaryTemplateBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingTemplateSelectors || _currentFloor == null)
        {
            return;
        }

        _currentFloor.TopBoundarySlab.TemplateId = ReadBoundaryTemplateId(
            TopBoundaryTemplateBox,
            _currentFloor.TopBoundarySlab.TemplateId);
        RefreshBoundaryTemplateDisplays(_currentFloor);
    }

    private void BottomBoundaryTemplateBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingTemplateSelectors || _currentFloor == null)
        {
            return;
        }

        _currentFloor.BottomBoundarySlab.TemplateId = ReadBoundaryTemplateId(
            BottomBoundaryTemplateBox,
            _currentFloor.BottomBoundarySlab.TemplateId);
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
        if (!string.IsNullOrWhiteSpace(selectedTemplateId))
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

    private bool TryCommitGlobalConfig(bool strictNumericParsing, out string errorMessage)
    {
        var globalSlopePercent = _document.Config.GlobalSlopeValue * 100.0;
        if (!GlobalSlopeCheck.IsChecked.HasValue || GlobalSlopeCheck.IsChecked == false || !GlobalSlopeValueBox.IsEnabled)
        {
            // Disabled global slope editor keeps the last committed numeric value.
        }
        else if (double.TryParse(GlobalSlopeValueBox.Text, out var parsedGlobalSlopePercent))
        {
            globalSlopePercent = parsedGlobalSlopePercent;
        }
        else if (strictNumericParsing)
        {
            errorMessage = "图纸级全局坡度必须为有效数字。";
            return false;
        }

        var globalSlopeEnabled = GlobalSlopeCheck.IsChecked == true;
        var globalSlopeTarget = (GlobalSlopeTargetBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? _document.Config.GlobalSlopeTarget
            ?? "StructuralSlab";

        _document.Config.AlignmentBaseFloorName = (BaseFloorComboBox.SelectedItem as FloorConfig)?.Name?.Trim() ?? string.Empty;
        _document.Config.GlobalSlopeEnabled = globalSlopeEnabled;
        _document.Config.GlobalSlopeValue = globalSlopePercent / 100.0;
        _document.Config.GlobalSlopeTarget = globalSlopeTarget;
        _document.Config.GlobalTopSlopeEnabled = globalSlopeEnabled;
        _document.Config.GlobalTopSlopeValue = globalSlopePercent / 100.0;
        _document.Config.GlobalTopSlopeTarget = globalSlopeTarget;
        errorMessage = string.Empty;
        return true;
    }

    private bool TryCommitUiStateToDocument(bool strictNumericParsing, out string errorMessage)
    {
        if (!TrySaveCurrentEdits(strictNumericParsing, out errorMessage))
        {
            return false;
        }

        if (!TryCommitGlobalConfig(strictNumericParsing, out errorMessage))
        {
            return false;
        }

        if (!TrySaveOutputConfig(strictNumericParsing, out errorMessage))
        {
            return false;
        }

        _document.Config.Floors = _floors.ToList();
        errorMessage = string.Empty;
        return true;
    }

    private bool TryBuildSaveRequest(
        out SaveFloorConfigDocumentRequestDto request,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(_saveRequestMapper);
        ArgumentNullException.ThrowIfNull(_sectionOutputConfigMapper);

        if (!TryCommitUiStateToDocument(strictNumericParsing: true, out errorMessage))
        {
            request = new SaveFloorConfigDocumentRequestDto();
            return false;
        }

        request = new SaveFloorConfigDocumentRequestDto
        {
            FloorConfig = _saveRequestMapper.ToRequest(_document),
            OutputConfig = _sectionOutputConfigMapper.ToDto(_document.OutputConfig)
        };
        return true;
    }

    private void PickAlignment_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null)
        {
            return;
        }

        SaveFormEdits();
        if (_currentFloor == null)
        {
            return;
        }

        PickAlignmentRequested?.Invoke(this, new FloorConfigSelectionRequestedEventArgs(_currentFloor));
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
        if (_currentFloor == null)
        {
            return;
        }

        PickScopeRequested?.Invoke(this, new FloorConfigSelectionRequestedEventArgs(_currentFloor));
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
        if (!TryBuildSaveRequest(out var saveRequest, out var validationMessage))
        {
            MessageBox.Show(validationMessage, "配置无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _logger.LogInformation("楼层配置编辑完成，待命令层保存，楼层数: {Count}", _document.Config.Floors.Count);
        SaveRequested?.Invoke(this, new FloorConfigSaveRequestedEventArgs(saveRequest));
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogDebug("楼层配置面板请求取消");
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SaveFormEdits()
    {
        TryCommitUiStateToDocument(strictNumericParsing: false, out _);
    }
}

public sealed class FloorConfigSaveRequestedEventArgs : EventArgs
{
    public FloorConfigSaveRequestedEventArgs(SaveFloorConfigDocumentRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    public SaveFloorConfigDocumentRequestDto Request { get; }
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
