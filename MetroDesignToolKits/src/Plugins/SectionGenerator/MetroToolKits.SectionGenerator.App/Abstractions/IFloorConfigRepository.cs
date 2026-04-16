using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 楼层配置仓储接口
/// </summary>
public interface IFloorConfigRepository
{
    SectionConfig Load();
    void Save(SectionConfig config);
}
