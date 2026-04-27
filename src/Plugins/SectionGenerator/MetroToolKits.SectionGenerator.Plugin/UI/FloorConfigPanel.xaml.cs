using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.ViewModels;
using MetroToolKits.SectionGenerator.Contracts.Common;
using MetroToolKits.SectionGenerator.Contracts.Floors;
using MetroToolKits.SectionGenerator.Contracts.Output;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public partial class FloorConfigPanel : UserControl
{
    private readonly ObservableCollection<FloorSummaryDto> _floorSummaries = new();
    private readonly FloorConfigViewModel _viewModel = new();
    private ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    private LoadedSectionConfig _document = new();
    private ISlabAssemblyTemplateCatalog? _slabTemplateCatalog;
    private IFloorConfigDocumentAssembler? _floorConfigDocumentAssembler;
    private IFloorConfigSaveRequestMapper? _saveRequestMapper;
    private ISectionOutputConfigMapper? _sectionOutputConfigMapper;
    private FloorEditViewModel? _currentFloor;
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
        BaseFloorComboBox.ItemsSource = _viewModel.Floors;
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

        var currentTopTemplateId = _currentFloor?.TopBoundaryTemplateId ?? string.Empty;
        var currentBottomTemplateId = _currentFloor?.BottomBoundaryTemplateId ?? string.Empty;

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
        ArgumentNullException.ThrowIfNull(_sectionOutputConfigMapper);
        _viewModel.Load(_document, _sectionOutputConfigMapper, selectedFloorName);

        GlobalSlopeCheck.IsChecked = _viewModel.GlobalSlopeEnabled;
        GlobalSlopeValueBox.Text = _viewModel.GlobalSlopePercent.ToString("F2");
        GlobalSlopeTargetBox.SelectedIndex = _viewModel.GlobalSlopeTarget == "FinishLayer" ? 1 : 0;
        BaseFloorComboBox.SelectedItem = _viewModel.Floors.FirstOrDefault(floor =>
            string.Equals(floor.Name, _viewModel.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase));
        LoadOutputConfig(_viewModel.OutputConfig);

        if (BaseFloorComboBox.SelectedItem == null && _viewModel.Floors.Count == 1)
        {
            BaseFloorComboBox.SelectedItem = _viewModel.Floors[0];
        }

        _currentFloor = _viewModel.SelectedFloor;

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
        _logger.LogDebug("楼层配置面板加载，楼层数: {Count}", _viewModel.Floors.Count);
    }

    private void RefreshFloorSummaryList(string? selectedFloorName = null)
    {
        ArgumentNullException.ThrowIfNull(_floorConfigDocumentAssembler);

        var displayDocument = new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                GlobalSlopeEnabled = _viewModel.GlobalSlopeEnabled,
                GlobalSlopeValue = _viewModel.GlobalSlopePercent / 100.0,
                GlobalSlopeTarget = _viewModel.GlobalSlopeTarget,
                GlobalTopSlopeEnabled = _viewModel.GlobalTopSlopeEnabled,
                GlobalTopSlopeValue = _viewModel.GlobalTopSlopePercent / 100.0,
                GlobalTopSlopeTarget = _viewModel.GlobalTopSlopeTarget,
                GlobalBottomSlopeEnabled = _viewModel.GlobalBottomSlopeEnabled,
                GlobalBottomSlopeValue = _viewModel.GlobalBottomSlopePercent / 100.0,
                GlobalBottomSlopeTarget = _viewModel.GlobalBottomSlopeTarget,
                AlignmentBaseFloorName = (BaseFloorComboBox.SelectedItem as FloorEditViewModel)?.Name
                    ?? _viewModel.AlignmentBaseFloorName,
                Floors = _viewModel.Floors.Select(floor =>
                {
                    floor.ApplyToDomain();
                    return floor.DomainFloor;
                }).ToList()
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

    private void ApplyBoundaryTemplateSelection(FloorEditViewModel floor, string topTemplateId, string bottomTemplateId)
    {
        var normalizedTop = NormalizeTemplateId(topTemplateId, _slabTemplates);
        var normalizedBottom = NormalizeTemplateId(bottomTemplateId, _slabTemplates);

        floor.TopBoundaryTemplateId = normalizedTop;
        floor.BottomBoundaryTemplateId = normalizedBottom;

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

    private void RefreshBoundaryTemplateDisplays(FloorEditViewModel floor)
    {
        var topTemplate = ResolveTemplate(floor.TopBoundaryTemplateId);
        var bottomTemplate = ResolveTemplate(floor.BottomBoundaryTemplateId);

        BottomSlabBox.Text = BuildTemplateDisplayValue(
            floor.BottomBoundaryTemplateId,
            bottomTemplate,
            template => template.CoreRule.Thickness);
        TopSlabBox.Text = BuildTemplateDisplayValue(
            floor.TopBoundaryTemplateId,
            topTemplate,
            template => template.CoreRule.Thickness);
        FinishBox.Text = BuildTemplateDisplayValue(
            floor.TopBoundaryTemplateId,
            topTemplate,
            template => template.TopLayers.Sum(layer => layer.Thickness));
    }

    private void LoadOutputConfig(SectionOutputConfigViewModel outputConfig)
    {
        var dto = outputConfig.ToDto();

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
        var existingDto = _viewModel.OutputConfig.ToDto();
        if (!TryBuildOutputConfigDto(existingDto, strictNumericParsing, out var dto, out errorMessage))
        {
            return false;
        }

        _viewModel.OutputConfig.Load(dto);
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
            var floor = _viewModel.SelectFloor(summary.FloorName);
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
        SaveCurrentEdits();
        var newFloor = _viewModel.AddFloor();
        if (_viewModel.Floors.Count == 1)
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

        SaveCurrentEdits();
        var name = _currentFloor.Name;
        _viewModel.DeleteSelectedFloor();
        if (BaseFloorComboBox.SelectedItem is FloorEditViewModel selectedBase &&
            string.Equals(selectedBase.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            BaseFloorComboBox.SelectedItem = _viewModel.Floors.FirstOrDefault();
        }

        _currentFloor = _viewModel.SelectedFloor;
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

        SaveCurrentEdits();
        _viewModel.SelectFloor(_currentFloor);
        _viewModel.MoveSelectedFloorUp();
        RefreshFloorSummaryList(_currentFloor?.Name);
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var index = FloorListBox.SelectedIndex;
        if (index < 0 || index >= _viewModel.Floors.Count - 1)
        {
            return;
        }

        SaveCurrentEdits();
        _viewModel.SelectFloor(_currentFloor);
        _viewModel.MoveSelectedFloorDown();
        RefreshFloorSummaryList(_currentFloor?.Name);
    }

    private void BindFloorToUI(FloorEditViewModel floor)
    {
        NameBox.Text = floor.Name;
        HeightBox.Text = floor.Height.ToString("F0");
        SlopeCheck.IsChecked = floor.LegacySlopeEnabled;
        SlopeValueBox.Text = floor.LegacySlopePercent.ToString("F2");
        TopSlopeCheck.IsChecked = floor.TopBoundarySlopeEnabled;
        TopSlopeValueBox.Text = floor.TopBoundarySlopePercent.ToString("F2");
        BottomSlopeCheck.IsChecked = floor.BottomBoundarySlopeEnabled;
        BottomSlopeValueBox.Text = floor.BottomBoundarySlopePercent.ToString("F2");
        RefreshTemplateOptions();
        ApplyBoundaryTemplateSelection(floor, floor.TopBoundaryTemplateId, floor.BottomBoundaryTemplateId);
        RefreshBoundaryTemplateDisplays(floor);
        UpdateSlopeControlAvailability();

        var pointCount = floor.AlignmentPoints.Count;
        AlignmentLabel.Content = BaseFloorComboBox.SelectedItem is FloorEditViewModel baseFloor &&
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

        _currentFloor.LegacySlopeEnabled = SlopeCheck.IsChecked == true;
        if (!_currentFloor.LegacySlopeEnabled || !SlopeValueBox.IsEnabled)
        {
            // Disabled slope editors keep the last committed numeric value.
        }
        else if (double.TryParse(SlopeValueBox.Text, out var legacySlope))
        {
            _currentFloor.LegacySlopePercent = legacySlope;
        }
        else if (strictNumericParsing)
        {
            errorMessage = "楼层兼容坡度必须为有效数字。";
            return false;
        }

        _currentFloor.TopBoundarySlopeEnabled = TopSlopeCheck.IsChecked == true;
        _currentFloor.BottomBoundarySlopeEnabled = BottomSlopeCheck.IsChecked == true;
        _currentFloor.TopBoundaryTemplateId = ReadBoundaryTemplateId(
            TopBoundaryTemplateBox,
            _currentFloor.TopBoundaryTemplateId);
        _currentFloor.BottomBoundaryTemplateId = ReadBoundaryTemplateId(
            BottomBoundaryTemplateBox,
            _currentFloor.BottomBoundaryTemplateId);
        if (!_currentFloor.TopBoundarySlopeEnabled || !TopSlopeValueBox.IsEnabled)
        {
            // Disabled slope editors keep the last committed numeric value.
        }
        else if (double.TryParse(TopSlopeValueBox.Text, out var topSlope))
        {
            _currentFloor.TopBoundarySlopePercent = topSlope;
        }
        else if (strictNumericParsing)
        {
            errorMessage = "顶边界板坡度必须为有效数字。";
            return false;
        }

        if (!_currentFloor.BottomBoundarySlopeEnabled || !BottomSlopeValueBox.IsEnabled)
        {
            // Disabled slope editors keep the last committed numeric value.
        }
        else if (double.TryParse(BottomSlopeValueBox.Text, out var bottomSlope))
        {
            _currentFloor.BottomBoundarySlopePercent = bottomSlope;
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
        var bottomSlopeOverriddenByGlobal = _viewModel.GlobalBottomSlopeEnabled;

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

        _currentFloor.TopBoundaryTemplateId = ReadBoundaryTemplateId(
            TopBoundaryTemplateBox,
            _currentFloor.TopBoundaryTemplateId);
        RefreshBoundaryTemplateDisplays(_currentFloor);
    }

    private void BottomBoundaryTemplateBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingTemplateSelectors || _currentFloor == null)
        {
            return;
        }

        _currentFloor.BottomBoundaryTemplateId = ReadBoundaryTemplateId(
            BottomBoundaryTemplateBox,
            _currentFloor.BottomBoundaryTemplateId);
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

        var currentTopTemplateId = _currentFloor.TopBoundaryTemplateId;
        var currentBottomTemplateId = _currentFloor.BottomBoundaryTemplateId;
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
        var globalSlopePercent = _viewModel.GlobalSlopePercent;
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
            ?? _viewModel.GlobalSlopeTarget
            ?? "StructuralSlab";

        _viewModel.AlignmentBaseFloorName = (BaseFloorComboBox.SelectedItem as FloorEditViewModel)?.Name?.Trim() ?? string.Empty;
        _viewModel.GlobalSlopeEnabled = globalSlopeEnabled;
        _viewModel.GlobalSlopePercent = globalSlopePercent;
        _viewModel.GlobalSlopeTarget = globalSlopeTarget;
        _viewModel.GlobalTopSlopeEnabled = globalSlopeEnabled;
        _viewModel.GlobalTopSlopePercent = globalSlopePercent;
        _viewModel.GlobalTopSlopeTarget = globalSlopeTarget;
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

        ArgumentNullException.ThrowIfNull(_sectionOutputConfigMapper);
        _viewModel.CommitToDocument(_sectionOutputConfigMapper);
        _document = _viewModel.Document;
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

        request = _viewModel.BuildSaveRequest(_saveRequestMapper, _sectionOutputConfigMapper);
        _document = _viewModel.Document;
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

        PickAlignmentRequested?.Invoke(this, new FloorConfigSelectionRequestedEventArgs(_currentFloor.DomainFloor));
    }

    private void ClearAlignment_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null)
        {
            return;
        }

        _currentFloor.AlignmentPoints.Clear();
        _currentFloor.ApplyToDomain();
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

        PickScopeRequested?.Invoke(this, new FloorConfigSelectionRequestedEventArgs(_currentFloor.DomainFloor));
    }

    private void ClearScope_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFloor == null)
        {
            return;
        }

        _currentFloor.ScopeBounds = null;
        _currentFloor.ApplyToDomain();
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

        _logger.LogInformation("楼层配置编辑完成，待命令层保存，楼层数: {Count}", _viewModel.Floors.Count);
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
