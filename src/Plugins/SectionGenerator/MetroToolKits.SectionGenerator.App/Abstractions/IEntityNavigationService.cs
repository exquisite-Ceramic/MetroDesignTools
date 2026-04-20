namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// CAD 视图定位服务
/// </summary>
public interface IEntityNavigationService
{
    NavigationResult FocusEntity(string handle);
    NavigationResult FocusBlock(string blockHandle);
}

/// <summary>
/// 定位结果
/// </summary>
public sealed class NavigationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
