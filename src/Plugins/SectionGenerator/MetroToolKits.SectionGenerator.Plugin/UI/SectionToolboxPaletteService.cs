using System.Drawing;
using Autodesk.AutoCAD.Windows;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 工具箱 PaletteSet 管理器
/// </summary>
public sealed class SectionToolboxPaletteService
{
    private PaletteSet? _paletteSet;

    public void Show()
    {
        _paletteSet ??= CreatePaletteSet();
        _paletteSet.Visible = true;
    }

    private static PaletteSet CreatePaletteSet()
    {
        var paletteSet = new PaletteSet("MetroToolKits 工具箱")
        {
            MinimumSize = new Size(320, 480),
            Size = new Size(320, 480),
            DockEnabled = DockSides.Left | DockSides.Right
        };

        paletteSet.Style =
            PaletteSetStyles.ShowAutoHideButton |
            PaletteSetStyles.ShowCloseButton |
            PaletteSetStyles.ShowPropertiesMenu;
        paletteSet.AddVisual("SectionGenerator", new SectionToolboxControl());
        return paletteSet;
    }
}
