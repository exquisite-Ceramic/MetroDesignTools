using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Tests.Foundation.Core;

public class Line3DTests
{
    [Fact]
    public void Length_HorizontalLine_ReturnsCorrect()
        => new Line3D(new Point3D(0, 0, 0), new Point3D(5, 0, 0)).Length
            .Should().BeApproximately(5.0, 1e-10);

    [Fact]
    public void Length_DiagonalLine_ReturnsCorrect()
        => new Line3D(new Point3D(0, 0, 0), new Point3D(3, 4, 0)).Length
            .Should().BeApproximately(5.0, 1e-10);

    [Fact]
    public void MidPoint_ReturnsCenter()
        => new Line3D(new Point3D(0, 0, 0), new Point3D(4, 0, 0)).MidPoint
            .Should().Be(new Point3D(2, 0, 0));

    [Fact]
    public void GetPointAt_T0_ReturnsStart()
        => new Line3D(new Point3D(1, 2, 3), new Point3D(4, 5, 6)).GetPointAt(0)
            .Should().Be(new Point3D(1, 2, 3));

    [Fact]
    public void GetPointAt_T1_ReturnsEnd()
        => new Line3D(new Point3D(1, 2, 3), new Point3D(4, 5, 6)).GetPointAt(1)
            .Should().Be(new Point3D(4, 5, 6));

    [Fact]
    public void GetPointAt_T05_ReturnsMidPoint()
    {
        var mid = new Line3D(new Point3D(0, 0, 0), new Point3D(4, 0, 0)).GetPointAt(0.5);
        mid.X.Should().BeApproximately(2.0, 1e-10);
    }
}
