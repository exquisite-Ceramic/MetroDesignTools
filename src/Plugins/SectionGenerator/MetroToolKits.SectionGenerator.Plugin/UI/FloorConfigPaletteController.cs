using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.SectionGenerator.Plugin.Commands;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 在 Palette 场景下协调楼层配置面板与 DWG 持久化、原窗口拾取工作流。
/// </summary>
public sealed class FloorConfigPaletteController
{
    private readonly IFloorConfigUseCase _floorConfigUseCase;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly IFloorConfigDocumentAssembler _floorConfigDocumentAssembler;
    private readonly IFloorConfigSaveRequestMapper _floorConfigSaveRequestMapper;
    private readonly ISectionOutputConfigMapper _sectionOutputConfigMapper;
    private readonly IUserLogger _userLogger;
    private readonly ILogger<FloorConfigPaletteController> _logger;

    private FloorConfigPanel? _panel;
    private Document? _boundDocument;
    private bool _panelInitialized;
    private Func<IDisposable>? _cadPickSuspensionFactory;

    public FloorConfigPaletteController(
        IFloorConfigUseCase floorConfigUseCase,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IFloorConfigDocumentAssembler floorConfigDocumentAssembler,
        IFloorConfigSaveRequestMapper floorConfigSaveRequestMapper,
        ISectionOutputConfigMapper sectionOutputConfigMapper,
        IUserLogger userLogger,
        ILogger<FloorConfigPaletteController> logger)
    {
        _floorConfigUseCase = floorConfigUseCase;
        _slabTemplateCatalog = slabTemplateCatalog;
        _floorConfigDocumentAssembler = floorConfigDocumentAssembler;
        _floorConfigSaveRequestMapper = floorConfigSaveRequestMapper;
        _sectionOutputConfigMapper = sectionOutputConfigMapper;
        _userLogger = userLogger;
        _logger = logger;
    }

    public event EventHandler? StateChanged;

    public string BoundDocumentDisplayText { get; private set; } = "当前未绑定楼层配置图纸。";

    public void ConfigureCadPickSuspension(Func<IDisposable> cadPickSuspensionFactory)
    {
        _cadPickSuspensionFactory = cadPickSuspensionFactory;
    }

    public void Attach(FloorConfigPanel panel)
    {
        if (_panel != null && !ReferenceEquals(_panel, panel))
        {
            throw new InvalidOperationException("FloorConfigPaletteController 只允许绑定一个 FloorConfigPanel。");
        }

        if (_panel == null)
        {
            _panel = panel;
            _panel.SaveRequested += Panel_SaveCompleted;
            _panel.CancelRequested += Panel_CancelRequested;
            _panel.PickAlignmentRequested += Panel_PickAlignmentRequested;
            _panel.PickScopeRequested += Panel_PickScopeRequested;
        }

        if (_panelInitialized)
        {
            RaiseStateChanged();
            return;
        }

        var configDocument = LoadCurrentDocument();
        _panel.Initialize(
            configDocument,
            _slabTemplateCatalog,
            _floorConfigDocumentAssembler,
            _floorConfigSaveRequestMapper,
            _sectionOutputConfigMapper,
            _logger);
        _panelInitialized = true;
        RaiseStateChanged();
    }

    public void RefreshBoundDocument()
    {
        EnsurePanelAttached();
        var configDocument = LoadCurrentDocument();
        _panel!.LoadDocument(configDocument);
        RaiseStateChanged();
    }

