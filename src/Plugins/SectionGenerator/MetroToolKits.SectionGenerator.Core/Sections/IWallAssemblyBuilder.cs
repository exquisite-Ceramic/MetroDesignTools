using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 墙体构造器接口。
/// </summary>
public interface IWallAssemblyBuilder
{
    CompositeWallElement Build(CoreWallSegment coreSegment, WallAssemblyTemplate template);
}
