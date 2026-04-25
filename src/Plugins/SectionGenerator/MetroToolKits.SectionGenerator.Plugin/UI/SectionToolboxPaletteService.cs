using System.Drawing;
using Autodesk.AutoCAD.Windows;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 工具箱 PaletteSet 管理器
/// </summary>
public sealed class SectionToolboxPaletteService
{
    private readonly IGenerateSectionPreflightUseCase _preflightUseCase;
    private readonly IWorkbenchSnapshotAssembler _workbenchSnapshotAssembler;
    private readonly FloorConfigPaletteController _floorConfigPaletteController;
    private readonly LayerMappingPaletteController _layerMappingPaletteController;
    private readonly IWallAssemblyTemplateCatalog _wallTemplateCatalog;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly IWallTemplateCatalogMapper _wallTemplateCatalogMapper;
    private readonly ISlabTemplateCatalogMapper _slabTemplateCatalogMapper;
    private PaletteSet? _paletteSet;
    private int _cadPickSuspendCount;
    private bool _restoreVisibleAfterCadPick;

    public SectionToolboxPaletteService(
        IGenerateSectionPreflightUseCase preflightUseCase,
        IWorkbenchSnapshotAssembler workbenchSnapshotAssembler,
        FloorConfigPaletteController floorConfigPaletteController,
        LayerMappingPaletteController layerMappingPaletteController,
        IWallAssemblyTemplateCatalog wallTemplateCatalog,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IWallTemplateCatalogMapper wallTemplateCatalogMapper,
        ISlabTemplateCatalogMapper slabTemplateCatalogMapper)
    {
        _preflightUseCase = preflightUseCase;
        _workbenchSnapshotAssembler = workbenchSnapshotAssembler;
        _floorConfigPaletteController = floorConfigPaletteController;
        _layerMappingPaletteController = layerMappingPaletteController;
        _wallTemplateCatalog = wallTemplateCatalog;
        _slabTemplateCatalog = slabTemplateCatalog;
        _wallTemplateCatalogMapper = wallTemplateCatalogMapper;
        _slabTemplateCatalogMapper = slabTemplateCatalogMapper;
        _floorConfigPaletteController.ConfigureCadPickSuspension(SuspendForCadPick);
    }

    public void Show()
    {
        _paletteSet ??= CreatePaletteSet();
        _paletteSet.Visible = true;
    }

    public IDisposable SuspendForCadPick()
    {
        if (_paletteSet == null)
        {
            return NoOpScope.Instance;
        }

        if (_cadPickSuspendCount == 0)
        {
            _restoreVisibleAfterCadPick = _paletteSet.Visible;
            if (_restoreVisibleAfterCadPick)
            {
                _paletteSet.Visible = false;
            }
        }

        _cadPickSuspendCount++;
        return new CadPickScope(this);
    }

    private PaletteSet CreatePaletteSet()
    {
        var paletteSet = new PaletteSet("MetroToolKits 工具箱")
        {
            MinimumSize = new Size(980, 860),
            Size = new Size(980, 860),
            DockEnabled = DockSides.Left | DockSides.Right
        };

        paletteSet.Style =
            PaletteSetStyles.ShowAutoHideButton |
            PaletteSetStyles.ShowCloseButton |
            PaletteSetStyles.ShowPropertiesMenu;
        paletteSet.AddVisual(
            "SectionGenerator",
            new SectionToolboxControl(
                _preflightUseCase,
                _workbenchSnapshotAssembler,
                _floorConfigPaletteController,
                _layerMappingPaletteController,
                _wallTemplateCatalog,
                _slabTemplateCatalog,
                _wallTemplateCatalogMapper,
                _slabTemplateCatalogMapper));
        return paletteSet;
    }

    private void RestoreAfterCadPick()
    {
        if (_cadPickSuspendCount <= 0)
        {
            return;
        }

        _cadPickSuspendCount--;
        if (_cadPickSuspendCount > 0)
        {
            return;
        }

        if (_paletteSet != null && _restoreVisibleAfterCadPick)
        {
            _paletteSet.Visible = true;
        }

        _restoreVisibleAfterCadPick = false;
    }

    private sealed class CadPickScope : IDisposable
    {
        private SectionToolboxPaletteService? _owner;

        public CadPickScope(SectionToolboxPaletteService owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            var owner = _owner;
            if (owner == null)
            {
                return;
            }

            _owner = null;
            owner.RestoreAfterCadPick();
        }
    }

    private sealed class NoOpScope : IDisposable
    {
        public static readonly NoOpScope Instance = new();

        public void Dispose()
        {
        }
    }
}
