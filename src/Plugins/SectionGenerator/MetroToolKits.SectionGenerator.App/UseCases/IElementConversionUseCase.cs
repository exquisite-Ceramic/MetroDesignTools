namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 构件转换/恢复用例接口。
/// </summary>
public interface IElementConversionUseCase
{
    ElementConversionSelectionResult DescribeSelection(ElementConversionSelectionRequest request);
    ElementConversionApplyResult ApplyMappings(ElementConversionApplyRequest request);
    ElementConversionRevertResult Revert(ElementConversionRevertRequest request);
    ElementConversionRevertAllResult RevertAll();
}

public sealed class ElementConversionSelectionRequest
{
    public IReadOnlyCollection<string> EntityHandles { get; set; } = Array.Empty<string>();
}

public sealed class ElementConversionSelectionResult
{
    public bool Success { get; set; }
    public IReadOnlyList<string> Layers { get; set; } = Array.Empty<string>();
    public string? ErrorMessage { get; set; }
}

public sealed class ElementConversionApplyRequest
{
    public bool ApplyToEntireDrawing { get; set; }
    public IReadOnlyCollection<string> EntityHandles { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<LayerTypeAssignment> LayerMappings { get; set; } = Array.Empty<LayerTypeAssignment>();
}

public sealed class LayerTypeAssignment
{
    public string SourceLayerName { get; set; } = string.Empty;
    public string TypeId { get; set; } = string.Empty;
    public string? TemplateId { get; set; }
}

public sealed class ElementConversionApplyResult
{
    public bool Success { get; set; }
    public int ConvertedCount { get; set; }
    public int MappedLayerCount { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ElementConversionRevertRequest
{
    public IReadOnlyCollection<string> EntityHandles { get; set; } = Array.Empty<string>();
}

public sealed class ElementConversionRevertResult
{
    public bool Success { get; set; }
    public int RestoredCount { get; set; }
    public int SkippedCount { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ElementConversionRevertAllResult
{
    public bool Success { get; set; }
    public int RestoredCount { get; set; }
    public string? ErrorMessage { get; set; }
}
