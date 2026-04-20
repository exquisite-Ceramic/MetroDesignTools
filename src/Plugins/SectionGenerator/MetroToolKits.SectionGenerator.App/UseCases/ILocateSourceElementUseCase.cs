namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 剖面实体定位源构件用例接口
/// </summary>
public interface ILocateSourceElementUseCase
{
    LocateSourceElementResult Execute(LocateSourceElementRequest request);
}

public sealed class LocateSourceElementRequest
{
    public string SectionEntityHandle { get; set; } = string.Empty;
}

public sealed class LocateSourceElementResult
{
    public bool Success { get; set; }
    public string SourceHandle { get; set; } = string.Empty;
    public string ElementType { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
