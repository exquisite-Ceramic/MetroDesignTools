namespace MetroToolKits.Foundation.Core.Geometry;

/// <summary>
/// 三维变换矩阵
/// </summary>
public sealed class Transform3D
{
    private readonly double[,] _matrix;

    private Transform3D()
    {
        _matrix = new double[4, 4];
        SetIdentity();
    }

    private void SetIdentity()
    {
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                _matrix[i, j] = i == j ? 1 : 0;
    }

    public static Transform3D Identity => new();

    /// <summary>
    /// 创建平移变换
    /// </summary>
    public static Transform3D Translation(double dx, double dy, double dz)
    {
        var t = new Transform3D();
        t._matrix[0, 3] = dx;
        t._matrix[1, 3] = dy;
        t._matrix[2, 3] = dz;
        return t;
    }

    /// <summary>
    /// 创建缩放变换
    /// </summary>
    public static Transform3D Scale(double sx, double sy, double sz)
    {
        var t = new Transform3D();
        t._matrix[0, 0] = sx;
        t._matrix[1, 1] = sy;
        t._matrix[2, 2] = sz;
        return t;
    }

    /// <summary>
    /// 三点对齐变换 - 将源三点对齐到目标三点
    /// </summary>
    public static Transform3D AlignPoints(Point3D srcOrigin, Point3D srcX, Point3D srcY,
        Point3D dstOrigin, Point3D dstX, Point3D dstY)
    {
        // 计算源坐标系
        var srcXAxis = new Vector3D(srcX.X - srcOrigin.X, srcX.Y - srcOrigin.Y, srcX.Z - srcOrigin.Z).Normalized;
        var srcYAxis = new Vector3D(srcY.X - srcOrigin.X, srcY.Y - srcOrigin.Y, srcY.Z - srcOrigin.Z).Normalized;
        var srcZAxis = Vector3D.Cross(srcXAxis, srcYAxis).Normalized;

        // 计算目标坐标系
        var dstXAxis = new Vector3D(dstX.X - dstOrigin.X, dstX.Y - dstOrigin.Y, dstX.Z - dstOrigin.Z).Normalized;
        var dstYAxis = new Vector3D(dstY.X - dstOrigin.X, dstY.Y - dstOrigin.Y, dstY.Z - dstOrigin.Z).Normalized;
        var dstZAxis = Vector3D.Cross(dstXAxis, dstYAxis).Normalized;

        // 构建变换矩阵
        var t = new Transform3D();
        t._matrix[0, 0] = dstXAxis.X; t._matrix[0, 1] = dstYAxis.X; t._matrix[0, 2] = dstZAxis.X;
        t._matrix[1, 0] = dstXAxis.Y; t._matrix[1, 1] = dstYAxis.Y; t._matrix[1, 2] = dstZAxis.Y;
        t._matrix[2, 0] = dstXAxis.Z; t._matrix[2, 1] = dstYAxis.Z; t._matrix[2, 2] = dstZAxis.Z;
        t._matrix[0, 3] = dstOrigin.X;
        t._matrix[1, 3] = dstOrigin.Y;
        t._matrix[2, 3] = dstOrigin.Z;

        return t;
    }

    /// <summary>
    /// 变换点
    /// </summary>
    public Point3D Transform(Point3D p)
    {
        double x = _matrix[0, 0] * p.X + _matrix[0, 1] * p.Y + _matrix[0, 2] * p.Z + _matrix[0, 3];
        double y = _matrix[1, 0] * p.X + _matrix[1, 1] * p.Y + _matrix[1, 2] * p.Z + _matrix[1, 3];
        double z = _matrix[2, 0] * p.X + _matrix[2, 1] * p.Y + _matrix[2, 2] * p.Z + _matrix[2, 3];
        return new Point3D(x, y, z);
    }

    /// <summary>
    /// 变换向量
    /// </summary>
    public Vector3D Transform(Vector3D v)
    {
        double x = _matrix[0, 0] * v.X + _matrix[0, 1] * v.Y + _matrix[0, 2] * v.Z;
        double y = _matrix[1, 0] * v.X + _matrix[1, 1] * v.Y + _matrix[1, 2] * v.Z;
        double z = _matrix[2, 0] * v.X + _matrix[2, 1] * v.Y + _matrix[2, 2] * v.Z;
        return new Vector3D(x, y, z);
    }

    /// <summary>
    /// 组合变换
    /// </summary>
    public Transform3D Multiply(Transform3D other)
    {
        var result = new Transform3D();
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                double sum = 0;
                for (int k = 0; k < 4; k++)
                    sum += _matrix[i, k] * other._matrix[k, j];
                result._matrix[i, j] = sum;
            }
        }
        return result;
    }
}
