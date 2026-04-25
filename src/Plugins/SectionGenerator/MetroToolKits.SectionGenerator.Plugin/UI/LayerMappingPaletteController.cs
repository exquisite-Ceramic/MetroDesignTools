using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 在 Palette 场景下协调图层映射面板与映射应用、状态刷新。
/// </summary>
public sealed class LayerMappingPaletteController
{
    private readonly ILayerMappingWorkspaceAssembler _workspaceAssembler;
    private readonly ILayerMappingApplyRequestMapper _applyRequestMapper;
    private readonly IWallAssemblyTemplateCatalog _wallTemplateCatalog;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly IElementConversionUseCase _conversionUseCase;
    private readonly ILogger<LayerMappingPaletteController> _logger;

    private LayerMappingPanel? _panel;
    private Document? _boundDocument;
    private bool _panelInitialized;

    public LayerMappingPaletteController(
        ILayerMappingWorkspaceAssembler workspaceAssembler,
        ILayerMappingApplyRequestMapper applyRequestMapper,
        IWallAssemblyTemplateCatalog wallTemplateCatalog,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IElementConversionUseCase conversionUseCase,
        ILogger<LayerMappingPaletteController> logger)
    {
        _workspaceAssembler = workspaceAssembler;
        _applyRequestMapper = applyRequestMapper;
        _wallTemplateCatalog = wallTemplateCatalog;
        _slabTemplateCatalog = slabTemplateCatalog;
        _conversionUseCase = conversionUseCase;
        _logger = logger;
    }

    public event EventHandler? StateChanged;

    public string BoundDocumentDisplayText { get; private set; } = "当前未绑定图纸准备图纸。";

    public void Attach(LayerMappingPanel panel)
    {
        if (_panel != null && !ReferenceEquals(_panel, panel))
        {
            throw new InvalidOperationException("LayerMappingPaletteController 只允许绑定一个 LayerMappingPanel。");
        }

        if (_panel == null)
        {
            _panel = panel;
            _panel.ApplyRequested += Panel_ApplyRequested;
            _panel.CancelRequested += Panel_CancelRequested;
        }

        if (_panelInitialized)
        {
            RaiseStateChanged();
            return;
        }

        BindCurrentDocument();
        _panel.Initialize(
            _workspaceAssembler,
            _applyRequestMapper,
            _wallTemplateCatalog,
            _slabTemplateCatalog);
        _panelInitialized = true;
        RaiseStateChanged();
    }

    public void RefreshBoundDocument()
    {
        EnsurePanelAttached();
        BindCurrentDocument();
        _panel!.ReloadWorkspace();
        RaiseStateChanged();
    }

    private void Panel_ApplyRequested(object? sender, ApplyLayerMappingsRequestedEventArgs e)
    {
        if (!TryEnsureBoundDocument("应用图层映射"))
        {
            return;
        }

        var domainRequest = _applyRequestMapper.ToDomainRequest(e.Request);
        var result = _conversionUseCase.ApplyMappings(domainRequest);

        if (!result.Success)
        {
            MessageBox.Show(result.ErrorMessage ?? "图层映射应用失败。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(
            $"已应用 {result.MappedLayerCount} 个图层映射，共转换 {result.ConvertedCount} 个实体。",
            "完成",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        _panel!.ReloadWorkspace();
        RaiseStateChanged();
    }

    private void Panel_CancelRequested(object? sender, EventArgs e)
    {
        if (!TryEnsureBoundDocument("取消图层映射修改"))
        {
            return;
        }

        _panel!.ReloadWorkspace();
        RaiseStateChanged();
    }

    private void BindCurrentDocument()
    {
        _boundDocument = Application.DocumentManager.MdiActiveDocument;
        BoundDocumentDisplayText = _boundDocument == null
            ? "当前没有活动图纸。"
            : $"当前绑定图纸：{_boundDocument.Name}";
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
            "请切回原图纸或刷新图层映射。",
            "图纸已切换",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }

    private void EnsurePanelAttached()
    {
        if (_panel == null)
        {
            throw new InvalidOperationException("LayerMappingPaletteController 尚未绑定 LayerMappingPanel。");
        }
    }

    private void RaiseStateChanged()
        => StateChanged?.Invoke(this, EventArgs.Empty);
}
