using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Foundation.Building.Elements;

/// <summary>
/// 柱构件
/// </summary>
public sealed class Column : BuildingElement
{
    /// <summary>
    /// 柱中心点
    /// </summary>
    public Point3D CenterPoint { get; set; }

    /// <summary>
    /// 柱宽度（X方向）
    /// </summary>
    public double Width { get; set; } = 400.0;

    /// <summary>
    /// 柱深度（Y方向）
    /// </summary>
    public double Depth { get; set; } = 400.0;

    /// <summary>
    /// 柱高度
    /// </summary>
    public double Height { get; set; } = 3000.0;

    /// <summary>
    /// 底部标高
    /// </summary>
    public double BaseElevation { get; set; }

    /// <summary>
    /// 旋转角度（弧度）
    /// </summary>
    public double Rotation { get; set; }

    public Column()
    {
        ElementType = "Column";
    }

    public override Polygon3D? GetBoundingBox()
    {
        var cos = Math.Cos(Rotation);
        var sin = Math.Sin(Rotation);
        var halfW = Width / 2;
        var halfD = Depth / 2;

        var vertices = new List<Point3D>
        {
            RotatePoint(CenterPoint, -halfW, -halfD, cos, sin, BaseElevation),
            RotatePoint(CenterPoint, halfW, -halfD, cos, sin, BaseElevation),
            RotatePoint(CenterPoint, halfW, halfD, cos, sin, BaseElevation),
            RotatePoint(CenterPoint, -halfW, halfD, cos, sin, BaseElevation)
        };

        return new Polygon3D(vertices);
    }

    public override IEnumerable<Line3D> GetSectionGeometry(Line3D sectionLine, Vector3D viewDirection)
    {
        // 检查剖切线是否穿过柱子
        var bbox = GetBoundingBox();
        if (bbox == null) yield break;

        var vertices = bbox.Vertices.ToList();
        var intersections = new List<Point3D>();

        for (int i = 0; i < 4; i++)
        {
            var edge = new Line3D(vertices[i], vertices[(i + 1) % 4]);
            var intersection = LineIntersection2D(sectionLine, edge);
            if (intersection.HasValue)
            {
                intersections.Add(intersection.Value);
            }
        }

        if (intersections.Count < 2) yield break;

        // 生成柱剖面
        var left = intersections.OrderBy(p => p.X).First();
        var right = intersections.OrderBy(p => p.X).Last();

        var bottomLeft = new Point3D(left.X, left.Y, BaseElevation);
        var topLeft = new Point3D(left.X, left.Y, BaseElevation + Height);
        var bottomRight = new Point3D(right.X, right.Y, BaseElevation);
        var topRight = new Point3D(right.X, right.Y, BaseElevation + Height);

        yield return new Line3D(bottomLeft, topLeft);
        yield return new Line3D(bottomRight, topRight);
        yield return new Line3D(topLeft, topRight);
        yield return new Line3D(bottomLeft, bottomRight);
    }

    private Point3D RotatePoint(Point3D center, double dx, double dy, double cos, double sin, double z)
    {
        return new Point3D(
            center.X + dx * cos - dy * sin,
            center.Y + dx * sin + dy * cos,
            z);
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

