using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 构件识别接口 - 从 CAD 图纸中识别建筑构件
/// </summary>
public interface IElementRecognizer
{
    /// <summary>
    /// 识别剖切线附近的构件
    /// </summary>
    ElementRecognitionResult RecognizeElements(Line3D sectionLine, double viewDepth, ScopeBounds2D? scopeBounds = null);
}

/// <summary>
/// 构件识别接口的分离模型版本。旧接口的 Elements 仍表示可剖切构件。
/// </summary>
public interface ISectionElementRecognizerV2 : IElementRecognizer
{
    /// <summary>
    /// 分离识别可剖切构件和视深候选构件。
    /// </summary>
    SectionRecognitionSet RecognizeSectionElements(Line3D sectionLine, double viewDepth, ScopeBounds2D? scopeBounds = null);
}

/// <summary>
/// 构件识别结果。
/// </summary>
public sealed class ElementRecognitionResult
{
    public IReadOnlyList<BuildingElement> Elements { get; init; } = Array.Empty<BuildingElement>();
    public IReadOnlyList<OperationDiagnostic> Diagnostics { get; init; } = Array.Empty<OperationDiagnostic>();
    public int ScannedEntityCount { get; init; }
    public int MatchedLayerCount { get; init; }
    public int IntersectingElementCount { get; init; }
}

/// <summary>
/// 分离后的构件识别结果。CutElements 保持现有剖切输出语义，ViewDepthCandidates 仅作为后续 SightLines 的候选输入。
/// </summary>
public sealed class SectionRecognitionSet
{
    public IReadOnlyList<BuildingElement> CutElements { get; init; } = Array.Empty<BuildingElement>();
    public IReadOnlyList<ViewDepthCandidate> ViewDepthCandidates { get; init; } = Array.Empty<ViewDepthCandidate>();
    public IReadOnlyList<OperationDiagnostic> Diagnostics { get; init; } = Array.Empty<OperationDiagnostic>();
}

/// <summary>
/// 位于 viewDepth strip 内但不一定与剖切线相交的候选构件。
/// </summary>
public sealed class ViewDepthCandidate
{
    public required BuildingElement Element { get; init; }
    public string SourceHandle { get; init; } = string.Empty;
    public string ElementType { get; init; } = string.Empty;
    public double MinChainage { get; init; }
    public double MaxChainage { get; init; }
    public double MinDepth { get; init; }
    public double MaxDepth { get; init; }
}
