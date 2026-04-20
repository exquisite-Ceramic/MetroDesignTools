using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Tests.Foundation.Core;

public class Vector3DTests
{
    [Fact]
    public void Length_UnitX_ReturnsOne()
        => Vector3D.UnitX.Length.Should().BeApproximately(1.0, 1e-10);

    [Fact]
    public void Length_KnownVector_ReturnsCorrect()
        => new Vector3D(3, 4, 0).Length.Should().BeApproximately(5.0, 1e-10);

    [Fact]
    public void Normalized_NonZeroVector_HasLengthOne()
        => new Vector3D(3, 4, 0).Normalized.Length.Should().BeApproximately(1.0, 1e-10);

    [Fact]
    public void Normalized_ZeroVector_ReturnsZero()
        => Vector3D.Zero.Normalized.Should().Be(Vector3D.Zero);

    [Fact]
    public void Dot_PerpendicularVectors_ReturnsZero()
        => Vector3D.Dot(Vector3D.UnitX, Vector3D.UnitY).Should().BeApproximately(0, 1e-10);

    [Fact]
    public void Dot_SameVector_ReturnsLengthSquared()
    {
        var v = new Vector3D(2, 0, 0);
        Vector3D.Dot(v, v).Should().BeApproximately(4.0, 1e-10);
    }

    [Fact]
    public void Cross_XandY_ReturnsZ()
    {
        var r = Vector3D.Cross(Vector3D.UnitX, Vector3D.UnitY);
        r.X.Should().BeApproximately(0, 1e-10);
        r.Y.Should().BeApproximately(0, 1e-10);
        r.Z.Should().BeApproximately(1, 1e-10);
    }

    [Fact]
    public void Cross_ParallelVectors_ReturnsZeroLength()
        => Vector3D.Cross(Vector3D.UnitX, Vector3D.UnitX).Length.Should().BeApproximately(0, 1e-10);

    [Fact]
    public void Add_TwoVectors_ReturnsSum()
        => (new Vector3D(1, 2, 3) + new Vector3D(4, 5, 6)).Should().Be(new Vector3D(5, 7, 9));

    [Fact]
    public void Negate_Vector_ReturnsOpposite()
        => (-new Vector3D(1, -2, 3)).Should().Be(new Vector3D(-1, 2, -3));
}