    private void Panel_SaveCompleted(object? sender, FloorConfigSaveRequestedEventArgs e)
    {
        if (!TryEnsureBoundDocument("保存楼层配置"))
        {
            return;
        }

        var saveResult = _floorConfigUseCase.Save(e.Request);
        if (!saveResult.Success)
        {
            MessageBox.Show(
                saveResult.ErrorMessage ?? "楼层配置保存失败。",
                "保存失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _userLogger.FloorConfigSaved(
            e.Request.FloorConfig.Floors.Count,
            e.Request.FloorConfig.Floors.Select(f => f.Name).ToArray());
        ReloadBoundDocument();
    }

    private void Panel_CancelRequested(object? sender, EventArgs e)
    {
        if (!TryEnsureBoundDocument("取消楼层配置修改"))
        {
            return;
        }

        ReloadBoundDocument();
    }

    private void Panel_PickAlignmentRequested(object? sender, FloorConfigSelectionRequestedEventArgs e)
    {
        if (!TryEnsureBoundDocument("拾取楼层对齐点"))
        {
            return;
        }

        using var paletteScope = _cadPickSuspensionFactory?.Invoke();
        try
        {
            FloorConfigDialogWorkflow.PickAlignmentPoints(
                _panel!.CurrentDocument.Config,
                e.Floor,
                _logger,
                _userLogger);
        }
        finally
        {
            _panel!.LoadDocument(_panel.CurrentDocument);
            RaiseStateChanged();
        }
    }

    private void Panel_PickScopeRequested(object? sender, FloorConfigSelectionRequestedEventArgs e)
    {
        if (!TryEnsureBoundDocument("拾取楼层范围"))
        {
            return;
        }

        using var paletteScope = _cadPickSuspensionFactory?.Invoke();
        try
        {
            FloorConfigDialogWorkflow.PickScopeBounds(e.Floor, _logger);
        }
        finally
        {
            _panel!.LoadDocument(_panel.CurrentDocument);
            RaiseStateChanged();
        }
    }

    private void ReloadBoundDocument()
    {
        EnsurePanelAttached();
        if (!TryEnsureBoundDocument("刷新楼层配置"))
        {
            return;
        }

        var configDocument = _floorConfigUseCase.Load();
        _userLogger.FloorConfigLoaded(
            configDocument.Config.Floors.Count,
            configDocument.Config.Floors.Select(f => f.Name).ToArray());
        UpdateBoundDocument(configDocument);
        _panel!.LoadDocument(configDocument);
        RaiseStateChanged();
    }

    private LoadedSectionConfig LoadCurrentDocument()
    {
        var activeDocument = Application.DocumentManager.MdiActiveDocument;
        _boundDocument = activeDocument;

        var configDocument = _floorConfigUseCase.Load();
        _userLogger.FloorConfigLoaded(
            configDocument.Config.Floors.Count,
            configDocument.Config.Floors.Select(f => f.Name).ToArray());
        UpdateBoundDocument(configDocument);
        return configDocument;
    }

    private void UpdateBoundDocument(LoadedSectionConfig configDocument)
    {
        if (_boundDocument == null)
        {
            BoundDocumentDisplayText = "当前没有活动图纸。";
            return;
        }

        BoundDocumentDisplayText = $"当前绑定图纸：{configDocument.RuntimeState.DrawingDisplayName}";
    }

    private bool TryEnsureBoundDocument(string actionDisplayName)
    {
        var activeDocument = Application.DocumentManager.MdiActiveDocument;
        if (_boundDocument != null && ReferenceEquals(_boundDocument, activeDocument))
        {
            return true;
        }

        _logger.LogWarning(
            "{Action} 被拦截，当前活动图纸与 Palette 绑定图纸不一致。Bound={Bound} Active={Active}",
            actionDisplayName,
            _boundDocument?.Name ?? "None",
            activeDocument?.Name ?? "None");
        MessageBox.Show(
            "请切回原图纸或刷新楼层配置。",
            "图纸已切换",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    private void EnsurePanelAttached()
    {
        if (_panel == null)
        {
            throw new InvalidOperationException("FloorConfigPaletteController 尚未绑定 FloorConfigPanel。");
        }
    }

    private void RaiseStateChanged()
        => StateChanged?.Invoke(this, EventArgs.Empty);
}
