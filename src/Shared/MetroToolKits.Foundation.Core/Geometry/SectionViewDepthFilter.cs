namespace MetroToolKits.Foundation.Core.Geometry;

/// <summary>
/// Evaluates whether a footprint intersects the single-sided view-depth strip of a section line.
/// </summary>
public static class SectionViewDepthFilter
{
    private const double DefaultTolerance = 1e-6;

    public static bool IntersectsSingleSidedStrip(
        Line3D sectionLine,
        double viewDepth,
        Polygon3D? footprint,
        double tolerance = DefaultTolerance)
    {
        if (footprint == null || footprint.VertexCount == 0 || viewDepth <= tolerance)
        {
            return false;
        }

        var axis = new Vector3D(
            sectionLine.End.X - sectionLine.Start.X,
            sectionLine.End.Y - sectionLine.Start.Y,
            0);
        var sectionLength = axis.Length;
        if (sectionLength <= tolerance)
        {
            return false;
        }

        var horizontalAxis = axis.Normalized;
        var viewAxis = new Vector3D(-horizontalAxis.Y, horizontalAxis.X, 0);
        var polygon = footprint.Vertices
            .Select(point => ToStripCoordinates(sectionLine.Start, horizontalAxis, viewAxis, point))
            .ToList();

        if (polygon.Any(point => IsInsideStrip(point, sectionLength, viewDepth, tolerance)))
        {
            return true;
        }

        var stripCorners = new[]
        {
            new Point2D(0, 0),
            new Point2D(sectionLength, 0),
            new Point2D(sectionLength, viewDepth),
            new Point2D(0, viewDepth)
        };

        if (stripCorners.Any(corner => IsPointInsidePolygon(corner, polygon, tolerance)))
        {
            return true;
        }

        return Edges(polygon).Any(polygonEdge =>
            Edges(stripCorners).Any(stripEdge =>
                SegmentsIntersect(polygonEdge.Start, polygonEdge.End, stripEdge.Start, stripEdge.End, tolerance)));
    }

    private static Point2D ToStripCoordinates(
        Point3D origin,
        Vector3D horizontalAxis,
        Vector3D viewAxis,
        Point3D point)
    {
        var offset = new Vector3D(point.X - origin.X, point.Y - origin.Y, 0);
        return new Point2D(
            Vector3D.Dot(offset, horizontalAxis),
            Vector3D.Dot(offset, viewAxis));
    }

    private static bool IsInsideStrip(Point2D point, double sectionLength, double viewDepth, double tolerance)
        => point.Chainage >= -tolerance &&
           point.Chainage <= sectionLength + tolerance &&
           point.Depth >= -tolerance &&
           point.Depth <= viewDepth + tolerance;

    private static bool IsPointInsidePolygon(Point2D point, IReadOnlyList<Point2D> polygon, double tolerance)
    {
        if (polygon.Count < 3)
        {
            return false;
        }

        if (Edges(polygon).Any(edge => IsPointOnSegment(point, edge.Start, edge.End, tolerance)))
        {
            return true;
        }

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            var crosses = (pi.Depth > point.Depth) != (pj.Depth > point.Depth);
            if (!crosses)
            {
                continue;
            }

            var intersectionChainage =
                (pj.Chainage - pi.Chainage) * (point.Depth - pi.Depth) /
                (pj.Depth - pi.Depth) +
                pi.Chainage;
            if (point.Chainage < intersectionChainage)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static IEnumerable<Segment2D> Edges(IReadOnlyList<Point2D> points)
    {
        if (points.Count < 2)
        {
            yield break;
        }

        for (var i = 0; i < points.Count; i++)
        {
            yield return new Segment2D(points[i], points[(i + 1) % points.Count]);
        }
    }

    private static bool SegmentsIntersect(Point2D a, Point2D b, Point2D c, Point2D d, double tolerance)
    {
        var o1 = Orientation(a, b, c);
        var o2 = Orientation(a, b, d);
        var o3 = Orientation(c, d, a);
        var o4 = Orientation(c, d, b);

        if (Math.Abs(o1) <= tolerance && IsPointOnSegment(c, a, b, tolerance)) return true;
        if (Math.Abs(o2) <= tolerance && IsPointOnSegment(d, a, b, tolerance)) return true;
        if (Math.Abs(o3) <= tolerance && IsPointOnSegment(a, c, d, tolerance)) return true;
        if (Math.Abs(o4) <= tolerance && IsPointOnSegment(b, c, d, tolerance)) return true;

        return (o1 > tolerance && o2 < -tolerance || o1 < -tolerance && o2 > tolerance) &&
               (o3 > tolerance && o4 < -tolerance || o3 < -tolerance && o4 > tolerance);
    }

    private static double Orientation(Point2D a, Point2D b, Point2D c)
        => (b.Chainage - a.Chainage) * (c.Depth - a.Depth) -
           (b.Depth - a.Depth) * (c.Chainage - a.Chainage);

    private static bool IsPointOnSegment(Point2D point, Point2D start, Point2D end, double tolerance)
        => Math.Abs(Orientation(start, end, point)) <= tolerance &&
           point.Chainage >= Math.Min(start.Chainage, end.Chainage) - tolerance &&
           point.Chainage <= Math.Max(start.Chainage, end.Chainage) + tolerance &&
           point.Depth >= Math.Min(start.Depth, end.Depth) - tolerance &&
           point.Depth <= Math.Max(start.Depth, end.Depth) + tolerance;

    private readonly record struct Point2D(double Chainage, double Depth);

    private readonly record struct Segment2D(Point2D Start, Point2D End);
}
