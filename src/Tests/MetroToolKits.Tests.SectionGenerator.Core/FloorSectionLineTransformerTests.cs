using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FloorSectionLineTransformerTests
{
    [Fact]
    public void TryValidateAlignmentPoints_WithThreeDistinctPoints_ReturnsTrue()
    {
        var points = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(10, 0, 0),
            new Point3D(0, 10, 0)
        };

        var isValid = FloorSectionLineTransformer.TryValidateAlignmentPoints(points, out var error);

        isValid.Should().BeTrue();
        error.Should().BeEmpty();
    }

    [Fact]
    public void TryValidateAlignmentPoints_WithCollinearPoints_ReturnsFalse()
    {
        var points = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(10, 0, 0),
            new Point3D(20, 0, 0)
        };

        var isValid = FloorSectionLineTransformer.TryValidateAlignmentPoints(points, out var error);

        isValid.Should().BeFalse();
        error.Should().Contain("共线");
    }

    [Fact]
    public void ApplyAlignment_WithThreePointTranslation_ReturnsTransformedLine()
    {
        var sourcePoints = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(10, 0, 0),
            new Point3D(0, 10, 0)
        };
        var targetPoints = new[]
        {
            new Point3D(100, 200, 0),
            new Point3D(110, 200, 0),
            new Point3D(100, 210, 0)
        };
        var line = new Line3D(new Point3D(0, 0, 0), new Point3D(0, 50, 0));

        var result = FloorSectionLineTransformer.ApplyAlignment(line, sourcePoints, targetPoints);

        result.Start.Should().Be(new Point3D(100, 200, 0));
        result.End.Should().Be(new Point3D(100, 250, 0));
    }
}
