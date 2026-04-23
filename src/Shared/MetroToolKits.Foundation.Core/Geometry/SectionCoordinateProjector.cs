namespace MetroToolKits.Foundation.Core.Geometry;

/// <summary>
/// 将世界坐标中的剖面几何投影到局部剖面坐标系。
/// 局部坐标约定：
/// X = 沿剖切线的链长
/// Y = 标高
/// Z = 0
/// </summary>
public readonly struct SectionCoordinateProjector
{
    private readonly Point3D _origin;
    private readonly Vector3D _horizontalAxis;

    public SectionCoordinateProjector(Line3D sectionLine)
    {
        _origin = sectionLine.Start;

        var horizontal = new Vector3D(
            sectionLine.End.X - sectionLine.Start.X,
            sectionLine.End.Y - sectionLine.Start.Y,
            0);

        if (horizontal.Length <= 1e-9)
            throw new ArgumentException("Section line must have a non-zero XY length.", nameof(sectionLine));

        _horizontalAxis = horizontal.Normalized;
        SectionLength = horizontal.Length;
    }

    public double SectionLength { get; }

    public double GetChainage(Point3D point)
    {
        var offset = new Vector3D(point.X - _origin.X, point.Y - _origin.Y, 0);
        return Vector3D.Dot(offset, _horizontalAxis);
    }

    public Point3D ProjectPoint(Point3D point)
        => new(GetChainage(point), point.Z, 0);

    public Line3D ProjectLine(Line3D line)
        => new(ProjectPoint(line.Start), ProjectPoint(line.End));

    public static double GetChainage(Line3D sectionLine, Point3D point)
        => new SectionCoordinateProjector(sectionLine).GetChainage(point);

    public static Point3D ProjectPoint(Line3D sectionLine, Point3D point)
        => new SectionCoordinateProjector(sectionLine).ProjectPoint(point);

    public static Line3D ProjectLine(Line3D sectionLine, Line3D line)
        => new SectionCoordinateProjector(sectionLine).ProjectLine(line);
}
