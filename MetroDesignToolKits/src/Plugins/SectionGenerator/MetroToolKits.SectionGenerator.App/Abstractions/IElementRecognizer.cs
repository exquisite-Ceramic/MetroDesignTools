using MetroToolKits.Foundation.Building.Elements;
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
    IReadOnlyList<BuildingElement> RecognizeElements(Line3D sectionLine, double viewDepth);
}
