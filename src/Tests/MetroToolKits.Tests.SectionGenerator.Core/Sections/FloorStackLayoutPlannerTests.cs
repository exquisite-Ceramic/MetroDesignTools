using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core.Sections;

public class FloorStackLayoutPlannerTests
{
    private static readonly Vector3D ViewDirection = new(0, 1, 0);

    [Fact]
    public void Create_DrawAll_IncludesEveryBoundary()
    {
        var planner = new FloorStackLayoutPlanner();
        var floors = new[] { MakeFloor("F1"), MakeFloor("F2"), MakeFloor("F3") };

        var plan = planner.Create(floors, FloorStackBoundaryPolicy.DrawAll);

        plan.Items.Should().HaveCount(3);
        plan.Items.Should().OnlyContain(item =>
            Includes(item.BottomBoundary) &&
            Includes(item.TopBoundary));
    }

    [Fact]
    public void Create_ShareInteriorBoundaries_ForSingleFloorIncludesEveryBoundary()
    {
        var planner = new FloorStackLayoutPlanner();
        var floors = new[] { MakeFloor("F1") };

        var plan = planner.Create(floors, FloorStackBoundaryPolicy.ShareInteriorBoundaries);

        plan.Items.Should().ContainSingle();
        Includes(plan.Items[0].BottomBoundary).Should().BeTrue();
        Includes(plan.Items[0].TopBoundary).Should().BeTrue();
    }

    [Fact]
    public void Create_ShareInteriorBoundaries_ForTwoFloorsSuppressesUpperBottom()
    {
        var planner = new FloorStackLayoutPlanner();
        var floors = new[] { MakeFloor("F1"), MakeFloor("F2") };

        var plan = planner.Create(floors, FloorStackBoundaryPolicy.ShareInteriorBoundaries);

        Includes(plan.Items[0].BottomBoundary).Should().BeTrue();
        Includes(plan.Items[0].TopBoundary).Should().BeTrue();
        IsSuppressed(plan.Items[1].BottomBoundary).Should().BeTrue();
        Includes(plan.Items[1].TopBoundary).Should().BeTrue();
    }

    [Fact]
    public void Create_ShareInteriorBoundaries_ForThreeFloorsSuppressesMiddleAndTopBottom()
    {
        var planner = new FloorStackLayoutPlanner();
        var floors = new[] { MakeFloor("F1"), MakeFloor("F2"), MakeFloor("F3") };

        var plan = planner.Create(floors, FloorStackBoundaryPolicy.ShareInteriorBoundaries);

        Includes(plan.Items[0].BottomBoundary).Should().BeTrue();
        Includes(plan.Items[0].TopBoundary).Should().BeTrue();
        IsSuppressed(plan.Items[1].BottomBoundary).Should().BeTrue();
        Includes(plan.Items[1].TopBoundary).Should().BeTrue();
        IsSuppressed(plan.Items[2].BottomBoundary).Should().BeTrue();
        Includes(plan.Items[2].TopBoundary).Should().BeTrue();
    }

    [Fact]
    public void Generate_DrawAll_CountsBottomAndTopHeightForEveryFloor()
    {
        var composer = new MultiFloorSectionComposer(new SectionComposer());
        var floors = new[] { MakeFloor("F1"), MakeFloor("F2"), MakeFloor("F3") };
        var executionContexts = ExecutionContexts(floors);

        var result = composer.Generate(
            executionContexts,
            ViewDirection,
            new Dictionary<string, IReadOnlyList<BuildingElement>>(),
            floors,
            boundaryPolicy: FloorStackBoundaryPolicy.DrawAll);

        result.TotalHeight.Should().Be(9900);
        result.Floors.Select(floor => floor.BaseElevation).Should().Equal(0, 3300, 6600);
    }

    [Fact]
    public void Generate_ShareInteriorBoundaries_DoesNotCountUpperBottomHeight()
    {
        var composer = new MultiFloorSectionComposer(new SectionComposer());
        var floors = new[] { MakeFloor("F1"), MakeFloor("F2"), MakeFloor("F3") };
        var executionContexts = ExecutionContexts(floors);

        var result = composer.Generate(
            executionContexts,
            ViewDirection,
            new Dictionary<string, IReadOnlyList<BuildingElement>>(),
            floors,
            boundaryPolicy: FloorStackBoundaryPolicy.ShareInteriorBoundaries);

        result.TotalHeight.Should().Be(9700);
        result.Floors.Select(floor => floor.BaseElevation).Should().Equal(0, 3300, 6500);
    }

    private static bool Includes(FloorBoundaryContribution contribution)
        => contribution.DrawLines && contribution.DrawHatch && contribution.CountHeight;

    private static bool IsSuppressed(FloorBoundaryContribution contribution)
        => !contribution.DrawLines && !contribution.DrawHatch && !contribution.CountHeight;

    private static FloorConfig MakeFloor(string name) => new()
    {
        Name = name,
        Height = 3000,
        BottomSlabThickness = 100,
        TopSlabThickness = 200,
        FinishThickness = 0
    };

    private static IReadOnlyDictionary<string, FloorExecutionContext> ExecutionContexts(IReadOnlyList<FloorConfig> floors)
        => floors.ToDictionary(
            floor => floor.Name,
            floor => new FloorExecutionContext
            {
                FloorName = floor.Name,
                CanParticipate = true,
                SectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(0, 1000, 0))
            },
            StringComparer.OrdinalIgnoreCase);
}
