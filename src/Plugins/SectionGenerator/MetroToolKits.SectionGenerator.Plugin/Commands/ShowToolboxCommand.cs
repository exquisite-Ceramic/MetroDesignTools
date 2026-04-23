using MetroToolKits.Foundation.Core.Hosting;
using MetroToolKits.SectionGenerator.Plugin.UI;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 打开工具箱面板命令
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.ShowToolbox)]
public sealed class ShowToolboxCommand
{
    private readonly SectionToolboxPaletteService _paletteService;

    public ShowToolboxCommand(SectionToolboxPaletteService paletteService)
    {
        _paletteService = paletteService;
    }

    public void Execute()
    {
        _paletteService.Show();
    }
}
