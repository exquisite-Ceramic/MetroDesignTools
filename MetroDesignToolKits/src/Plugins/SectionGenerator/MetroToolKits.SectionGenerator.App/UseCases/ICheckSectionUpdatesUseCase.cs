using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 变更检测用例接口
/// </summary>
public interface ICheckSectionUpdatesUseCase
{
    IReadOnlyList<SectionCheckResult> Execute();
}
