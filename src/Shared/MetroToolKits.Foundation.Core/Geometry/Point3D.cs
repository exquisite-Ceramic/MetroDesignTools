using System.Text.Json.Serialization;

namespace MetroToolKits.Foundation.Core.Geometry;

/// <summary>
/// 三维点
/// </summary>
public readonly struct Point3D : IEquatable<Point3D>
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    [JsonConstructor]
    public Point3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Point3D Origin => new(0, 0, 0);

    public static Point3D operator +(Point3D a, Point3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Point3D operator -(Point3D a, Point3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Point3D operator *(Point3D p, double scalar) => new(p.X * scalar, p.Y * scalar, p.Z * scalar);

    public double DistanceTo(Point3D other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public bool Equals(Point3D other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    public override bool Equals(object? obj) => obj is Point3D other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}
