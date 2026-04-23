using MetroToolKits.SectionGenerator.App.Models;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 楼层配置读写用例。
/// </summary>
public interface IFloorConfigUseCase
{
    LoadedSectionConfig Load();
    FloorConfigSaveResult Save(LoadedSectionConfig config);
}
