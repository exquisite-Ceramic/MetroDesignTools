using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Foundation.Building.Elements;

/// <summary>
/// 建筑构件抽象基类
/// </summary>
public abstract class BuildingElement
{
    /// <summary>
    /// 构件唯一标识
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 构件类型
    /// </summary>
    public string ElementType { get; set; } = string.Empty;

    /// <summary>
    /// 构件名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 所在楼层
    /// </summary>
    public string Floor { get; set; } = string.Empty;

    /// <summary>
    /// 原始 CAD 实体句柄
    /// </summary>
    public string? SourceHandle { get; set; }

    /// <summary>
    /// 原始 CAD 实体句柄集合
    /// </summary>
    public List<string> SourceHandles { get; set; } = new();

    /// <summary>
    /// 原始图层名称
    /// </summary>
    public string? SourceLayer { get; set; }

    /// <summary>
    /// 获取构件的几何边界
    /// </summary>
    public abstract Polygon3D? GetBoundingBox();

    /// <summary>
    /// 获取构件的剖面几何（给定统一剖面上下文）
    /// </summary>
    public abstract IEnumerable<Line3D> GetSectionGeometry(SectionGeometryContext context);
}
