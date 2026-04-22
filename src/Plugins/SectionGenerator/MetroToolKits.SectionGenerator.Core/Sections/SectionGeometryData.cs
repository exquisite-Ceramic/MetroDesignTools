using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

public enum SectionLineRole
{
    Generic,
    Structural,
    Finish
}

public enum SectionHatchCategory
{
    Wall,
    Column,
    Slab
}

public sealed class SectionLineSegment
{
    public required Line3D Line { get; init; }
    public SectionLineRole Role { get; init; } = SectionLineRole.Generic;
}

public sealed class SectionHatchRegion
{
    public SectionHatchCategory Category { get; init; }
    public IReadOnlyList<Point3D> Boundary { get; init; } = Array.Empty<Point3D>();
}

/// <summary>
/// 单个构件的剖面几何数据
/// </summary>
public sealed class ElementSectionData
{
    /// <summary>源构件句柄（用于双向定位）</summary>
    public string SourceHandle { get; init; } = string.Empty;

    /// <summary>源构件句柄集合（用于快照与反查）</summary>
    public IReadOnlyList<string> SourceHandles { get; init; } = Array.Empty<string>();

    /// <summary>构件类型</summary>
    public string ElementType { get; init; } = string.Empty;

    /// <summary>墙体模板标识（可选）</summary>
    public string? TemplateId { get; init; }

    /// <summary>剖切面域轮廓线（局部剖面 XY 坐标）</summary>
    public IReadOnlyList<Line3D> CutLines { get; init; } = Array.Empty<Line3D>();

    /// <summary>带角色语义的剖切线。</summary>
    public IReadOnlyList<SectionLineSegment> CutLineSegments { get; init; } = Array.Empty<SectionLineSegment>();

    /// <summary>投影看线（局部剖面 XY 坐标）</summary>
    public IReadOnlyList<Line3D> SightLines { get; init; } = Array.Empty<Line3D>();

    /// <summary>可用于填充的闭合轮廓。</summary>
    public IReadOnlyList<SectionHatchRegion> HatchRegions { get; init; } = Array.Empty<SectionHatchRegion>();
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

    /// <summary>楼层竖向轮廓。</summary>
    public FloorVerticalProfile? VerticalProfile { get; init; }

    /// <summary>各构件的剖面数据（局部剖面 XY 坐标）</summary>
    public IReadOnlyList<ElementSectionData> Elements { get; init; } = Array.Empty<ElementSectionData>();

    /// <summary>楼板剖面线（局部剖面 XY 坐标）</summary>
    public IReadOnlyList<Line3D> SlabLines { get; init; } = Array.Empty<Line3D>();

    /// <summary>带角色语义的楼板/边界板剖切线。</summary>
    public IReadOnlyList<SectionLineSegment> SlabLineSegments { get; init; } = Array.Empty<SectionLineSegment>();

    /// <summary>边界板或附加区域的填充轮廓。</summary>
    public IReadOnlyList<SectionHatchRegion> HatchRegions { get; init; } = Array.Empty<SectionHatchRegion>();

    /// <summary>所有剖切线（合并）</summary>
    public IEnumerable<Line3D> AllCutLines =>
        Elements.SelectMany(e => e.CutLineSegments.Count > 0 ? e.CutLineSegments.Select(segment => segment.Line) : e.CutLines)
            .Concat(SlabLineSegments.Count > 0 ? SlabLineSegments.Select(segment => segment.Line) : SlabLines);

    /// <summary>所有看线（合并）</summary>
    public IEnumerable<Line3D> AllSightLines =>
        Elements.SelectMany(e => e.SightLines);
}
