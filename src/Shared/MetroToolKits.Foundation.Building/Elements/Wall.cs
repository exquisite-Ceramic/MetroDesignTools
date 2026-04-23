using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Foundation.Building.Elements;

/// <summary>
/// 墙体构件
/// </summary>
public sealed class Wall : BuildingElement
{
    /// <summary>
    /// 墙体起点
    /// </summary>
    public Point3D StartPoint { get; set; }

    /// <summary>
    /// 墙体终点
    /// </summary>
    public Point3D EndPoint { get; set; }

    /// <summary>
    /// 墙体厚度
    /// </summary>
    public double Thickness { get; set; } = 200.0;

    /// <summary>
    /// 墙体高度
    /// </summary>
    public double Height { get; set; } = 3000.0;

    /// <summary>
    /// 底部标高
    /// </summary>
    public double BaseElevation { get; set; }

    public Wall()
    {
        ElementType = "Wall";
    }

    public override Polygon3D? GetBoundingBox()
    {
        // 简化实现：返回墙体底面轮廓
        var direction = new Vector3D(
            EndPoint.X - StartPoint.X,
            EndPoint.Y - StartPoint.Y,
            0).Normalized;

        var normal = new Vector3D(-direction.Y, direction.X, 0);
        var halfThickness = Thickness / 2;

        var vertices = new List<Point3D>
        {
            new(StartPoint.X + normal.X * halfThickness, StartPoint.Y + normal.Y * halfThickness, BaseElevation),
            new(EndPoint.X + normal.X * halfThickness, EndPoint.Y + normal.Y * halfThickness, BaseElevation),
            new(EndPoint.X - normal.X * halfThickness, EndPoint.Y - normal.Y * halfThickness, BaseElevation),
            new(StartPoint.X - normal.X * halfThickness, StartPoint.Y - normal.Y * halfThickness, BaseElevation)
        };

        return new Polygon3D(vertices);
    }

    public override IEnumerable<Line3D> GetSectionGeometry(SectionGeometryContext context)
    {
        var sectionLine = context.SectionLine;
        // 计算剖切线与墙体的交点
        var wallLine = new Line3D(StartPoint, EndPoint);
        var intersection = LineIntersection2D(sectionLine, wallLine);

        if (intersection == null) yield break;

        var chainage = context.Projector.GetChainage(intersection.Value);
        var bottomElevation = context.VerticalProfile?.GetBottomStructuralTop(chainage) ?? BaseElevation;
        var topElevation = context.VerticalProfile?.GetTopStructuralBottom(chainage) ?? (BaseElevation + Height);

        if (topElevation <= bottomElevation + 1e-6)
        {
            yield break;
        }

        // 生成剖面线段（垂直方向）
        var bottom = new Point3D(intersection.Value.X, intersection.Value.Y, bottomElevation);
        var top = new Point3D(intersection.Value.X, intersection.Value.Y, topElevation);

        yield return new Line3D(bottom, top);
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
