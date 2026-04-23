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
