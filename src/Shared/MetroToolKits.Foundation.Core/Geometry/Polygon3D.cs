namespace MetroToolKits.Foundation.Core.Geometry;

/// <summary>
/// 三维多边形
/// </summary>
public sealed class Polygon3D
{
    private readonly List<Point3D> _vertices;

    public IReadOnlyList<Point3D> Vertices => _vertices;
    public bool IsClosed { get; }

    public Polygon3D(IEnumerable<Point3D> vertices, bool isClosed = true)
    {
        _vertices = new List<Point3D>(vertices);
        IsClosed = isClosed;
    }

    public int VertexCount => _vertices.Count;

    /// <summary>
    /// 获取多边形所在平面的法向量
    /// </summary>
    public Vector3D? GetNormal()
    {
        if (_vertices.Count < 3) return null;

        var v1 = new Vector3D(
            _vertices[1].X - _vertices[0].X,
            _vertices[1].Y - _vertices[0].Y,
            _vertices[1].Z - _vertices[0].Z);

        var v2 = new Vector3D(
            _vertices[2].X - _vertices[0].X,
            _vertices[2].Y - _vertices[0].Y,
            _vertices[2].Z - _vertices[0].Z);

        return Vector3D.Cross(v1, v2).Normalized;
    }

    /// <summary>
    /// 计算多边形周长
    /// </summary>
    public double GetPerimeter()
    {
        double perimeter = 0;
        for (int i = 0; i < _vertices.Count - 1; i++)
        {
            perimeter += _vertices[i].DistanceTo(_vertices[i + 1]);
        }
        if (IsClosed && _vertices.Count > 1)
        {
            perimeter += _vertices[^1].DistanceTo(_vertices[0]);
        }
        return perimeter;
    }
}
