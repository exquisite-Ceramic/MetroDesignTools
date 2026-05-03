using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core.Sections;

public class SightLineComposerTests
{
    [Fact]
    public void Compose_WithSlabCandidate_DoesNotGenerateSightLines()
    {
        var candidate = CreateCandidate(new Slab
        {
            Outline = RectangleOutline(),
            TopElevation = 3000,
            Thickness = 300
        });

        var result = new SightLineComposer().Compose(new[] { candidate }, baseElevation: 0);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compose_WithSlabCandidateAndIncludeSlabs_GeneratesSightLines()
    {
        var candidate = CreateCandidate(new Slab
        {
            Outline = RectangleOutline(),
            TopElevation = 3000,
            Thickness = 300
        });

        var result = new SightLineComposer().Compose(
            new[] { candidate },
            baseElevation: 0,
            options: new SightLineComposerOptions { IncludeSlabs = true });

        result.Should().ContainSingle();
        result[0].SightLines.Should().HaveCount(4);
    }

    [Fact]
    public void Compose_WithCompositeSlabCandidate_DoesNotGenerateSightLines()
    {
        var candidate = CreateCandidate(new CompositeSlabElement
        {
            CoreArea = new CoreSlabArea
            {
                Outline = RectangleOutline(),
                CoreTopElevation = 3000
            },
            LayerSections = new[]
            {
                new SlabLayerSection
                {
                    Name = "Core",
                    IsCore = true,
                    Thickness = 300,
                    TopOffset = 0,
                    BottomOffset = -300
                }
            }
        });

        var result = new SightLineComposer().Compose(new[] { candidate }, baseElevation: 0);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compose_WithWallCandidate_GeneratesSightLinesOnly()
    {
        var candidate = CreateCandidate(new Wall
        {
            StartPoint = new Point3D(0, 0, 0),
            EndPoint = new Point3D(0, 5000, 0),
            Height = 3000,
            Thickness = 200,
            BaseElevation = 0
        });

        var result = new SightLineComposer().Compose(new[] { candidate }, baseElevation: 0);

        result.Should().ContainSingle();
        result[0].SightLines.Should().HaveCount(4);
        result[0].CutLines.Should().BeEmpty();
        result[0].HatchRegions.Should().BeEmpty();
    }

    [Fact]
    public void Compose_WithColumnCandidate_GeneratesSightLinesOnly()
    {
        var candidate = CreateCandidate(new Column
        {
            CenterPoint = new Point3D(500, 500, 0),
            Width = 400,
            Depth = 400,
            Height = 3000,
            BaseElevation = 0
        });

        var result = new SightLineComposer().Compose(new[] { candidate }, baseElevation: 0);

        result.Should().ContainSingle();
        result[0].SightLines.Should().HaveCount(4);
        result[0].CutLines.Should().BeEmpty();
        result[0].HatchRegions.Should().BeEmpty();
    }

    private static SectionSightLineCandidate CreateCandidate(BuildingElement element)
        => new()
        {
            Element = element,
            SourceHandle = "H1",
            MinChainage = 0,
            MaxChainage = 1000,
            MinDepth = 0,
            MaxDepth = 1000
        };

    private static List<Point3D> RectangleOutline()
        => new()
        {
            new Point3D(0, 0, 0),
            new Point3D(1000, 0, 0),
            new Point3D(1000, 1000, 0),
            new Point3D(0, 1000, 0)
        };
}
