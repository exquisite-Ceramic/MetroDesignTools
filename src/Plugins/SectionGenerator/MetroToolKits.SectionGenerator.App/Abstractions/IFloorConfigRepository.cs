using MetroToolKits.SectionGenerator.App.Models;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 楼层配置仓储接口
/// </summary>
public interface IFloorConfigRepository
{
    LoadedSectionConfig Load();
    void Save(LoadedSectionConfig config);
}
