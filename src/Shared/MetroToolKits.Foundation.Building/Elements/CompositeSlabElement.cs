using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Foundation.Building.Elements;

/// <summary>
/// 核心楼板区域。
/// </summary>
public sealed class CoreSlabArea
{
    public List<Point3D> Outline { get; set; } = new();

    public double CoreTopElevation { get; set; }

    public string TemplateId { get; set; } = string.Empty;

    public string? SourceLayer { get; set; }

    public List<string> SourceHandles { get; set; } = new();
}

public sealed class SlabLayerSection
{
    public string Name { get; set; } = string.Empty;

    public string MaterialOrCategory { get; set; } = string.Empty;

    public bool VisibleInSection { get; set; } = true;

    public bool IsCore { get; set; }

    public SlabLayerSide? Side { get; set; }

    public double Thickness { get; set; }

    public double TopOffset { get; set; }

    public double BottomOffset { get; set; }
}

/// <summary>
/// 可组合楼板。
/// </summary>
public sealed class CompositeSlabElement : BuildingElement
{
    public string TemplateId { get; init; } = string.Empty;

    public string TemplateName { get; init; } = string.Empty;

    public SlabWallJunctionMode WallJunctionMode { get; init; } = SlabWallJunctionMode.StopAtWallFace;

    public CoreSlabArea CoreArea { get; init; } = new();

    public IReadOnlyList<SlabLayerSection> LayerSections { get; init; } = Array.Empty<SlabLayerSection>();

    public CompositeSlabElement()
    {
        ElementType = "Slab";
    }

    public override Polygon3D? GetBoundingBox()
    {
        if (CoreArea.Outline.Count < 3)
        {
            return null;
        }

        return new Polygon3D(CoreArea.Outline);
    }

    public override IEnumerable<Line3D> GetSectionGeometry(SectionGeometryContext context)
    {
        if (CoreArea.Outline.Count < 2)
        {
            yield break;
        }

        var intersections = new List<Point3D>();
        for (int i = 0; i < CoreArea.Outline.Count; i++)
        {
            var p1 = CoreArea.Outline[i];
            var p2 = CoreArea.Outline[(i + 1) % CoreArea.Outline.Count];
            var edge = new Line3D(p1, p2);
            var intersection = LineIntersection2D(context.SectionLine, edge);
            if (intersection.HasValue)
            {
                intersections.Add(intersection.Value);
            }
        }

        if (intersections.Count < 2)
        {
            yield break;
        }

        var ordered = intersections
            .Select(point => new
            {
                Point = point,
                Chainage = context.Projector.GetChainage(point)
            })
            .OrderBy(item => item.Chainage)
            .ToList();

        var unique = new List<(Point3D Point, double Chainage)>();
        foreach (var point in ordered)
        {
            if (unique.Count == 0 || Math.Abs(unique[^1].Chainage - point.Chainage) > 1e-6)
            {
                unique.Add((point.Point, point.Chainage));
            }
        }

        if (unique.Count < 2)
        {
            yield break;
        }

        var left = unique.First().Point;
        var right = unique.Last().Point;

        foreach (var layer in LayerSections.Where(layer => layer.VisibleInSection))
        {
            var topLeft = new Point3D(left.X, left.Y, CoreArea.CoreTopElevation + layer.TopOffset);
            var topRight = new Point3D(right.X, right.Y, CoreArea.CoreTopElevation + layer.TopOffset);
            var bottomLeft = new Point3D(left.X, left.Y, CoreArea.CoreTopElevation + layer.BottomOffset);
            var bottomRight = new Point3D(right.X, right.Y, CoreArea.CoreTopElevation + layer.BottomOffset);

            yield return new Line3D(topLeft, topRight);
            yield return new Line3D(bottomLeft, bottomRight);
            yield return new Line3D(topLeft, bottomLeft);
            yield return new Line3D(topRight, bottomRight);
        }
    }

    private static Point3D? LineIntersection2D(Line3D line1, Line3D line2)
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
