using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MetroToolKits.Foundation.Core.Hosting;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Contracts.Workbench;
using MetroToolKits.SectionGenerator.Core.Sections;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// SectionGenerator 工具箱内容
/// </summary>
public partial class SectionToolboxControl : UserControl
{
    private readonly IGenerateSectionPreflightUseCase _preflightUseCase;
    private readonly IWorkbenchSnapshotAssembler _workbenchSnapshotAssembler;
    private readonly FloorConfigPaletteController _floorConfigPaletteController;
    private readonly LayerMappingPaletteController _layerMappingPaletteController;
    private readonly IWallAssemblyTemplateCatalog _wallTemplateCatalog;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly IWallTemplateCatalogMapper _wallTemplateCatalogMapper;
    private readonly ISlabTemplateCatalogMapper _slabTemplateCatalogMapper;
    private bool _templatePanelsInitialized;
    private const string ReadyMessage = "这里会汇总当前图纸的配置、构件就绪度和剖面状态。";

    public SectionToolboxControl(
        IGenerateSectionPreflightUseCase preflightUseCase,
        IWorkbenchSnapshotAssembler workbenchSnapshotAssembler,
        FloorConfigPaletteController floorConfigPaletteController,
        LayerMappingPaletteController layerMappingPaletteController,
        IWallAssemblyTemplateCatalog wallTemplateCatalog,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IWallTemplateCatalogMapper wallTemplateCatalogMapper,
        ISlabTemplateCatalogMapper slabTemplateCatalogMapper)
    {
        InitializeComponent();
        _preflightUseCase = preflightUseCase;
        _workbenchSnapshotAssembler = workbenchSnapshotAssembler;
        _floorConfigPaletteController = floorConfigPaletteController;
        _layerMappingPaletteController = layerMappingPaletteController;
        _wallTemplateCatalog = wallTemplateCatalog;
        _slabTemplateCatalog = slabTemplateCatalog;
        _wallTemplateCatalogMapper = wallTemplateCatalogMapper;
        _slabTemplateCatalogMapper = slabTemplateCatalogMapper;
        _floorConfigPaletteController.StateChanged += FloorConfigPaletteController_StateChanged;
        _layerMappingPaletteController.StateChanged += LayerMappingPaletteController_StateChanged;
        WorkbenchOverviewPanelHost.RefreshRequested += WorkbenchOverviewPanelHost_RefreshRequested;
        WorkbenchOverviewPanelHost.CommandRequested += WorkbenchOverviewPanelHost_CommandRequested;
        WorkbenchGenerationPanelHost.CommandRequested += WorkbenchGenerationPanelHost_CommandRequested;
        WorkbenchMaintenancePanelHost.CommandRequested += WorkbenchMaintenancePanelHost_CommandRequested;
        OutputSettingsEntryPanelHost.NavigateToFloorConfigRequested += OutputSettingsEntryPanelHost_NavigateToFloorConfigRequested;
        Loaded += SectionToolboxControl_Loaded;
    }

