using MetroToolKits.Foundation.Building.Types;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 楼板装配模板目录。
/// </summary>
public interface ISlabAssemblyTemplateCatalog
{
    IReadOnlyList<SlabAssemblyTemplate> GetAllTemplates();

    SlabAssemblyTemplate? GetById(string templateId);

    void SaveAll(IReadOnlyCollection<SlabAssemblyTemplate> templates);
}
