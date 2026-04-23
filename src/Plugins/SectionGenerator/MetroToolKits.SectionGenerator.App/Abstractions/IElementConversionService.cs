using MetroToolKits.Foundation.Building.Types;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 构件转换基础设施服务接口。
/// </summary>
public interface IElementConversionService
{
    IReadOnlyList<string> GetDistinctLayers(IReadOnlyCollection<string> entityHandles);

    ElementConversionApplySummary ApplyMappingsToSelection(
        IReadOnlyCollection<string> entityHandles,
        IReadOnlyCollection<ElementLayerMapping> mappings);

    ElementConversionApplySummary ApplyMappingsToEntireDrawing(
        IReadOnlyCollection<ElementLayerMapping> mappings);

    ElementConversionRevertSummary Revert(IReadOnlyCollection<string> entityHandles);

    int RevertAll();
}

public sealed class ElementLayerMapping
{
    public string SourceLayerName { get; set; } = string.Empty;
    public ElementTypeDefinition ElementType { get; set; } = new();
    public string? TemplateId { get; set; }
}

public sealed class ElementConversionApplySummary
{
    public int ConvertedCount { get; set; }
    public int MappedLayerCount { get; set; }
}

public sealed class ElementConversionRevertSummary
{
    public int RestoredCount { get; set; }
    public int SkippedCount { get; set; }
}