    private void SectionToolboxControl_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
        _floorConfigPaletteController.Attach(FloorConfigPanelHost);
        _layerMappingPaletteController.Attach(LayerMappingPanelHost);
        EnsureTemplatePanelsInitialized();
        ShowTemplateHome();
        UpdateFloorConfigBindingText();
        UpdateLayerMappingBindingText();
    }

    private void CommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string commandName)
        {
            return;
        }

        ExecuteCommand(commandName);
    }

    private void ExecuteCommand(string commandName)
    {
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
        WorkbenchOverviewPanelHost.SetStatusText(ReadyMessage);

        try
        {
            var result = _preflightUseCase.Execute();
            var snapshot = _workbenchSnapshotAssembler.Assemble(result);

            UpdateOverviewTab(snapshot);
            UpdatePreparationTab(snapshot);
            UpdateGenerationTab(snapshot);
            UpdateMaintenanceTab(snapshot);
            UpdateTemplatesTab(snapshot);
        }
        catch (Exception ex)
        {
            ApplyRefreshFailure(ex);
        }
    }

    private void UpdateOverviewTab(SectionWorkbenchSnapshotDto snapshot)
    {
        WorkbenchOverviewPanelHost.ApplySnapshot(snapshot);
    }

    private void UpdatePreparationTab(SectionWorkbenchSnapshotDto snapshot)
    {
        PreparationSummaryText.Text = snapshot.ElementReadiness.SummaryText;
        PreparationCountsText.Text =
            $"可识别构件 {snapshot.ElementReadiness.TotalRecognizableElementCount} 个，其中墙 {snapshot.ElementReadiness.RecognizableWallCount}、柱 {snapshot.ElementReadiness.RecognizableColumnCount}、板 {snapshot.ElementReadiness.RecognizableSlabCount}。";
        PreparationModeText.Text =
            $"模板模式：墙 {snapshot.ElementReadiness.TemplatedWallCount}、板 {snapshot.ElementReadiness.TemplatedSlabCount}。稳定模式：墙 {snapshot.ElementReadiness.LegacyWallCount}、板 {snapshot.ElementReadiness.LegacySlabCount}。";
    }

    private void UpdateGenerationTab(SectionWorkbenchSnapshotDto snapshot)
    {
        WorkbenchGenerationPanelHost.ApplyState(
            snapshot.FloorConfig.CanGenerate
                ? "当前已满足生成条件。"
                : "当前未满足生成条件。",
            snapshot.FloorConfig.SummaryText,
            $"缺失项：{FormatMissingIssues(snapshot.Issues)}");
    }

    private void UpdateMaintenanceTab(SectionWorkbenchSnapshotDto snapshot)
    {
        WorkbenchMaintenancePanelHost.ApplyState(
            snapshot.ExistingSections.SummaryText,
            $"已有剖面 {snapshot.ExistingSections.TotalCount} 个，其中最新 {snapshot.ExistingSections.UpToDateCount}、需更新 {snapshot.ExistingSections.OutdatedCount}、未知 {snapshot.ExistingSections.UnknownCount}、部分检查 {snapshot.ExistingSections.PartialCount}。");
    }

    private void UpdateTemplatesTab(SectionWorkbenchSnapshotDto snapshot)
    {
        var wallTemplateCount = _wallTemplateCatalog.GetAllTemplates().Count;
        var slabTemplateCount = _slabTemplateCatalog.GetAllTemplates().Count;
        TemplatesSummaryText.Text =
            $"当前图纸：{snapshot.Drawing.DrawingDisplayName}。现有墙体模板 {wallTemplateCount} 个，楼板模板 {slabTemplateCount} 个。输出设置仍在“楼层配置”Tab 中维护。";
        OutputSettingsEntryPanelHost.SetHintText("输出设置仍在“楼层配置”Tab 的“输出设置”区域维护。");
    }

    private void ApplyRefreshFailure(Exception ex)
    {
        WorkbenchOverviewPanelHost.ApplyFallbackState(
            "配置：暂时无法读取当前图纸配置状态。",
            "构件：暂时无法检查当前图纸的构件就绪度。",
            $"剖面：状态刷新失败。{ex.Message}",
            "状态刷新失败时，仍可直接进入楼层配置或重新刷新。",
            "楼层配置",
            SectionGeneratorCommandNames.FloorConfig);

        PreparationSummaryText.Text = "准备：暂时无法读取图纸准备状态。";
        PreparationCountsText.Text = "构件计数：暂时无法统计。";
        PreparationModeText.Text = "模板模式/稳定模式数量暂时无法读取。";

        WorkbenchGenerationPanelHost.ApplyFallbackState(
            "生成条件：暂时无法判断。",
            "楼层配置摘要暂时无法读取。",
            "缺失项：暂时无法读取。");

        WorkbenchMaintenancePanelHost.ApplyFallbackState(
            "维护：暂时无法读取剖面状态。",
            "剖面数量统计暂时无法读取。");

        TemplatesSummaryText.Text = "模板与输出入口仍可继续使用。";
        OutputSettingsEntryPanelHost.SetHintText("输出设置入口暂时不可刷新，仍可前往“楼层配置”Tab 继续维护。");
    }

    private static string FormatMissingIssues(IReadOnlyList<ValidationIssueDto> issues)
    {
        if (issues.Count == 0)
        {
            return "无缺失项";
        }

        var messages = issues
            .Select(issue => issue.Message.Trim())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (messages.Count == 0)
        {
            return "无缺失项";
        }

        if (messages.Count <= 3)
        {
            return string.Join("；", messages);
        }

        return $"{string.Join("；", messages.Take(3))}；等 {messages.Count} 项";
    }

    private void FloorConfigPaletteController_StateChanged(object? sender, EventArgs e)
    {
        UpdateFloorConfigBindingText();
        RefreshStatus();
    }

    private void LayerMappingPaletteController_StateChanged(object? sender, EventArgs e)
    {
        UpdateLayerMappingBindingText();
        RefreshStatus();
    }

    private void UpdateFloorConfigBindingText()
    {
        FloorConfigBindingText.Text = _floorConfigPaletteController.BoundDocumentDisplayText;
    }

    private void UpdateLayerMappingBindingText()
    {
        LayerMappingBindingText.Text = _layerMappingPaletteController.BoundDocumentDisplayText;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
    }

    private void WorkbenchOverviewPanelHost_RefreshRequested(object? sender, EventArgs e)
    {
        RefreshStatus();
    }

    private void WorkbenchOverviewPanelHost_CommandRequested(object? sender, WorkbenchCommandRequestedEventArgs e)
    {
        ExecuteCommand(e.CommandName);
    }

    private void WorkbenchGenerationPanelHost_CommandRequested(object? sender, WorkbenchCommandRequestedEventArgs e)
    {
        ExecuteCommand(e.CommandName);
    }

    private void WorkbenchMaintenancePanelHost_CommandRequested(object? sender, WorkbenchCommandRequestedEventArgs e)
    {
        ExecuteCommand(e.CommandName);
    }

    private void RefreshFloorConfigButton_Click(object sender, RoutedEventArgs e)
    {
        _floorConfigPaletteController.RefreshBoundDocument();
        UpdateFloorConfigBindingText();
    }

    private void RefreshLayerMappingButton_Click(object sender, RoutedEventArgs e)
    {
        _layerMappingPaletteController.RefreshBoundDocument();
        UpdateLayerMappingBindingText();
    }

    private void EnterWallTemplatePanelButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureTemplatePanelsInitialized();
        WallTemplatePanelHost.ReloadTemplates();
        ShowWallTemplatePanel();
    }

    private void EnterSlabTemplatePanelButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureTemplatePanelsInitialized();
        SlabTemplatePanelHost.ReloadTemplates();
        ShowSlabTemplatePanel();
    }

    private void OutputSettingsEntryPanelHost_NavigateToFloorConfigRequested(object? sender, EventArgs e)
    {
        WorkbenchTabs.SelectedItem = FloorConfigTabItem;
    }

    private void EnsureTemplatePanelsInitialized()
    {
        if (_templatePanelsInitialized)
        {
            return;
        }

        WallTemplatePanelHost.Initialize(_wallTemplateCatalog, _wallTemplateCatalogMapper);
        SlabTemplatePanelHost.Initialize(_slabTemplateCatalog, _slabTemplateCatalogMapper);
        WallTemplatePanelHost.SaveCompleted += WallTemplatePanelHost_SaveCompleted;
        WallTemplatePanelHost.CancelRequested += WallTemplatePanelHost_CancelRequested;
        SlabTemplatePanelHost.SaveCompleted += SlabTemplatePanelHost_SaveCompleted;
        SlabTemplatePanelHost.CancelRequested += SlabTemplatePanelHost_CancelRequested;
        _templatePanelsInitialized = true;
    }

    private void ShowTemplateHome()
    {
        TemplatesHomeView.Visibility = Visibility.Visible;
        WallTemplatePanelView.Visibility = Visibility.Collapsed;
        SlabTemplatePanelView.Visibility = Visibility.Collapsed;
    }

    private void ShowWallTemplatePanel()
    {
        TemplatesHomeView.Visibility = Visibility.Collapsed;
        WallTemplatePanelView.Visibility = Visibility.Visible;
        SlabTemplatePanelView.Visibility = Visibility.Collapsed;
    }

    private void ShowSlabTemplatePanel()
    {
        TemplatesHomeView.Visibility = Visibility.Collapsed;
        WallTemplatePanelView.Visibility = Visibility.Collapsed;
        SlabTemplatePanelView.Visibility = Visibility.Visible;
    }

    private void RefreshToolboxState()
    {
        RefreshStatus();
    }

    private void RefreshTemplateState(
        bool reloadWallTemplatePanel = false,
        bool reloadSlabTemplatePanel = false,
        bool reloadFloorConfigTemplateOptions = false)
    {
        if (reloadWallTemplatePanel)
        {
            EnsureTemplatePanelsInitialized();
            WallTemplatePanelHost.ReloadTemplates();
        }

        if (reloadSlabTemplatePanel)
        {
            EnsureTemplatePanelsInitialized();
            SlabTemplatePanelHost.ReloadTemplates();
        }

        if (reloadFloorConfigTemplateOptions)
        {
            FloorConfigPanelHost.ReloadTemplateOptions();
        }
    }

    private void WallTemplatePanelHost_SaveCompleted(object? sender, EventArgs e)
    {
        RefreshTemplateState(reloadWallTemplatePanel: true);
        ShowTemplateHome();
        RefreshToolboxState();
    }

    private void WallTemplatePanelHost_CancelRequested(object? sender, EventArgs e)
    {
        ShowTemplateHome();
    }

    private void SlabTemplatePanelHost_SaveCompleted(object? sender, EventArgs e)
    {
        RefreshTemplateState(
            reloadSlabTemplatePanel: true,
            reloadFloorConfigTemplateOptions: true);
        ShowTemplateHome();
        RefreshToolboxState();
    }

    private void SlabTemplatePanelHost_CancelRequested(object? sender, EventArgs e)
    {
        ShowTemplateHome();
    }
}
