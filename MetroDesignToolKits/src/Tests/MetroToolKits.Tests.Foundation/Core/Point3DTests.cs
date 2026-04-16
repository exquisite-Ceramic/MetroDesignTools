using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Tests.Foundation.Core;

public class Point3DTests
{
    [Fact]
    public void DistanceTo_SamePoint_ReturnsZero()
    {
        var p = new Point3D(1, 2, 3);
        p.DistanceTo(p).Should().BeApproximately(0, 1e-10);
    }

    [Fact]
    public void DistanceTo_KnownDistance_ReturnsCorrect()
    {
        var p1 = new Point3D(0, 0, 0);
        var p2 = new Point3D(3, 4, 0);
        p1.DistanceTo(p2).Should().BeApproximately(5.0, 1e-10);
    }

    [Fact]
    public void DistanceTo_3D_ReturnsCorrect()
    {
        var p1 = new Point3D(0, 0, 0);
        var p2 = new Point3D(1, 1, 1);
        p1.DistanceTo(p2).Should().BeApproximately(Math.Sqrt(3), 1e-10);
    }

    [Fact]
    public void Add_TwoPoints_ReturnsSum()
    {
        var p1 = new Point3D(1, 2, 3);
        var p2 = new Point3D(4, 5, 6);
        (p1 + p2).Should().Be(new Point3D(5, 7, 9));
    }

    [Fact]
    public void Subtract_TwoPoints_ReturnsDifference()
    {
        var p1 = new Point3D(5, 7, 9);
        var p2 = new Point3D(1, 2, 3);
        (p1 - p2).Should().Be(new Point3D(4, 5, 6));
    }

    [Fact]
    public void Multiply_ByScalar_ReturnsScaled()
    {
        var p = new Point3D(1, 2, 3);
        (p * 2.0).Should().Be(new Point3D(2, 4, 6));
    }

    [Fact]
    public void Origin_IsZeroZeroZero()
    {
        Point3D.Origin.Should().Be(new Point3D(0, 0, 0));
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var p1 = new Point3D(1.0, 2.0, 3.0);
        var p2 = new Point3D(1.0, 2.0, 3.0);
        p1.Equals(p2).Should().BeTrue();
    }
}
