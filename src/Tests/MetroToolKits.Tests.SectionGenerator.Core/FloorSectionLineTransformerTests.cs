using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FloorSectionLineTransformerTests
{
    [Fact]
    public void ApplyAlignment_WithoutAlignmentPoints_ReturnsOriginalLine()
    {
        var floor = new FloorConfig { Name = "F1" };
        var line = new Line3D(new Point3D(0, 0, 0), new Point3D(0, 1000, 0));

        var result = FloorSectionLineTransformer.ApplyAlignment(line, floor);

        result.Start.Should().Be(line.Start);
        result.End.Should().Be(line.End);
    }

    [Fact]
    public void ApplyAlignment_WithThreePointTranslation_ReturnsTransformedLine()
    {
        var floor = new FloorConfig
        {
            Name = "F2",
            AlignmentSourcePoints =
            {
                new Point3D(0, 0, 0),
                new Point3D(10, 0, 0),
                new Point3D(0, 10, 0)
            },
            AlignmentTargetPoints =
            {
                new Point3D(100, 200, 0),
                new Point3D(110, 200, 0),
                new Point3D(100, 210, 0)
            }
        };
        var line = new Line3D(new Point3D(0, 0, 0), new Point3D(0, 50, 0));

        var result = FloorSectionLineTransformer.ApplyAlignment(line, floor);

        result.Start.Should().Be(new Point3D(100, 200, 0));
        result.End.Should().Be(new Point3D(100, 250, 0));
    }
}
