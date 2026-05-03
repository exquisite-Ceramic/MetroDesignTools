using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections.Hatching;

public static class HatchBoundaryNormalizer
{
    public static HatchBoundaryValidationResult Normalize(
        IEnumerable<Point3D> boundary,
        double epsilon = 1e-6,
        double areaEpsilon = 1e-3)
    {
        if (boundary is null)
        {
            return Invalid(
                HatchBoundaryIssueCode.TooFewPoints,
                "Boundary must contain at least three points.");
        }

        var source = boundary.ToList();
        if (source.Count < 3)
        {
            return Invalid(
                HatchBoundaryIssueCode.TooFewPoints,
                "Boundary must contain at least three points.");
        }

        if (source.Any(static point => !IsFinite(point)))
        {
            return Invalid(
                HatchBoundaryIssueCode.NonFiniteCoordinate,
                "Boundary contains a non-finite coordinate.");
        }

        var distinctPath = RemoveConsecutiveDuplicates(source, epsilon);
        if (distinctPath.Count > 1 && AreSamePoint2D(distinctPath[0], distinctPath[^1], epsilon))
        {
            distinctPath.RemoveAt(distinctPath.Count - 1);
        }

        if (distinctPath.Count < 3)
        {
            return Invalid(
                HatchBoundaryIssueCode.TooFewDistinctPoints,
                "Boundary must contain at least three distinct points.");
        }

        if (HasGlobalDuplicatePoint(distinctPath, epsilon))
        {
            return Invalid(
                HatchBoundaryIssueCode.DuplicatePoint,
                "Boundary contains a repeated non-consecutive point.");
        }

        var normalized = NormalizePointOrder(distinctPath);
        if (HasDegenerateEdge(normalized, epsilon))
        {
            return Invalid(
                HatchBoundaryIssueCode.DegenerateEdge,
                "Boundary contains a degenerate edge.");
        }

        var area = SignedArea(normalized);
        if (Math.Abs(area) <= areaEpsilon)
        {
            return Invalid(
                HatchBoundaryIssueCode.ZeroArea,
                "Boundary area is too small.");
        }

        if (HasSelfIntersection(normalized, epsilon))
        {
            return Invalid(
                HatchBoundaryIssueCode.SelfIntersection,
                "Boundary edges intersect each other.");
        }

        if (area < 0)
        {
            normalized.Reverse();
        }

        return new HatchBoundaryValidationResult(
            true,
            HatchBoundaryIssueCode.None,
            "Boundary is valid.",
            normalized.ToArray());
    }

    private static HatchBoundaryValidationResult Invalid(HatchBoundaryIssueCode code, string message)
        => new(false, code, message, Array.Empty<Point3D>());

    private static List<Point3D> RemoveConsecutiveDuplicates(IReadOnlyList<Point3D> points, double epsilon)
    {
        var result = new List<Point3D>(points.Count);
        foreach (var point in points)
        {
            if (result.Count == 0 || !AreSamePoint2D(result[^1], point, epsilon))
            {
                result.Add(point);
            }
        }

        return result;
    }

    private static List<Point3D> NormalizePointOrder(IReadOnlyList<Point3D> points)
    {
        if (points.Count != 4)
        {
            return points.ToList();
        }

        var centerX = points.Average(static point => point.X);
        var centerY = points.Average(static point => point.Y);
        return points
            .OrderBy(point => Math.Atan2(point.Y - centerY, point.X - centerX))
            .ThenBy(point => SquaredDistance2D(point, centerX, centerY))
            .ToList();
    }

    private static bool IsFinite(Point3D point)
        => double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private static bool HasGlobalDuplicatePoint(IReadOnlyList<Point3D> points, double epsilon)
    {
        for (var i = 0; i < points.Count; i++)
        {
            for (var j = i + 1; j < points.Count; j++)
            {
                if (AreSamePoint2D(points[i], points[j], epsilon))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasDegenerateEdge(IReadOnlyList<Point3D> points, double epsilon)
    {
        for (var i = 0; i < points.Count; i++)
        {
            if (AreSamePoint2D(points[i], points[(i + 1) % points.Count], epsilon))
            {
                return true;
            }
        }

        return false;
    }

    private static double SignedArea(IReadOnlyList<Point3D> points)
    {
        var area = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }

        return area / 2.0;
    }

    private static bool HasSelfIntersection(IReadOnlyList<Point3D> points, double epsilon)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var a1 = points[i];
            var a2 = points[(i + 1) % points.Count];
            for (var j = i + 1; j < points.Count; j++)
            {
                if (AreAdjacentEdges(i, j, points.Count))
                {
                    continue;
                }

                var b1 = points[j];
                var b2 = points[(j + 1) % points.Count];
                if (SegmentsIntersect(a1, a2, b1, b2, epsilon))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AreAdjacentEdges(int first, int second, int count)
        => first == second ||
           Math.Abs(first - second) == 1 ||
           (first == 0 && second == count - 1);

    private static bool SegmentsIntersect(Point3D a1, Point3D a2, Point3D b1, Point3D b2, double epsilon)
    {
        var o1 = Cross(a1, a2, b1);
        var o2 = Cross(a1, a2, b2);
        var o3 = Cross(b1, b2, a1);
        var o4 = Cross(b1, b2, a2);

        if (HasOppositeSign(o1, o2, epsilon) && HasOppositeSign(o3, o4, epsilon))
        {
            return true;
        }

        return IsCollinearAndOnSegment(a1, b1, a2, o1, epsilon) ||
               IsCollinearAndOnSegment(a1, b2, a2, o2, epsilon) ||
               IsCollinearAndOnSegment(b1, a1, b2, o3, epsilon) ||
               IsCollinearAndOnSegment(b1, a2, b2, o4, epsilon);
    }

    private static bool HasOppositeSign(double first, double second, double epsilon)
        => (first > epsilon && second < -epsilon) || (first < -epsilon && second > epsilon);

    private static bool IsCollinearAndOnSegment(
        Point3D start,
        Point3D point,
        Point3D end,
        double cross,
        double epsilon)
        => Math.Abs(cross) <= epsilon &&
           point.X >= Math.Min(start.X, end.X) - epsilon &&
           point.X <= Math.Max(start.X, end.X) + epsilon &&
           point.Y >= Math.Min(start.Y, end.Y) - epsilon &&
           point.Y <= Math.Max(start.Y, end.Y) + epsilon;

    private static double Cross(Point3D origin, Point3D first, Point3D second)
        => ((first.X - origin.X) * (second.Y - origin.Y)) -
           ((first.Y - origin.Y) * (second.X - origin.X));

    private static bool AreSamePoint2D(Point3D first, Point3D second, double epsilon)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return (dx * dx) + (dy * dy) <= epsilon * epsilon;
    }

    private static double SquaredDistance2D(Point3D point, double x, double y)
    {
        var dx = point.X - x;
        var dy = point.Y - y;
        return (dx * dx) + (dy * dy);
    }
}
