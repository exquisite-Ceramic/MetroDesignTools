using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 剖面块绘制结果。
/// 更新剖面依赖块参照 Handle 写入/读取快照，不能只返回块定义名称。
/// </summary>
public sealed class SectionBlockDrawResult
{
    public string BlockName   { get; init; } = string.Empty;
    public string BlockHandle { get; init; } = string.Empty;
}

/// <summary>
/// 绘图服务接口
/// </summary>
public interface IDrawingService
{
    /// <summary>绘制单层剖面块</summary>
    SectionBlockDrawResult DrawSectionBlock(SectionGeometryData geometryData, Point3D insertionPoint, FloorConfig floorConfig);

    /// <summary>绘制多楼层剖面块</summary>
    SectionBlockDrawResult DrawMultiFloorSectionBlock(MultiFloorSectionData multiData, Point3D insertionPoint,
        IReadOnlyList<FloorConfig> floors);
}
