using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 剖面组合器 - 核心算法层
/// 接收剖切线和构件列表，计算并返回 SectionGeometryData
/// </summary>
public sealed class SectionComposer
{
    /// <summary>
    /// 生成单层剖面几何数据
    /// </summary>
    /// <param name="sectionLine">剖切线（平面坐标）</param>
    /// <param name="viewDirection">视图方向（看线方向）</param>
    /// <param name="elements">该层所有构件</param>
    /// <param name="floorConfig">楼层配置</param>
    /// <param name="baseElevation">楼层底部绝对标高</param>
    public SectionGeometryData Generate(
        Line3D sectionLine,
        Vector3D viewDirection,
        IEnumerable<BuildingElement> elements,
        FloorConfig floorConfig,
        double baseElevation = 0)
    {
        var elementDataList = new List<ElementSectionData>();

        foreach (var element in elements)
        {
            var cutLines = element.GetSectionGeometry(sectionLine, viewDirection).ToList();
            if (cutLines.Count == 0) continue;

            elementDataList.Add(new ElementSectionData
            {
                SourceHandle = element.SourceHandle ?? string.Empty,
                ElementType = element.ElementType,
                CutLines = cutLines,
                SightLines = Array.Empty<Line3D>()
            });
        }

        // 生成楼板线（顶板/底板）
        var slabLines = GenerateSlabLines(sectionLine, floorConfig, baseElevation);

        return new SectionGeometryData
        {
            FloorName = floorConfig.Name,
            BaseElevation = baseElevation,
            FloorHeight = floorConfig.Height,
            Elements = elementDataList,
            SlabLines = slabLines
        };
    }

    /// <summary>
    /// 生成楼板剖面线（顶板 + 底板）
    /// </summary>
    private List<Line3D> GenerateSlabLines(Line3D sectionLine, FloorConfig config, double baseElevation)
    {
        var lines = new List<Line3D>();

        // 剖切线在平面上的投影长度
        var start = sectionLine.Start;
        var end = sectionLine.End;

        // 底板顶面（地面层）
        double bottomSlabTop = baseElevation;
        double bottomSlabBottom = baseElevation - config.BottomSlabThickness;

        // 顶板底面
        double topSlabBottom = baseElevation + config.Height;
        double topSlabTop = topSlabBottom + config.TopSlabThickness;

        // 计算坡度偏移
        double slopeDelta = 0;
        if (config.HasSlope && config.SlopeValue > 0)
        {
            double length = sectionLine.Length;
            slopeDelta = length * config.SlopeValue;
        }

        // 底板线（水平）
        lines.Add(new Line3D(
            new Point3D(start.X, start.Y, bottomSlabBottom),
            new Point3D(end.X, end.Y, bottomSlabBottom)));
        lines.Add(new Line3D(
            new Point3D(start.X, start.Y, bottomSlabTop),
            new Point3D(end.X, end.Y, bottomSlabTop)));

        // 顶板线（带坡度）
        lines.Add(new Line3D(
            new Point3D(start.X, start.Y, topSlabBottom),
            new Point3D(end.X, end.Y, topSlabBottom + slopeDelta)));
        lines.Add(new Line3D(
            new Point3D(start.X, start.Y, topSlabTop),
            new Point3D(end.X, end.Y, topSlabTop + slopeDelta)));

        // 左右封边线
        lines.Add(new Line3D(
            new Point3D(start.X, start.Y, bottomSlabBottom),
            new Point3D(start.X, start.Y, topSlabTop)));
        lines.Add(new Line3D(
            new Point3D(end.X, end.Y, bottomSlabBottom),
            new Point3D(end.X, end.Y, topSlabTop + slopeDelta)));

        return lines;
    }
}
