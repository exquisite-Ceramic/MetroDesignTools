using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 单个构件的剖面几何数据
/// </summary>
public sealed class ElementSectionData
{
    /// <summary>源构件句柄（用于双向定位）</summary>
    public string SourceHandle { get; init; } = string.Empty;

    /// <summary>构件类型</summary>
    public string ElementType { get; init; } = string.Empty;

    /// <summary>剖切面域轮廓线（被剖切到的截面）</summary>
    public IReadOnlyList<Line3D> CutLines { get; init; } = Array.Empty<Line3D>();

    /// <summary>投影看线（剖切面后方可见的线）</summary>
    public IReadOnlyList<Line3D> SightLines { get; init; } = Array.Empty<Line3D>();
}

/// <summary>
/// 单层剖面几何数据
/// </summary>
public sealed class SectionGeometryData
{
    /// <summary>楼层名称</summary>
    public string FloorName { get; init; } = string.Empty;

    /// <summary>楼层底部标高（绝对坐标）</summary>
    public double BaseElevation { get; init; }

    /// <summary>层高</summary>
    public double FloorHeight { get; init; }

    /// <summary>各构件的剖面数据</summary>
    public IReadOnlyList<ElementSectionData> Elements { get; init; } = Array.Empty<ElementSectionData>();

    /// <summary>楼板剖面线（顶板/底板）</summary>
    public IReadOnlyList<Line3D> SlabLines { get; init; } = Array.Empty<Line3D>();

    /// <summary>所有剖切线（合并）</summary>
    public IEnumerable<Line3D> AllCutLines =>
        Elements.SelectMany(e => e.CutLines).Concat(SlabLines);

    /// <summary>所有看线（合并）</summary>
    public IEnumerable<Line3D> AllSightLines =>
        Elements.SelectMany(e => e.SightLines);
}
