namespace MetroToolKits.Foundation.Core.Geometry;

/// <summary>
/// 三维线段
/// </summary>
public readonly struct Line3D
{
    public Point3D Start { get; }
    public Point3D End { get; }

    public Line3D(Point3D start, Point3D end)
    {
        Start = start;
        End = end;
    }

    public double Length => Start.DistanceTo(End);
    public Point3D MidPoint => new((Start.X + End.X) / 2, (Start.Y + End.Y) / 2, (Start.Z + End.Z) / 2);
    public Vector3D Direction => new(End.X - Start.X, End.Y - Start.Y, End.Z - Start.Z);

    /// <summary>
    /// 获取线段上指定参数位置的点 (t=0 为起点, t=1 为终点)
    /// </summary>
    public Point3D GetPointAt(double t) => new(
        Start.X + t * (End.X - Start.X),
        Start.Y + t * (End.Y - Start.Y),
        Start.Z + t * (End.Z - Start.Z));

    public override string ToString() => $"Line[{Start} -> {End}]";
}
