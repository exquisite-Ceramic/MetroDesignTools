using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FloorAlignmentResolverTests
{
    private static readonly Line3D SourceLine =
        new(new Point3D(0, 0, 0), new Point3D(0, 20, 0));

    private static FloorConfig Floor(
        string name,
        params Point3D[] alignmentPoints) => new()
    {
        Name = name,
        Height = 3000,
        BottomSlabThickness = 800,
        TopSlabThickness = 600,
        AlignmentPoints = alignmentPoints.ToList()
    };

    [Fact]
    public void Resolve_SingleFloorWithoutAlignmentPoints_AllowsParticipation()
    {
        var resolver = new FloorAlignmentResolver();
        var config = new SectionConfig
        {
            Floors = new List<FloorConfig> { Floor("F1") }
        };

        var result = resolver.Resolve(SourceLine, config);

        result.FatalIssue.Should().BeNull();
        result.Floors["F1"].CanParticipate.Should().BeTrue();
        result.Floors["F1"].SectionLine.Should().Be(SourceLine);
    }

    [Fact]
    public void Resolve_MultiFloorWithoutBaseFloor_ReturnsFatalFailure()
    {
        var resolver = new FloorAlignmentResolver();
        var config = new SectionConfig
        {
            Floors = new List<FloorConfig> { Floor("F1"), Floor("F2") }
        };

        var result = resolver.Resolve(SourceLine, config);

        result.FatalIssue.Should().NotBeNull();
        result.FatalIssue!.Kind.Should().Be(FloorAlignmentIssueKind.AlignmentBaseFloorMissing);
    }

    [Fact]
    public void Resolve_MultiFloorWithInvalidBasePoints_ReturnsFatalFailure()
    {
        var resolver = new FloorAlignmentResolver();
        var config = new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors = new List<FloorConfig>
            {
                Floor("F1",
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(20, 0, 0)),
                Floor("F2")
            }
        };

        var result = resolver.Resolve(SourceLine, config);

        result.FatalIssue.Should().NotBeNull();
        result.FatalIssue!.Kind.Should().Be(FloorAlignmentIssueKind.AlignmentBaseFloorInvalid);
    }

    [Fact]
    public void Resolve_NonBaseFloorMissingPoints_SkipsOnlyThatFloor()
    {
        var resolver = new FloorAlignmentResolver();
        var config = new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors = new List<FloorConfig>
            {
                Floor("F1",
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(0, 10, 0)),
                Floor("F2")
            }
        };

        var result = resolver.Resolve(SourceLine, config);

        result.FatalIssue.Should().BeNull();
        result.Floors["F1"].CanParticipate.Should().BeTrue();
        result.Floors["F2"].CanParticipate.Should().BeFalse();
        result.Floors["F2"].Issue!.Kind.Should().Be(FloorAlignmentIssueKind.AlignmentPointsMissing);
    }

    [Fact]
    public void Resolve_NonBaseFloorWithValidPoints_ReturnsAlignedLine()
    {
        var resolver = new FloorAlignmentResolver();
        var config = new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors = new List<FloorConfig>
            {
                Floor("F1",
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(0, 10, 0)),
                Floor("F2",
                    new Point3D(100, 0, 0),
                    new Point3D(110, 0, 0),
                    new Point3D(100, 10, 0))
            }
        };

        var result = resolver.Resolve(SourceLine, config);

        result.FatalIssue.Should().BeNull();
        result.Floors["F2"].CanParticipate.Should().BeTrue();
        result.Floors["F2"].SectionLine.Should().Be(new Line3D(
            new Point3D(100, 0, 0),
            new Point3D(100, 20, 0)));
    }
}
