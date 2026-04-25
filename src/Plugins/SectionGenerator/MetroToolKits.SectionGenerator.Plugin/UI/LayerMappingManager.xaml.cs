using System.Windows;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

/// <summary>
/// 图层映射管理器窗口
/// </summary>
public partial class LayerMappingManager : Window
{
    public LayerMappingManager(
        ILayerMappingWorkspaceAssembler workspaceAssembler,
        ILayerMappingApplyRequestMapper applyRequestMapper,
        IWallAssemblyTemplateCatalog wallTemplateCatalog,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IElementConversionUseCase conversionUseCase)
    {
        InitializeComponent();

        Panel.Initialize(
            workspaceAssembler,
            applyRequestMapper,
            wallTemplateCatalog,
            slabTemplateCatalog,
            conversionUseCase);

        Panel.ApplyCompleted += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
        Panel.CancelRequested += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
    }
}
