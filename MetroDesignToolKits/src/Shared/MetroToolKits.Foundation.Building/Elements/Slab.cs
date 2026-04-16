using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Foundation.Building.Elements;

/// <summary>
/// 楼板构件
/// </summary>
public sealed class Slab : BuildingElement
{
    /// <summary>
    /// 楼板轮廓点
    /// </summary>
    public List<Point3D> Outline { get; set; } = new();

    /// <summary>
    /// 楼板厚度
    /// </summary>
    public double Thickness { get; set; } = 200.0;

    /// <summary>
    /// 顶面标高
    /// </summary>
    public double TopElevation { get; set; }

    /// <summary>
    /// 坡度目标（排水方向）
    /// </summary>
    public Point3D? SlopeTarget { get; set; }

    /// <summary>
    /// 坡度值
    /// </summary>
    public double SlopeValue { get; set; }

    public Slab()
    {
        ElementType = "Slab";
    }

    public override Polygon3D? GetBoundingBox()
    {
        if (Outline.Count < 3) return null;
        return new Polygon3D(Outline);
    }

    public override IEnumerable<Line3D> GetSectionGeometry(Line3D sectionLine, Vector3D viewDirection)
    {
        if (Outline.Count < 2) yield break;

        // 找出剖切线与楼板轮廓的交点
        var intersections = new List<Point3D>();
        for (int i = 0; i < Outline.Count; i++)
        {
            var p1 = Outline[i];
            var p2 = Outline[(i + 1) % Outline.Count];
            var edge = new Line3D(p1, p2);
            var intersection = LineIntersection2D(sectionLine, edge);
            if (intersection.HasValue)
            {
                intersections.Add(intersection.Value);
            }
        }

        if (intersections.Count < 2) yield break;

        // 生成楼板剖面线段
        var sortedIntersections = intersections.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        var left = sortedIntersections[0];
        var right = sortedIntersections[^1];

        // 顶面线
        var topLeft = new Point3D(left.X, left.Y, TopElevation);
        var topRight = new Point3D(right.X, right.Y, TopElevation);
        yield return new Line3D(topLeft, topRight);

        // 底面线
        var bottomLeft = new Point3D(left.X, left.Y, TopElevation - Thickness);
        var bottomRight = new Point3D(right.X, right.Y, TopElevation - Thickness);
        yield return new Line3D(bottomLeft, bottomRight);

        // 左侧竖线
        yield return new Line3D(topLeft, bottomLeft);

        // 右侧竖线
        yield return new Line3D(topRight, bottomRight);
    }

    private Point3D? LineIntersection2D(Line3D line1, Line3D line2)
    {
        var x1 = line1.Start.X; var y1 = line1.Start.Y;
        var x2 = line1.End.X; var y2 = line1.End.Y;
        var x3 = line2.Start.X; var y3 = line2.Start.Y;
        var x4 = line2.End.X; var y4 = line2.End.Y;

        var denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 1e-10) return null;

        var t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        var u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;

        if (t < 0 || t > 1 || u < 0 || u > 1) return null;

        return new Point3D(
            x1 + t * (x2 - x1),
            y1 + t * (y2 - y1),
            line1.Start.Z + t * (line1.End.Z - line1.Start.Z));
    }
}

