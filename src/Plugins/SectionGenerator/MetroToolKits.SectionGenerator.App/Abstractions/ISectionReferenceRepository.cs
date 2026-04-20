namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 剖面实体与源构件引用关系仓储
/// </summary>
public interface ISectionReferenceRepository
{
    SectionEntityReference? GetSectionEntityReference(string sectionEntityHandle);
}

/// <summary>
/// 剖面实体引用信息
/// </summary>
public sealed class SectionEntityReference
{
    public string SourceHandle { get; init; } = string.Empty;
    public string ElementType { get; init; } = string.Empty;
}
