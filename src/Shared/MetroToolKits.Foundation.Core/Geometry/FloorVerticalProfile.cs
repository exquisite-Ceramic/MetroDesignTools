namespace MetroToolKits.Foundation.Core.Geometry;

/// <summary>
/// 楼层竖向轮廓边界。
/// </summary>
public readonly record struct ProfileEdge(double StartX, double EndX, double StartY, double EndY)
{
    public double Evaluate(double x)
    {
        if (Math.Abs(EndX - StartX) <= 1e-9)
        {
            return StartY;
        }

        if (x <= StartX)
        {
            return StartY;
        }

        if (x >= EndX)
        {
            return EndY;
        }

        var t = (x - StartX) / (EndX - StartX);
        return StartY + (EndY - StartY) * t;
    }

    public Line3D ToLine3D()
        => new(
            new Point3D(StartX, StartY, 0),
            new Point3D(EndX, EndY, 0));
}

/// <summary>
/// 楼层局部剖面竖向轮廓。
/// </summary>
public sealed class FloorVerticalProfile
{
    public required double SectionLength { get; init; }

    public required ProfileEdge BottomStructuralBottom { get; init; }

    public required ProfileEdge BottomStructuralTop { get; init; }

    public required ProfileEdge BottomBoundaryBottom { get; init; }

    public required ProfileEdge BottomBoundaryTop { get; init; }

    public required ProfileEdge TopStructuralBottom { get; init; }

    public required ProfileEdge TopStructuralTop { get; init; }

    public required ProfileEdge TopBoundaryBottom { get; init; }

    public required ProfileEdge TopBoundaryTop { get; init; }

    /// <summary>
    /// 楼层边界板在剖面中的最终输出线。
    /// </summary>
    public IReadOnlyList<Line3D> BoundaryLines { get; init; } = Array.Empty<Line3D>();

    public double GetBottomStructuralBottom(double x) => BottomStructuralBottom.Evaluate(x);

    public double GetBottomStructuralTop(double x) => BottomStructuralTop.Evaluate(x);

    public double GetBottomBoundaryBottom(double x) => BottomBoundaryBottom.Evaluate(x);

    public double GetBottomBoundaryTop(double x) => BottomBoundaryTop.Evaluate(x);

    public double GetTopStructuralBottom(double x) => TopStructuralBottom.Evaluate(x);

    public double GetTopStructuralTop(double x) => TopStructuralTop.Evaluate(x);

    public double GetTopBoundaryBottom(double x) => TopBoundaryBottom.Evaluate(x);

    public double GetTopBoundaryTop(double x) => TopBoundaryTop.Evaluate(x);
}
