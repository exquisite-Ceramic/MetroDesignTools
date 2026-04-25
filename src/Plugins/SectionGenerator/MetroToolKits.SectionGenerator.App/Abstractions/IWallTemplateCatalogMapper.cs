using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.Contracts.Templates;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface IWallTemplateCatalogMapper
{
    WallTemplateCatalogDto ToDto(IReadOnlyCollection<WallAssemblyTemplate> templates);

    WallAssemblyTemplateDto ToDto(WallAssemblyTemplate template);

    IReadOnlyList<WallAssemblyTemplate> ToDomain(WallTemplateCatalogDto catalogDto);

    WallAssemblyTemplate ToDomain(WallAssemblyTemplateDto templateDto);
}
