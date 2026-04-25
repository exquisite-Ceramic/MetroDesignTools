using System.Drawing;
using Autodesk.AutoCAD.Windows;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 工具箱 PaletteSet 管理器
/// </summary>
public sealed class SectionToolboxPaletteService
{
    private readonly IGenerateSectionPreflightUseCase _preflightUseCase;
    private readonly IWorkbenchSnapshotAssembler _workbenchSnapshotAssembler;
    private readonly FloorConfigPaletteController _floorConfigPaletteController;
    private PaletteSet? _paletteSet;

    public SectionToolboxPaletteService(
        IGenerateSectionPreflightUseCase preflightUseCase,
        IWorkbenchSnapshotAssembler workbenchSnapshotAssembler,
        FloorConfigPaletteController floorConfigPaletteController)
    {
        _preflightUseCase = preflightUseCase;
        _workbenchSnapshotAssembler = workbenchSnapshotAssembler;
        _floorConfigPaletteController = floorConfigPaletteController;
    }

    public void Show()
    {
        _paletteSet ??= CreatePaletteSet();
        _paletteSet.Visible = true;
    }

    private PaletteSet CreatePaletteSet()
    {
        var paletteSet = new PaletteSet("MetroToolKits 工具箱")
        {
            MinimumSize = new Size(760, 720),
            Size = new Size(760, 720),
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
                _floorConfigPaletteController));
        return paletteSet;
    }
}
