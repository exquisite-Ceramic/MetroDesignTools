using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 变更检测用例接口
/// </summary>
public interface ICheckSectionUpdatesUseCase
{
    CheckSectionUpdatesResult Execute();
}

public sealed class CheckSectionUpdatesResult : OperationResult
{
    public IReadOnlyList<SectionCheckResult> Items { get; init; } = Array.Empty<SectionCheckResult>();
}
