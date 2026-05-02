using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Tests.Foundation.Core;

public class SectionViewDepthFilterTests
{
    [Fact]
    public void IntersectsSingleSidedStrip_FootprintInsideViewDirectionAndDepth_ReturnsTrue()
    {
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(100, 0, 0));
        var footprint = Rectangle(minX: 20, minY: 10, maxX: 40, maxY: 20);

        var result = SectionViewDepthFilter.IntersectsSingleSidedStrip(sectionLine, 30, footprint);

        result.Should().BeTrue();
    }

    [Fact]
    public void IntersectsSingleSidedStrip_FootprintBeyondViewDepth_ReturnsFalse()
    {
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(100, 0, 0));
        var footprint = Rectangle(minX: 20, minY: 40, maxX: 40, maxY: 50);

        var result = SectionViewDepthFilter.IntersectsSingleSidedStrip(sectionLine, 30, footprint);

        result.Should().BeFalse();
    }

    [Fact]
    public void IntersectsSingleSidedStrip_FootprintBehindSectionLine_ReturnsFalse()
    {
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(100, 0, 0));
        var footprint = Rectangle(minX: 20, minY: -20, maxX: 40, maxY: -10);

        var result = SectionViewDepthFilter.IntersectsSingleSidedStrip(sectionLine, 30, footprint);

        result.Should().BeFalse();
    }

    [Fact]
    public void IntersectsSingleSidedStrip_ReversingSectionLineFlipsViewSide()
    {
        var forwardLine = new Line3D(new Point3D(0, 0, 0), new Point3D(100, 0, 0));
        var reversedLine = new Line3D(new Point3D(100, 0, 0), new Point3D(0, 0, 0));
        var forwardSideFootprint = Rectangle(minX: 20, minY: 10, maxX: 40, maxY: 20);

        SectionViewDepthFilter.IntersectsSingleSidedStrip(forwardLine, 30, forwardSideFootprint)
            .Should().BeTrue();
        SectionViewDepthFilter.IntersectsSingleSidedStrip(reversedLine, 30, forwardSideFootprint)
            .Should().BeFalse();
    }

    private static Polygon3D Rectangle(double minX, double minY, double maxX, double maxY)
        => new(new[]
        {
            new Point3D(minX, minY, 0),
            new Point3D(maxX, minY, 0),
            new Point3D(maxX, maxY, 0),
            new Point3D(minX, maxY, 0)
        });
}
