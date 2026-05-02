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
    private IReadOnlyList<TemplateOption> _slabTemplates = Array.Empty<TemplateOption>();
    private bool _isRefreshingTemplateSelectors;
    private bool _isRefreshingFloorSummaryList;
    private bool _initialized;

    public event EventHandler<FloorConfigSaveRequestedEventArgs>? SaveRequested;

    public event EventHandler? CancelRequested;

    public event EventHandler<FloorConfigSelectionRequestedEventArgs>? PickAlignmentRequested;

    public event EventHandler<FloorConfigSelectionRequestedEventArgs>? PickScopeRequested;

    public LoadedSectionConfig CurrentDocument => _document;

    private FloorEditViewModel? CurrentFloor => _viewModel.SelectedFloor;

    public FloorConfigPanel()
    {
        InitializeComponent();
        FloorListBox.ItemsSource = _floorSummaries;
        BaseFloorComboBox.ItemsSource = _viewModel.Floors;
        GlobalSlopeCheck.Checked += GlobalSlopeCheck_Changed;
        GlobalSlopeCheck.Unchecked += GlobalSlopeCheck_Changed;
        GlobalBottomSlopeCheck.Checked += GlobalBottomSlopeCheck_Changed;
        GlobalBottomSlopeCheck.Unchecked += GlobalBottomSlopeCheck_Changed;
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

        var selectedFloorName = CurrentFloor?.Name;
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

        var currentTopTemplateId = CurrentFloor?.TopBoundaryTemplateId ?? string.Empty;
        var currentBottomTemplateId = CurrentFloor?.BottomBoundaryTemplateId ?? string.Empty;

        RefreshTemplateOptions();

        if (CurrentFloor == null)
        {
            return;
        }

        ApplyBoundaryTemplateSelection(CurrentFloor, currentTopTemplateId, currentBottomTemplateId);
        RefreshBoundaryTemplateDisplays(CurrentFloor);
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

        GlobalSlopeCheck.IsChecked = _viewModel.GlobalTopSlopeEnabled;
        GlobalSlopeValueBox.Text = _viewModel.GlobalTopSlopePercent.ToString("F2");
        SelectSlopeTarget(GlobalSlopeTargetBox, _viewModel.GlobalTopSlopeTarget);
        GlobalBottomSlopeCheck.IsChecked = _viewModel.GlobalBottomSlopeEnabled;
        GlobalBottomSlopeValueBox.Text = _viewModel.GlobalBottomSlopePercent.ToString("F2");
        SelectSlopeTarget(GlobalBottomSlopeTargetBox, _viewModel.GlobalBottomSlopeTarget);
        BaseFloorComboBox.SelectedItem = _viewModel.Floors.FirstOrDefault(floor =>
            string.Equals(floor.Name, _viewModel.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase));
        LoadOutputConfig(_viewModel.OutputConfig);

        if (BaseFloorComboBox.SelectedItem == null && _viewModel.HasSingleFloor)
        {
            BaseFloorComboBox.SelectedItem = _viewModel.Floors[0];
        }

        if (CurrentFloor != null)
        {
            BindFloorToUI(CurrentFloor);
            EditPanel.IsEnabled = true;
        }
        else
        {
            EditPanel.IsEnabled = false;
        }

        UpdateSlopeControlAvailability();
        RefreshFloorSummaryList(CurrentFloor?.Name);
        _logger.LogDebug("楼层配置面板加载，楼层数: {Count}", _viewModel.Floors.Count);
    }

    private void RefreshFloorSummaryList(string? selectedFloorName = null)
    {
        ArgumentNullException.ThrowIfNull(_floorConfigDocumentAssembler);

        _viewModel.AlignmentBaseFloorName = (BaseFloorComboBox.SelectedItem as FloorEditViewModel)?.Name
            ?? _viewModel.AlignmentBaseFloorName;
        var displayDocument = _viewModel.BuildDisplayDocument();

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

            var targetFloorName = selectedFloorName ?? CurrentFloor?.Name;
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
                BindFloorToUI(floor);
                EditPanel.IsEnabled = true;
                RefreshFloorSummaryList(floor.Name);
                return;
            }
        }

        _viewModel.SelectFloor((FloorEditViewModel?)null);
        EditPanel.IsEnabled = false;
    }

    private void AddFloor_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentEdits();
        var newFloor = _viewModel.AddFloor();
        if (_viewModel.HasSingleFloor)
        {
            BaseFloorComboBox.SelectedItem = newFloor;
        }

        RefreshFloorSummaryList(newFloor.Name);
        BindFloorToUI(newFloor);
        EditPanel.IsEnabled = true;
        _logger.LogInformation("添加楼层: {FloorName}", newFloor.Name);
    }

    private void DeleteFloor_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentFloor == null)
        {
            return;
        }

        SaveCurrentEdits();
        var name = CurrentFloor.Name;
        _viewModel.DeleteSelectedFloor();
        if (BaseFloorComboBox.SelectedItem is FloorEditViewModel selectedBase &&
            string.Equals(selectedBase.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            BaseFloorComboBox.SelectedItem = _viewModel.Floors.FirstOrDefault();
        }

        RefreshFloorSummaryList(CurrentFloor?.Name);
        if (CurrentFloor != null)
        {
            BindFloorToUI(CurrentFloor);
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
        _viewModel.SelectFloor(CurrentFloor);
        _viewModel.MoveSelectedFloorUp();
        RefreshFloorSummaryList(CurrentFloor?.Name);
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var index = FloorListBox.SelectedIndex;
        if (index < 0 || index >= _viewModel.Floors.Count - 1)
        {
            return;
        }

        SaveCurrentEdits();
        _viewModel.SelectFloor(CurrentFloor);
        _viewModel.MoveSelectedFloorDown();
        RefreshFloorSummaryList(CurrentFloor?.Name);
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
        if (CurrentFloor == null)
        {
            errorMessage = string.Empty;
            return true;
        }

        CurrentFloor.Name = NameBox.Text.Trim();
        if (double.TryParse(HeightBox.Text, out var height))
        {
            CurrentFloor.Height = height;
        }
        else if (strictNumericParsing)
        {
            errorMessage = "层高必须为有效数字。";
            return false;
        }

        CurrentFloor.LegacySlopeEnabled = SlopeCheck.IsChecked == true;
        if (!CurrentFloor.LegacySlopeEnabled || !SlopeValueBox.IsEnabled)
        {
            // Disabled slope editors keep the last committed numeric value.
        }
        else if (double.TryParse(SlopeValueBox.Text, out var legacySlope))
        {
            CurrentFloor.LegacySlopePercent = legacySlope;
        }
        else if (strictNumericParsing)
        {
            errorMessage = "楼层兼容坡度必须为有效数字。";
            return false;
        }

        CurrentFloor.TopBoundarySlopeEnabled = TopSlopeCheck.IsChecked == true;
        CurrentFloor.BottomBoundarySlopeEnabled = BottomSlopeCheck.IsChecked == true;
        CurrentFloor.TopBoundaryTemplateId = ReadBoundaryTemplateId(
            TopBoundaryTemplateBox,
            CurrentFloor.TopBoundaryTemplateId);
        CurrentFloor.BottomBoundaryTemplateId = ReadBoundaryTemplateId(
            BottomBoundaryTemplateBox,
            CurrentFloor.BottomBoundaryTemplateId);
        if (!CurrentFloor.TopBoundarySlopeEnabled || !TopSlopeValueBox.IsEnabled)
        {
            // Disabled slope editors keep the last committed numeric value.
        }
        else if (double.TryParse(TopSlopeValueBox.Text, out var topSlope))
        {
            CurrentFloor.TopBoundarySlopePercent = topSlope;
        }
        else if (strictNumericParsing)
        {
            errorMessage = "顶边界板坡度必须为有效数字。";
            return false;
        }

        if (!CurrentFloor.BottomBoundarySlopeEnabled || !BottomSlopeValueBox.IsEnabled)
        {
            // Disabled slope editors keep the last committed numeric value.
        }
        else if (double.TryParse(BottomSlopeValueBox.Text, out var bottomSlope))
        {
            CurrentFloor.BottomBoundarySlopePercent = bottomSlope;
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

    private void GlobalBottomSlopeCheck_Changed(object sender, RoutedEventArgs e)
        => UpdateSlopeControlAvailability();

    private void UpdateSlopeControlAvailability()
    {
        var legacySlopeOverriddenByGlobal = GlobalSlopeCheck.IsChecked == true;
        var topSlopeOverriddenByGlobal = GlobalSlopeCheck.IsChecked == true;
        var bottomSlopeOverriddenByGlobal = GlobalBottomSlopeCheck.IsChecked == true;

        ApplyGlobalSlopeEditorState(
            GlobalSlopeValueBox,
            GlobalSlopeTargetBox,
            GlobalSlopeCheck.IsChecked == true);

        ApplyGlobalSlopeEditorState(
            GlobalBottomSlopeValueBox,
            GlobalBottomSlopeTargetBox,
            GlobalBottomSlopeCheck.IsChecked == true);

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

    private static void ApplyGlobalSlopeEditorState(
        TextBox valueBox,
        ComboBox targetBox,
        bool enabled)
    {
        valueBox.IsEnabled = enabled;
        targetBox.IsEnabled = enabled;
    }

    private void TopBoundaryTemplateBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingTemplateSelectors || CurrentFloor == null)
        {
            return;
        }

        CurrentFloor.TopBoundaryTemplateId = ReadBoundaryTemplateId(
            TopBoundaryTemplateBox,
            CurrentFloor.TopBoundaryTemplateId);
        RefreshBoundaryTemplateDisplays(CurrentFloor);
    }

    private void BottomBoundaryTemplateBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingTemplateSelectors || CurrentFloor == null)
        {
            return;
        }

        CurrentFloor.BottomBoundaryTemplateId = ReadBoundaryTemplateId(
            BottomBoundaryTemplateBox,
            CurrentFloor.BottomBoundaryTemplateId);
        RefreshBoundaryTemplateDisplays(CurrentFloor);
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

        if (CurrentFloor == null)
        {
            return;
        }

        var currentTopTemplateId = CurrentFloor.TopBoundaryTemplateId;
        var currentBottomTemplateId = CurrentFloor.BottomBoundaryTemplateId;
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

        ApplyBoundaryTemplateSelection(CurrentFloor, nextTopTemplateId, nextBottomTemplateId);
        RefreshBoundaryTemplateDisplays(CurrentFloor);
    }

    private void BaseFloorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrentFloor != null)
        {
            BindFloorToUI(CurrentFloor);
            RefreshFloorSummaryList(CurrentFloor.Name);
        }
    }

    private bool TryCommitGlobalConfig(bool strictNumericParsing, out string errorMessage)
    {
        var globalTopSlopePercent = _viewModel.GlobalTopSlopePercent;
        if (GlobalSlopeCheck.IsChecked == true && GlobalSlopeValueBox.IsEnabled)
        {
            if (double.TryParse(GlobalSlopeValueBox.Text, out var parsedGlobalSlopePercent))
            {
                globalTopSlopePercent = parsedGlobalSlopePercent;
            }
            else if (strictNumericParsing)
            {
                errorMessage = "图纸级顶板全局坡度必须为有效数字。";
                return false;
            }
        }

        var globalBottomSlopePercent = _viewModel.GlobalBottomSlopePercent;
        if (GlobalBottomSlopeCheck.IsChecked == true && GlobalBottomSlopeValueBox.IsEnabled)
        {
            if (double.TryParse(GlobalBottomSlopeValueBox.Text, out var parsedGlobalBottomSlopePercent))
            {
                globalBottomSlopePercent = parsedGlobalBottomSlopePercent;
            }
            else if (strictNumericParsing)
            {
                errorMessage = "图纸级底板全局坡度必须为有效数字。";
                return false;
            }
        }

        var globalTopSlopeEnabled = GlobalSlopeCheck.IsChecked == true;
        var globalTopSlopeTarget = ReadSlopeTarget(
            GlobalSlopeTargetBox,
            _viewModel.GlobalTopSlopeTarget);
        var globalBottomSlopeEnabled = GlobalBottomSlopeCheck.IsChecked == true;
        var globalBottomSlopeTarget = ReadSlopeTarget(
            GlobalBottomSlopeTargetBox,
            _viewModel.GlobalBottomSlopeTarget);

        _viewModel.ApplyGlobalSettings(
            (BaseFloorComboBox.SelectedItem as FloorEditViewModel)?.Name,
            globalTopSlopeEnabled,
            globalTopSlopePercent,
            globalTopSlopeTarget,
            globalBottomSlopeEnabled,
            globalBottomSlopePercent,
            globalBottomSlopeTarget);
        errorMessage = string.Empty;
        return true;
    }

    private static string ReadSlopeTarget(ComboBox comboBox, string fallbackTarget)
        => (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
           ?? fallbackTarget
           ?? "StructuralSlab";

    private static void SelectSlopeTarget(ComboBox comboBox, string? target)
    {
        var normalizedTarget = string.IsNullOrWhiteSpace(target)
            ? "StructuralSlab"
            : target.Trim();
        var selectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), normalizedTarget, StringComparison.OrdinalIgnoreCase));
        comboBox.SelectedItem = selectedItem ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
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
        if (CurrentFloor == null)
        {
            return;
        }

        SaveFormEdits();
        if (CurrentFloor == null)
        {
            return;
        }

        PickAlignmentRequested?.Invoke(this, new FloorConfigSelectionRequestedEventArgs(CurrentFloor.DomainFloor));
    }

    private void ClearAlignment_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentFloor == null)
        {
            return;
        }

        _viewModel.ClearSelectedFloorAlignment();
        AlignmentStatus.Text = "未设置";
        AlignmentStatus.Foreground = System.Windows.Media.Brushes.Gray;
        RefreshFloorSummaryList(CurrentFloor.Name);
        _logger.LogInformation("清除楼层 {FloorName} 对齐点", CurrentFloor.Name);
    }

    private void PickScope_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentFloor == null)
        {
            return;
        }

        SaveFormEdits();
        if (CurrentFloor == null)
        {
            return;
        }

        PickScopeRequested?.Invoke(this, new FloorConfigSelectionRequestedEventArgs(CurrentFloor.DomainFloor));
    }

    private void ClearScope_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentFloor == null)
        {
            return;
        }

        _viewModel.ClearSelectedFloorScope();
        ScopeStatus.Text = "未设置";
        ScopeStatus.Foreground = System.Windows.Media.Brushes.Gray;
        RefreshFloorSummaryList(CurrentFloor.Name);
        _logger.LogInformation("清除楼层 {FloorName} 整层范围", CurrentFloor.Name);
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
