using MetroToolKits.Foundation.Building.Types;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 墙体构造模板目录。
/// </summary>
public interface IWallAssemblyTemplateCatalog
{
    IReadOnlyList<WallAssemblyTemplate> GetAllTemplates();
    WallAssemblyTemplate? GetById(string templateId);
    void SaveAll(IReadOnlyCollection<WallAssemblyTemplate> templates);
}
