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
    private readonly ILayerMappingApplyRequestMapper _applyRequestMapper;
    private readonly IElementConversionUseCase _conversionUseCase;

    public LayerMappingManager(
        ILayerMappingWorkspaceAssembler workspaceAssembler,
        ILayerMappingApplyRequestMapper applyRequestMapper,
        IWallAssemblyTemplateCatalog wallTemplateCatalog,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IElementConversionUseCase conversionUseCase)
    {
        _applyRequestMapper = applyRequestMapper;
        _conversionUseCase = conversionUseCase;

        InitializeComponent();

        Panel.Initialize(
            workspaceAssembler,
            applyRequestMapper,
            wallTemplateCatalog,
            slabTemplateCatalog);

        Panel.ApplyRequested += (_, e) =>
        {
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
