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
        var (srcXAxis, srcYAxis, srcZAxis) = BuildBasis(srcOrigin, srcX, srcY);
        var (dstXAxis, dstYAxis, dstZAxis) = BuildBasis(dstOrigin, dstX, dstY);

        var srcBasis = new[,]
        {
            { srcXAxis.X, srcYAxis.X, srcZAxis.X },
            { srcXAxis.Y, srcYAxis.Y, srcZAxis.Y },
            { srcXAxis.Z, srcYAxis.Z, srcZAxis.Z }
        };

        var dstBasis = new[,]
        {
            { dstXAxis.X, dstYAxis.X, dstZAxis.X },
            { dstXAxis.Y, dstYAxis.Y, dstZAxis.Y },
            { dstXAxis.Z, dstYAxis.Z, dstZAxis.Z }
        };

        var srcBasisInverse = Transpose(srcBasis);
        var rotation = Multiply3x3(dstBasis, srcBasisInverse);
        var srcOriginVector = new[] { srcOrigin.X, srcOrigin.Y, srcOrigin.Z };
        var rotatedSourceOrigin = Multiply3x3Vector(rotation, srcOriginVector);

        var t = new Transform3D();
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                t._matrix[row, col] = rotation[row, col];
            }
        }

        t._matrix[0, 3] = dstOrigin.X - rotatedSourceOrigin[0];
        t._matrix[1, 3] = dstOrigin.Y - rotatedSourceOrigin[1];
        t._matrix[2, 3] = dstOrigin.Z - rotatedSourceOrigin[2];

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

    private static (Vector3D XAxis, Vector3D YAxis, Vector3D ZAxis) BuildBasis(
        Point3D origin,
        Point3D xPoint,
        Point3D yPoint)
    {
        const double tolerance = 1e-9;

        var xVector = new Vector3D(
            xPoint.X - origin.X,
            xPoint.Y - origin.Y,
            xPoint.Z - origin.Z);

        var rawYVector = new Vector3D(
            yPoint.X - origin.X,
            yPoint.Y - origin.Y,
            yPoint.Z - origin.Z);

        if (xVector.Length <= tolerance || rawYVector.Length <= tolerance)
            throw new ArgumentException("对齐点无效：原点到方向点的距离必须大于 0。");

        var xAxis = xVector.Normalized;
        var zAxis = Vector3D.Cross(xAxis, rawYVector);
        if (zAxis.Length <= tolerance)
            throw new ArgumentException("对齐点无效：三点不能共线。");

        zAxis = zAxis.Normalized;
        var yAxis = Vector3D.Cross(zAxis, xAxis).Normalized;

        return (xAxis, yAxis, zAxis);
    }

    private static double[,] Transpose(double[,] matrix)
    {
        var result = new double[3, 3];
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                result[row, col] = matrix[col, row];
            }
        }

        return result;
    }

    private static double[,] Multiply3x3(double[,] left, double[,] right)
    {
        var result = new double[3, 3];
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                double sum = 0;
                for (int k = 0; k < 3; k++)
                {
                    sum += left[row, k] * right[k, col];
                }

                result[row, col] = sum;
            }
        }

        return result;
    }

    private static double[] Multiply3x3Vector(double[,] matrix, double[] vector)
    {
        var result = new double[3];
        for (int row = 0; row < 3; row++)
        {
            result[row] =
                matrix[row, 0] * vector[0] +
                matrix[row, 1] * vector[1] +
                matrix[row, 2] * vector[2];
        }

        return result;
    }
}
