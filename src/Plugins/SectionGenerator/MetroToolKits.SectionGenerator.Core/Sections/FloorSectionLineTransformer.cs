using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 楼层剖切线变换器
/// </summary>
public static class FloorSectionLineTransformer
{
    /// <summary>
    /// 按楼层三点对齐配置变换剖切线；未配置时返回原始剖切线。
    /// </summary>
    public static Line3D ApplyAlignment(Line3D sectionLine, FloorConfig floor)
    {
        if (floor.AlignmentSourcePoints.Count < 3 || floor.AlignmentTargetPoints.Count < 3)
            return sectionLine;

        var src = floor.AlignmentSourcePoints;
        var dst = floor.AlignmentTargetPoints;

        var transform = Transform3D.AlignPoints(
            src[0], src[1], src[2],
            dst[0], dst[1], dst[2]);

        return new Line3D(
            transform.Transform(sectionLine.Start),
            transform.Transform(sectionLine.End));
    }
}
