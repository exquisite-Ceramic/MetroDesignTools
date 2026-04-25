using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.Contracts.Templates;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface ISlabTemplateCatalogMapper
{
    SlabTemplateCatalogDto ToDto(IReadOnlyCollection<SlabAssemblyTemplate> templates);

    SlabAssemblyTemplateDto ToDto(SlabAssemblyTemplate template);

    IReadOnlyList<SlabAssemblyTemplate> ToDomain(SlabTemplateCatalogDto catalogDto);

    SlabAssemblyTemplate ToDomain(SlabAssemblyTemplateDto templateDto);
}
