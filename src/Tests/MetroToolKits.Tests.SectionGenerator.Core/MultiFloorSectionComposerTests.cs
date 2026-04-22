using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class MultiFloorSectionComposerTests
{
    private static readonly Vector3D ViewDir = new(0, 1, 0);

    private static FloorConfig MakeFloor(string name, double height = 3000,
        double bottomSlab = 800, double topSlab = 600) => new()
    {
        Name = name,
        Height = height,
        BottomSlabThickness = bottomSlab,
        TopSlabThickness = topSlab,
        FinishThickness = 120
    };

    private static MultiFloorSectionComposer MakeComposer()
        => new(new SectionComposer());

    private static IReadOnlyDictionary<string, FloorExecutionContext> ExecutionContexts(params (string FloorName, bool CanParticipate, Line3D? Line)[] items)
        => items.ToDictionary(
            item => item.FloorName,
            item => new FloorExecutionContext
            {
                FloorName = item.FloorName,
                CanParticipate = item.CanParticipate,
                SectionLine = item.Line
            },
            StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Generate_TwoParticipatingFloors_ReturnsTwoFloorData()
    {
        var composer = MakeComposer();
        var floors = new[] { MakeFloor("F1"), MakeFloor("F2") };
        var executionContexts = ExecutionContexts(
            ("F1", true, new Line3D(new Point3D(0, 0, 0), new Point3D(0, 1000, 0))),
            ("F2", true, new Line3D(new Point3D(100, 0, 0), new Point3D(100, 1000, 0))));

        var result = composer.Generate(executionContexts, ViewDir, new Dictionary<string, IReadOnlyList<BuildingElement>>(), floors);

        result.Floors.Should().HaveCount(2);
    }

    [Fact]
    public void Generate_SkippedFloor_DoesNotCollapseUpperFloorElevation()
    {
        var composer = MakeComposer();
        var floors = new[]
        {
            MakeFloor("F1", height: 3000, bottomSlab: 800, topSlab: 600),
            MakeFloor("F2", height: 3000, bottomSlab: 800, topSlab: 600),
            MakeFloor("F3", height: 3000, bottomSlab: 800, topSlab: 600)
        };
        var executionContexts = ExecutionContexts(
            ("F1", true, new Line3D(new Point3D(0, 0, 0), new Point3D(0, 1000, 0))),
            ("F2", false, null),
            ("F3", true, new Line3D(new Point3D(200, 0, 0), new Point3D(200, 1000, 0))));

        var result = composer.Generate(executionContexts, ViewDir, new Dictionary<string, IReadOnlyList<BuildingElement>>(), floors);

        result.Floors.Should().HaveCount(2);
        result.Floors[0].FloorName.Should().Be("F1");
        result.Floors[0].BaseElevation.Should().Be(0);
        result.Floors[1].FloorName.Should().Be("F3");
        result.Floors[1].BaseElevation.Should().Be(8800);
    }

    [Fact]
    public void Generate_TotalHeight_EqualsSumOfAllFloors()
    {
        var composer = MakeComposer();
        var floors = new[]
        {
            MakeFloor("F1", height: 3000, bottomSlab: 800, topSlab: 600),
            MakeFloor("F2", height: 4000, bottomSlab: 1000, topSlab: 700)
        };
        var executionContexts = ExecutionContexts(
            ("F1", true, new Line3D(new Point3D(0, 0, 0), new Point3D(0, 1000, 0))),
            ("F2", true, new Line3D(new Point3D(100, 0, 0), new Point3D(100, 1000, 0))));

        var result = composer.Generate(executionContexts, ViewDir, new Dictionary<string, IReadOnlyList<BuildingElement>>(), floors);

        result.TotalHeight.Should().Be(10100);
    }
}
