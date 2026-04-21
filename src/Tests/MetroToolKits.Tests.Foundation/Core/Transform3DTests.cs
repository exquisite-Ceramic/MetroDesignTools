using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Tests.Foundation.Core;

public class Transform3DTests
{
    private static void AssertPointApprox(Point3D actual, Point3D expected, double tolerance = 1e-6)
    {
        actual.X.Should().BeApproximately(expected.X, tolerance);
        actual.Y.Should().BeApproximately(expected.Y, tolerance);
        actual.Z.Should().BeApproximately(expected.Z, tolerance);
    }

    // ── Identity ──────────────────────────────────────────────────────────────

    [Fact]
    public void Identity_TransformPoint_ReturnsSamePoint()
    {
        var p = new Point3D(3, 5, 7);
        AssertPointApprox(Transform3D.Identity.Transform(p), p);
    }

    // ── Translation ───────────────────────────────────────────────────────────

    [Fact]
    public void Translation_ShiftsPoint()
    {
        var t = Transform3D.Translation(10, 20, 30);
        AssertPointApprox(t.Transform(new Point3D(1, 2, 3)), new Point3D(11, 22, 33));
    }

    [Fact]
    public void Translation_DoesNotAffectVector()
    {
        var t = Transform3D.Translation(10, 20, 30);
        var v = new Vector3D(1, 0, 0);
        var r = t.Transform(v);
        r.X.Should().BeApproximately(1, 1e-6);
        r.Y.Should().BeApproximately(0, 1e-6);
        r.Z.Should().BeApproximately(0, 1e-6);
    }

    // ── Scale ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Scale_ScalesPoint()
    {
        var t = Transform3D.Scale(2, 3, 4);
        AssertPointApprox(t.Transform(new Point3D(1, 1, 1)), new Point3D(2, 3, 4));
    }

    // ── AlignPoints ───────────────────────────────────────────────────────────

    [Fact]
    public void AlignPoints_IdentityAlignment_ReturnsSamePoint()
    {
        var o = Point3D.Origin;
        var x = new Point3D(1, 0, 0);
        var y = new Point3D(0, 1, 0);
        var t = Transform3D.AlignPoints(o, x, y, o, x, y);
        AssertPointApprox(t.Transform(new Point3D(2, 3, 0)), new Point3D(2, 3, 0));
    }

    [Fact]
    public void AlignPoints_PureTranslation_ShiftsCorrectly()
    {
        var srcO = Point3D.Origin;
        var srcX = new Point3D(1, 0, 0);
        var srcY = new Point3D(0, 1, 0);
        var dstO = new Point3D(5, 5, 0);
        var dstX = new Point3D(6, 5, 0);
        var dstY = new Point3D(5, 6, 0);

        var t = Transform3D.AlignPoints(srcO, srcX, srcY, dstO, dstX, dstY);
        // 点 (1,0,0) 在源系中，对应目标系中 (6,5,0)
        AssertPointApprox(t.Transform(new Point3D(1, 0, 0)), new Point3D(6, 5, 0));
    }

    [Fact]
    public void AlignPoints_NonZeroSourceOrigin_UsesSourceBasisCorrectly()
    {
        var srcO = new Point3D(10, 10, 0);
        var srcX = new Point3D(20, 10, 0);
        var srcY = new Point3D(10, 20, 0);
        var dstO = new Point3D(100, 200, 0);
        var dstX = new Point3D(110, 200, 0);
        var dstY = new Point3D(100, 210, 0);

        var t = Transform3D.AlignPoints(srcO, srcX, srcY, dstO, dstX, dstY);

        AssertPointApprox(t.Transform(srcO), dstO);
        AssertPointApprox(t.Transform(new Point3D(15, 10, 0)), new Point3D(105, 200, 0));
    }

    [Fact]
    public void AlignPoints_RotationMapping_RotatesCorrectly()
    {
        var srcO = Point3D.Origin;
        var srcX = new Point3D(1, 0, 0);
        var srcY = new Point3D(0, 1, 0);
        var dstO = Point3D.Origin;
        var dstX = new Point3D(0, 1, 0);
        var dstY = new Point3D(-1, 0, 0);

        var t = Transform3D.AlignPoints(srcO, srcX, srcY, dstO, dstX, dstY);

        AssertPointApprox(t.Transform(new Point3D(2, 0, 0)), new Point3D(0, 2, 0));
        AssertPointApprox(t.Transform(new Point3D(0, 3, 0)), new Point3D(-3, 0, 0));
    }

    [Fact]
    public void AlignPoints_CollinearPoints_ThrowsArgumentException()
    {
        var act = () => Transform3D.AlignPoints(
            Point3D.Origin,
            new Point3D(1, 0, 0),
            new Point3D(2, 0, 0),
            Point3D.Origin,
            new Point3D(1, 0, 0),
            new Point3D(0, 1, 0));

        act.Should().Throw<ArgumentException>();
    }

    // ── Multiply ──────────────────────────────────────────────────────────────

    [Fact]
    public void Multiply_TwoTranslations_CombinesCorrectly()
    {
        var t1 = Transform3D.Translation(1, 0, 0);
        var t2 = Transform3D.Translation(0, 2, 0);
        var combined = t1.Multiply(t2);
        AssertPointApprox(combined.Transform(Point3D.Origin), new Point3D(1, 2, 0));
    }
}
