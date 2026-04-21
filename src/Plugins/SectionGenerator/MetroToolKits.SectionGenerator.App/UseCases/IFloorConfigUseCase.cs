using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 楼层配置读写用例。
/// </summary>
public interface IFloorConfigUseCase
{
    SectionConfig Load();
    void Save(SectionConfig config);
}
