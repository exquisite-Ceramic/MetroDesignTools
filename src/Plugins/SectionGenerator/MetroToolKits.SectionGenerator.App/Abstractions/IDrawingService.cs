using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 绘图服务接口
/// </summary>
public interface IDrawingService
{
    /// <summary>绘制单层剖面块</summary>
    DrawSectionBlockResult DrawSectionBlock(
        SectionGeometryData geometryData,
        Point3D insertionPoint,
        FloorConfig floorConfig,
        double geometryAnchorX = 0);

    /// <summary>绘制单层剖面块（带图纸级输出配置）</summary>
    DrawSectionBlockResult DrawSectionBlock(
        SectionGeometryData geometryData,
        Point3D insertionPoint,
        FloorConfig floorConfig,
        SectionOutputConfig outputConfig,
        double geometryAnchorX = 0);

    /// <summary>绘制多楼层剖面块</summary>
    DrawSectionBlockResult DrawMultiFloorSectionBlock(
        MultiFloorSectionData multiData,
        Point3D insertionPoint,
        IReadOnlyList<FloorConfig> floors,
        double geometryAnchorX = 0);

    /// <summary>绘制多楼层剖面块（带图纸级输出配置）</summary>
    DrawSectionBlockResult DrawMultiFloorSectionBlock(
        MultiFloorSectionData multiData,
        Point3D insertionPoint,
        IReadOnlyList<FloorConfig> floors,
        SectionOutputConfig outputConfig,
        double geometryAnchorX = 0);
}

/// <summary>
/// 绘制剖面块结果
/// </summary>
public sealed class DrawSectionBlockResult
{
    public string BlockName { get; init; } = string.Empty;
    public string BlockHandle { get; init; } = string.Empty;
}
