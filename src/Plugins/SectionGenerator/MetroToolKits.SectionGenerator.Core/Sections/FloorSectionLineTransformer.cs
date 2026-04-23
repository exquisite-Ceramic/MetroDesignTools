using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 楼层剖切线变换与对齐点校验。
/// </summary>
public static class FloorSectionLineTransformer
{
    public const double AlignmentTolerance = 1e-6;

    public static bool TryValidateAlignmentPoints(
        IReadOnlyList<Point3D>? points,
        out string errorMessage)
    {
        if (points == null || points.Count != 3)
        {
            errorMessage = "对齐点必须正好提供 3 个。";
            return false;
        }

        if (points[0].DistanceTo(points[1]) <= AlignmentTolerance ||
            points[0].DistanceTo(points[2]) <= AlignmentTolerance ||
            points[1].DistanceTo(points[2]) <= AlignmentTolerance)
        {
            errorMessage = "对齐点之间的距离必须大于容差，不能重复。";
            return false;
        }

        var xAxis = new Vector3D(
            points[1].X - points[0].X,
            points[1].Y - points[0].Y,
            points[1].Z - points[0].Z);
        var yAxis = new Vector3D(
            points[2].X - points[0].X,
            points[2].Y - points[0].Y,
            points[2].Z - points[0].Z);

        var cross = Vector3D.Cross(xAxis, yAxis);
        if (cross.Length <= AlignmentTolerance)
        {
            errorMessage = "对齐点不能共线。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public static Line3D ApplyAlignment(
        Line3D sectionLine,
        IReadOnlyList<Point3D> sourcePoints,
        IReadOnlyList<Point3D> targetPoints)
    {
        var transform = CreateAlignmentTransform(sourcePoints, targetPoints);

        return new Line3D(
            transform.Transform(sectionLine.Start),
            transform.Transform(sectionLine.End));
    }

    public static Transform3D CreateAlignmentTransform(
        IReadOnlyList<Point3D> sourcePoints,
        IReadOnlyList<Point3D> targetPoints)
    {
        if (!TryValidateAlignmentPoints(sourcePoints, out var sourceError))
            throw new ArgumentException($"源对齐点无效: {sourceError}", nameof(sourcePoints));

        if (!TryValidateAlignmentPoints(targetPoints, out var targetError))
            throw new ArgumentException($"目标对齐点无效: {targetError}", nameof(targetPoints));

        return Transform3D.AlignPoints(
            sourcePoints[0], sourcePoints[1], sourcePoints[2],
            targetPoints[0], targetPoints[1], targetPoints[2]);
    }
}
