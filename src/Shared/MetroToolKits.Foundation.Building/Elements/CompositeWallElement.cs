using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Foundation.Building.Elements;

/// <summary>
/// 核心墙体锚点。
/// </summary>
public sealed class CoreWallSegment
{
    public Point3D StartPoint { get; set; }
    public Point3D EndPoint { get; set; }
    public double Thickness { get; set; } = 200.0;
    public double Height { get; set; } = 3000.0;
    public double BaseElevation { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string? SourceLayer { get; set; }
    public List<string> SourceHandles { get; set; } = new();
}

/// <summary>
/// 墙体分层剖面段。
/// </summary>
public sealed class WallLayerSection
{
    public string Name { get; set; } = string.Empty;
    public string MaterialOrCategory { get; set; } = string.Empty;
    public bool VisibleInSection { get; set; } = true;
    public double Thickness { get; set; }
    public double InnerOffset { get; set; }
    public double OuterOffset { get; set; }
}

/// <summary>
/// 可组合墙体抽象。
/// </summary>
public abstract class CompositeWallElement : BuildingElement
{
    public string TemplateId { get; init; } = string.Empty;
    public string TemplateName { get; init; } = string.Empty;
    public WallVerticalAnchorMode VerticalAnchorMode { get; init; } = WallVerticalAnchorMode.StructuralSlabFaces;
    public CoreWallSegment CoreSegment { get; init; } = new();
    public IReadOnlyList<WallLayerSection> LayerSections { get; init; } = Array.Empty<WallLayerSection>();

    protected static Point3D? Intersect2D(Line3D line1, Line3D line2)
    {
        var x1 = line1.Start.X; var y1 = line1.Start.Y;
        var x2 = line1.End.X;   var y2 = line1.End.Y;
        var x3 = line2.Start.X; var y3 = line2.Start.Y;
        var x4 = line2.End.X;   var y4 = line2.End.Y;

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

    protected static Line3D OffsetLine(Line3D baseLine, Vector3D normal, double offset)
    {
        var delta = normal * offset;
        return new Line3D(
            new Point3D(baseLine.Start.X + delta.X, baseLine.Start.Y + delta.Y, baseLine.Start.Z + delta.Z),
            new Point3D(baseLine.End.X + delta.X, baseLine.End.Y + delta.Y, baseLine.End.Z + delta.Z));
    }

    public override Polygon3D? GetBoundingBox()
    {
        if (LayerSections.Count == 0)
        {
            return null;
        }

        var centerLine = new Line3D(CoreSegment.StartPoint, CoreSegment.EndPoint);
        var direction = centerLine.Direction.Normalized;
        if (direction.Length <= 1e-9)
        {
            return null;
        }

        var normal = new Vector3D(-direction.Y, direction.X, 0).Normalized;
        var minOffset = LayerSections.Min(layer => Math.Min(layer.InnerOffset, layer.OuterOffset));
        var maxOffset = LayerSections.Max(layer => Math.Max(layer.InnerOffset, layer.OuterOffset));

        var startOuter = new Point3D(centerLine.Start.X + normal.X * maxOffset, centerLine.Start.Y + normal.Y * maxOffset, centerLine.Start.Z);
        var endOuter = new Point3D(centerLine.End.X + normal.X * maxOffset, centerLine.End.Y + normal.Y * maxOffset, centerLine.End.Z);
        var endInner = new Point3D(centerLine.End.X + normal.X * minOffset, centerLine.End.Y + normal.Y * minOffset, centerLine.End.Z);
        var startInner = new Point3D(centerLine.Start.X + normal.X * minOffset, centerLine.Start.Y + normal.Y * minOffset, centerLine.Start.Z);

        return new Polygon3D(new[] { startOuter, endOuter, endInner, startInner });
    }

    public override IEnumerable<Line3D> GetSectionGeometry(SectionGeometryContext context)
    {
        var sectionLine = context.SectionLine;
        var centerLine = new Line3D(CoreSegment.StartPoint, CoreSegment.EndPoint);
        var direction = centerLine.Direction.Normalized;
        if (direction.Length <= 1e-9)
        {
            yield break;
        }

        var normal = new Vector3D(-direction.Y, direction.X, 0).Normalized;

        foreach (var layer in LayerSections.Where(layer => layer.VisibleInSection))
        {
            var innerLine = OffsetLine(centerLine, normal, layer.InnerOffset);
            var outerLine = OffsetLine(centerLine, normal, layer.OuterOffset);
            var innerPoint = Intersect2D(sectionLine, innerLine);
            var outerPoint = Intersect2D(sectionLine, outerLine);

            if (!innerPoint.HasValue || !outerPoint.HasValue)
            {
                continue;
            }

            if (innerPoint.Value.DistanceTo(outerPoint.Value) <= 1e-6)
            {
                continue;
            }

            var innerChainage = context.Projector.GetChainage(innerPoint.Value);
            var outerChainage = context.Projector.GetChainage(outerPoint.Value);
            var bottomInnerElevation = context.VerticalProfile?.GetBottomStructuralTop(innerChainage) ?? CoreSegment.BaseElevation;
            var topInnerElevation = context.VerticalProfile?.GetTopStructuralBottom(innerChainage) ?? (CoreSegment.BaseElevation + CoreSegment.Height);
            var bottomOuterElevation = context.VerticalProfile?.GetBottomStructuralTop(outerChainage) ?? CoreSegment.BaseElevation;
            var topOuterElevation = context.VerticalProfile?.GetTopStructuralBottom(outerChainage) ?? (CoreSegment.BaseElevation + CoreSegment.Height);

            if (topInnerElevation <= bottomInnerElevation + 1e-6 ||
                topOuterElevation <= bottomOuterElevation + 1e-6)
            {
                continue;
            }

            var bottomInner = new Point3D(innerPoint.Value.X, innerPoint.Value.Y, bottomInnerElevation);
            var topInner = new Point3D(innerPoint.Value.X, innerPoint.Value.Y, topInnerElevation);
            var bottomOuter = new Point3D(outerPoint.Value.X, outerPoint.Value.Y, bottomOuterElevation);
            var topOuter = new Point3D(outerPoint.Value.X, outerPoint.Value.Y, topOuterElevation);

            yield return new Line3D(bottomInner, topInner);
            yield return new Line3D(bottomOuter, topOuter);
            yield return new Line3D(bottomInner, bottomOuter);
            yield return new Line3D(topInner, topOuter);
        }
    }
}

/// <summary>
/// 模板装配得到的复合墙体。
/// </summary>
public sealed class TemplatedCompositeWallElement : CompositeWallElement
{
    public TemplatedCompositeWallElement()
    {
        ElementType = "Wall";
    }
}
