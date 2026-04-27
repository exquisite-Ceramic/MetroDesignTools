using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Contracts.LayerMapping;
using MetroToolKits.SectionGenerator.Contracts.Templates;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Support;

public sealed class LayerMappingWorkspaceAssembler : ILayerMappingWorkspaceAssembler
{
    private readonly ILayerNameProvider _layerNameProvider;
    private readonly IElementTypeCatalog _elementTypeCatalog;
    private readonly IWallAssemblyTemplateCatalog _wallTemplateCatalog;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;

    public LayerMappingWorkspaceAssembler(
        ILayerNameProvider layerNameProvider,
        IElementTypeCatalog elementTypeCatalog,
        IWallAssemblyTemplateCatalog wallTemplateCatalog,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog)
    {
        _layerNameProvider = layerNameProvider;
        _elementTypeCatalog = elementTypeCatalog;
        _wallTemplateCatalog = wallTemplateCatalog;
        _slabTemplateCatalog = slabTemplateCatalog;
    }

    public LayerMappingWorkspaceDto Assemble()
    {
        var layers = _layerNameProvider
            .GetAllLayerNames()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new LayerInfoDto
            {
                LayerName = name
            })
            .ToList();

        var elementTypes = _elementTypeCatalog
            .GetAllTypes()
            .Where(type => type.IsEnabled)
            .Select(type => new ElementTypeOptionDto
            {
                TypeId = type.TypeId,
                TypeName = type.TypeName,
                ShortcutKey = type.ShortcutKey,
                TargetLayerPrefix = type.TargetLayerPrefix,
                ColorIndex = type.LayerColorIndex,
                IsEnabled = type.IsEnabled
            })
            .ToList();

        var wallTemplates = _wallTemplateCatalog
            .GetAllTemplates()
            .Select(template => new AssemblyTemplateOptionDto
            {
                TemplateId = template.TemplateId,
                TemplateName = template.TemplateName
            })
            .ToList();

        var slabTemplates = _slabTemplateCatalog
            .GetAllTemplates()
            .Select(template => new AssemblyTemplateOptionDto
            {
                TemplateId = template.TemplateId,
                TemplateName = template.TemplateName
            })
            .ToList();

        return new LayerMappingWorkspaceDto
        {
            Layers = layers,
            ElementTypes = elementTypes,
            WallTemplateOptions = wallTemplates,
            SlabTemplateOptions = slabTemplates,
            Mappings = Array.Empty<LayerMappingDto>(),
            SummaryText = $"图层 {layers.Count} 个，启用构件类型 {elementTypes.Count} 个，墙体模板 {wallTemplates.Count} 个，楼板模板 {slabTemplates.Count} 个"
        };
    }
}
