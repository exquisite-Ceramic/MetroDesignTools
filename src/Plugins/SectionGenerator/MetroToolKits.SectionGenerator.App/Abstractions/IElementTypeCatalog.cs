using MetroToolKits.Foundation.Building.Types;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 构件类型目录接口。
/// </summary>
public interface IElementTypeCatalog
{
    IReadOnlyList<ElementTypeDefinition> GetAllTypes();
    IReadOnlyList<ElementTypeDefinition> GetEnabledTypes();
    ElementTypeDefinition? GetByShortcutKey(string shortcutKey);
    ElementTypeDefinition? GetByTypeId(string typeId);
}
