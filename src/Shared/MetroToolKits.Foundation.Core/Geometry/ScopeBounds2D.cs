namespace MetroToolKits.Foundation.Core.Geometry;

/// <summary>
/// 二维轴对齐范围框。
/// </summary>
public readonly struct ScopeBounds2D : IEquatable<ScopeBounds2D>
{
    public const double DefaultTolerance = 1e-6;

    public double MinX { get; init; }
    public double MinY { get; init; }
    public double MaxX { get; init; }
    public double MaxY { get; init; }

    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;

    public bool IsValid(double tolerance = DefaultTolerance)
    {
        return
            !double.IsNaN(MinX) &&
            !double.IsNaN(MinY) &&
            !double.IsNaN(MaxX) &&
            !double.IsNaN(MaxY) &&
            !double.IsInfinity(MinX) &&
            !double.IsInfinity(MinY) &&
            !double.IsInfinity(MaxX) &&
            !double.IsInfinity(MaxY) &&
            Width > tolerance &&
            Height > tolerance;
    }

    public bool Intersects(ScopeBounds2D other, double tolerance = DefaultTolerance)
    {
        if (!IsValid(tolerance) || !other.IsValid(tolerance))
            return false;

        return
            MaxX >= other.MinX + tolerance &&
            other.MaxX >= MinX + tolerance &&
            MaxY >= other.MinY + tolerance &&
            other.MaxY >= MinY + tolerance;
    }

    public ScopeBounds2D? Intersect(ScopeBounds2D other, double tolerance = DefaultTolerance)
    {
        if (!Intersects(other, tolerance))
            return null;

        var intersection = new ScopeBounds2D
        {
            MinX = Math.Max(MinX, other.MinX),
            MinY = Math.Max(MinY, other.MinY),
            MaxX = Math.Min(MaxX, other.MaxX),
            MaxY = Math.Min(MaxY, other.MaxY)
        };

        return intersection.IsValid(tolerance) ? intersection : null;
    }

    public IReadOnlyList<Point3D> GetCornerPoints(double z = 0)
    {
        return
        [
            new Point3D(MinX, MinY, z),
            new Point3D(MaxX, MinY, z),
            new Point3D(MaxX, MaxY, z),
            new Point3D(MinX, MaxY, z)
        ];
    }

    public static ScopeBounds2D FromPoints(IEnumerable<Point3D> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        using var enumerator = points.GetEnumerator();
        if (!enumerator.MoveNext())
            throw new ArgumentException("范围框至少需要一个点。", nameof(points));

        var first = enumerator.Current;
        var minX = first.X;
        var minY = first.Y;
        var maxX = first.X;
        var maxY = first.Y;

        while (enumerator.MoveNext())
        {
            var point = enumerator.Current;
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        return new ScopeBounds2D
        {
            MinX = minX,
            MinY = minY,
            MaxX = maxX,
            MaxY = maxY
        };
    }

    public static ScopeBounds2D? FromPolygon(Polygon3D? polygon)
    {
        if (polygon == null || polygon.VertexCount == 0)
            return null;

        return FromPoints(polygon.Vertices);
    }

    public bool Equals(ScopeBounds2D other)
    {
        return
            MinX.Equals(other.MinX) &&
            MinY.Equals(other.MinY) &&
            MaxX.Equals(other.MaxX) &&
            MaxY.Equals(other.MaxY);
    }

    public override bool Equals(object? obj) => obj is ScopeBounds2D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(MinX, MinY, MaxX, MaxY);

    public override string ToString() => $"[{MinX:F2},{MinY:F2}] - [{MaxX:F2},{MaxY:F2}]";
}
