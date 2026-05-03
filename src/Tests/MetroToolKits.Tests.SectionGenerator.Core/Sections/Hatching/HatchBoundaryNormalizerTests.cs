using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections.Hatching;

namespace MetroToolKits.Tests.SectionGenerator.Core.Sections.Hatching;

public class HatchBoundaryNormalizerTests
{
    [Fact]
    public void Normalize_ValidRectangle_ReturnsNormalizedBoundary()
    {
        var boundary = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(1000, 0, 0),
            new Point3D(1000, 500, 0),
            new Point3D(0, 500, 0)
        };

        var result = HatchBoundaryNormalizer.Normalize(boundary);

        result.IsValid.Should().BeTrue();
        result.IssueCode.Should().Be(HatchBoundaryIssueCode.None);
        result.NormalizedBoundary.Should().HaveCount(4);
        Math.Abs(SignedArea(result.NormalizedBoundary)).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Normalize_ClosedRectangle_RemovesDuplicateClosingPoint()
    {
        var boundary = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(1000, 0, 0),
            new Point3D(1000, 500, 0),
            new Point3D(0, 500, 0),
            new Point3D(0, 0, 0)
        };

        var result = HatchBoundaryNormalizer.Normalize(boundary);

        result.IsValid.Should().BeTrue();
        result.NormalizedBoundary.Should().HaveCount(4);
    }

    [Fact]
    public void Normalize_ConsecutiveDuplicatePoint_RemovesDuplicateAndKeepsBoundaryValid()
    {
        var boundary = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(1000, 0, 0),
            new Point3D(1000, 0, 0),
            new Point3D(1000, 500, 0),
            new Point3D(0, 500, 0)
        };

        var result = HatchBoundaryNormalizer.Normalize(boundary);

        result.IsValid.Should().BeTrue();
        result.NormalizedBoundary.Should().HaveCount(4);
    }

    [Fact]
    public void Normalize_TooFewPoints_ReturnsInvalid()
    {
        var boundary = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(1000, 0, 0)
        };

        var result = HatchBoundaryNormalizer.Normalize(boundary);

        result.IsValid.Should().BeFalse();
        result.IssueCode.Should().BeOneOf(
            HatchBoundaryIssueCode.TooFewPoints,
            HatchBoundaryIssueCode.TooFewDistinctPoints);
    }

    [Fact]
    public void Normalize_NonFiniteCoordinate_ReturnsInvalid()
    {
        var boundary = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(double.NaN, 0, 0),
            new Point3D(1000, 500, 0),
            new Point3D(0, 500, double.PositiveInfinity)
        };

        var result = HatchBoundaryNormalizer.Normalize(boundary);

        result.IsValid.Should().BeFalse();
        result.IssueCode.Should().Be(HatchBoundaryIssueCode.NonFiniteCoordinate);
    }

    [Fact]
    public void Normalize_CollinearTriangle_ReturnsZeroArea()
    {
        var boundary = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(1000, 0, 0),
            new Point3D(2000, 0, 0)
        };

        var result = HatchBoundaryNormalizer.Normalize(boundary);

        result.IsValid.Should().BeFalse();
        result.IssueCode.Should().Be(HatchBoundaryIssueCode.ZeroArea);
    }

    [Fact]
    public void Normalize_UnorderedRectanglePoints_SortsIntoValidQuad()
    {
        var boundary = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(1000, 500, 0),
            new Point3D(1000, 0, 0),
            new Point3D(0, 500, 0)
        };

        var result = HatchBoundaryNormalizer.Normalize(boundary);

        result.IsValid.Should().BeTrue();
        result.NormalizedBoundary.Should().HaveCount(4);
        Math.Abs(SignedArea(result.NormalizedBoundary)).Should().BeApproximately(500000, 1e-6);
    }

    [Fact]
    public void Normalize_SelfIntersectingBoundary_ReturnsSelfIntersection()
    {
        var boundary = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(400, 400, 0),
            new Point3D(0, 400, 0),
            new Point3D(400, 0, 0),
            new Point3D(200, -100, 0)
        };

        var result = HatchBoundaryNormalizer.Normalize(boundary);

        result.IsValid.Should().BeFalse();
        result.IssueCode.Should().Be(HatchBoundaryIssueCode.SelfIntersection);
    }

    [Fact]
    public void Normalize_AreaBelowThreshold_ReturnsZeroArea()
    {
        var boundary = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(0.01, 0, 0),
            new Point3D(0.01, 0.01, 0),
            new Point3D(0, 0.01, 0)
        };

        var result = HatchBoundaryNormalizer.Normalize(boundary);

        result.IsValid.Should().BeFalse();
        result.IssueCode.Should().Be(HatchBoundaryIssueCode.ZeroArea);
    }

    private static double SignedArea(IReadOnlyList<Point3D> points)
    {
        var area = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }

        return area / 2.0;
    }
}
