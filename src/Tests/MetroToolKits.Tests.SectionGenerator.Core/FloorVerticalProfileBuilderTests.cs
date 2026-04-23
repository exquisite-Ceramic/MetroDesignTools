using FluentAssertions;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FloorVerticalProfileBuilderTests
{
    [Fact]
    public void Build_BottomFinishThickness_DoesNotChangeBottomStructuralTop()
    {
        var builder = new FloorVerticalProfileBuilder();
        var floor = new FloorConfig
        {
            Name = "F1",
            Height = 5200,
            FinishThickness = 120,
            BottomSlabThickness = 800,
            TopSlabThickness = 600,
            BottomBoundarySlab = new BoundarySlabConfig(),
            TopBoundarySlab = new BoundarySlabConfig()
        };

        var profile = builder.Build(floor, sectionLength: 10000, baseElevation: 0);

        profile.GetBottomStructuralTop(0).Should().Be(0);
        profile.GetBottomStructuralBottom(0).Should().Be(-800);
        profile.GetTopBoundaryTop(0).Should().Be(5920);
    }

    [Fact]
    public void Build_TopAndBottomSlope_AffectStructuralFaces()
    {
        var builder = new FloorVerticalProfileBuilder();
        var floor = new FloorConfig
        {
            Name = "F1",
            Height = 5200,
            FinishThickness = 120,
            BottomSlabThickness = 800,
            TopSlabThickness = 600,
            BottomBoundarySlab = new BoundarySlabConfig
            {
                SlopeEnabled = true,
                SlopeValue = 0.01
            },
            TopBoundarySlab = new BoundarySlabConfig
            {
                SlopeEnabled = true,
                SlopeValue = 0.02
            }
        };

        var profile = builder.Build(floor, sectionLength: 1000, baseElevation: 100);

        profile.GetBottomStructuralTop(0).Should().Be(100);
        profile.GetBottomStructuralTop(1000).Should().Be(110);
        profile.GetTopStructuralBottom(0).Should().Be(5300);
        profile.GetTopStructuralBottom(1000).Should().Be(5320);
    }
}
