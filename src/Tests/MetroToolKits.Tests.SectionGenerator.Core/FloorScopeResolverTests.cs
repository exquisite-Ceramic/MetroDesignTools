using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FloorScopeResolverTests
{
    private static readonly Line3D SourceLine =
        new(new Point3D(0, 0, 0), new Point3D(0, 20, 0));

    private static FloorConfig Floor(
        string name,
        ScopeBounds2D? scopeBounds = null,
        params Point3D[] alignmentPoints) => new()
    {
        Name = name,
        Height = 3000,
        BottomSlabThickness = 800,
        TopSlabThickness = 600,
        AlignmentPoints = alignmentPoints.ToList(),
        ScopeBounds = scopeBounds
    };

    [Fact]
    public void Resolve_SingleFloorWithoutScope_AllowsParticipation()
    {
        var alignment = new FloorAlignmentResolver().Resolve(SourceLine, new SectionConfig
        {
            Floors = new List<FloorConfig> { Floor("F1") }
        });

        var result = new FloorScopeResolver().Resolve(
            new SectionConfig { Floors = new List<FloorConfig> { Floor("F1") }, AlignmentBaseFloorName = "F1" },
            alignment,
            targetFloorName: null,
            localScopeBounds: null,
            localScopeFloorName: null);

        result.FatalIssue.Should().BeNull();
        result.Floors["F1"].CanParticipate.Should().BeTrue();
        result.Floors["F1"].EffectiveScope.Should().BeNull();
    }

    [Fact]
    public void Resolve_MultiFloorMissingBaseScope_ReturnsFatal()
    {
        var config = new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors = new List<FloorConfig>
            {
                Floor("F1", null,
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(0, 10, 0)),
                Floor("F2", new ScopeBounds2D { MinX = 100, MinY = 0, MaxX = 200, MaxY = 100 },
                    new Point3D(100, 0, 0),
                    new Point3D(110, 0, 0),
                    new Point3D(100, 10, 0))
            }
        };

        var alignment = new FloorAlignmentResolver().Resolve(SourceLine, config);
        var result = new FloorScopeResolver().Resolve(config, alignment, null, null, null);

        result.FatalIssue.Should().NotBeNull();
        result.FatalIssue!.Kind.Should().Be(FloorScopeIssueKind.FloorScopeMissing);
    }

    [Fact]
    public void Resolve_MultiFloorMissingNonBaseScope_SkipsOnlyThatFloor()
    {
        var config = new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors = new List<FloorConfig>
            {
                Floor("F1", new ScopeBounds2D { MinX = 0, MinY = 0, MaxX = 100, MaxY = 100 },
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(0, 10, 0)),
                Floor("F2", null,
                    new Point3D(100, 0, 0),
                    new Point3D(110, 0, 0),
                    new Point3D(100, 10, 0))
            }
        };

        var alignment = new FloorAlignmentResolver().Resolve(SourceLine, config);
        var result = new FloorScopeResolver().Resolve(config, alignment, null, null, null);

        result.FatalIssue.Should().BeNull();
        result.Floors["F1"].CanParticipate.Should().BeTrue();
        result.Floors["F2"].CanParticipate.Should().BeFalse();
        result.Floors["F2"].ScopeIssue!.Kind.Should().Be(FloorScopeIssueKind.FloorScopeMissing);
    }

    [Fact]
    public void Resolve_AllFloorsWithLocalScope_MapsLocalBoundsToTargetFloor()
    {
        var config = new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors = new List<FloorConfig>
            {
                Floor("F1", new ScopeBounds2D { MinX = 0, MinY = 0, MaxX = 200, MaxY = 200 },
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(0, 10, 0)),
                Floor("F2", new ScopeBounds2D { MinX = 100, MinY = 0, MaxX = 300, MaxY = 200 },
                    new Point3D(100, 0, 0),
                    new Point3D(110, 0, 0),
                    new Point3D(100, 10, 0))
            }
        };

        var alignment = new FloorAlignmentResolver().Resolve(SourceLine, config);
        var localScope = new ScopeBounds2D { MinX = 10, MinY = 20, MaxX = 30, MaxY = 40 };

        var result = new FloorScopeResolver().Resolve(
            config,
            alignment,
            null,
            localScope,
            "F1");

        result.FatalIssue.Should().BeNull();
        result.Floors["F1"].EffectiveScope.Should().Be(localScope);
        result.Floors["F2"].EffectiveScope.Should().Be(new ScopeBounds2D
        {
            MinX = 110,
            MinY = 20,
            MaxX = 130,
            MaxY = 40
        });
    }
}
