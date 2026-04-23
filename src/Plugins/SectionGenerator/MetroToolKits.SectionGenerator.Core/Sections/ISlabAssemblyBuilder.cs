using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 楼板装配器。
/// </summary>
public interface ISlabAssemblyBuilder
{
    CompositeSlabElement Build(CoreSlabArea coreArea, SlabAssemblyTemplate template);
}
