using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FloorGeometryHasherTests
{
    private static Wall MakeWall(double x1, double y1, double x2, double y2) => new()
    {
        StartPoint = new Point3D(x1, y1, 0),
        EndPoint = new Point3D(x2, y2, 0),
        Height = 3000,
        Thickness = 200
    };

    [Fact]
    public void SameGeometry_ProducesSameHash()
    {
        var hasher = new FloorGeometryHasher();
        var wallA = MakeWall(0, 0, 0, 5000);
        var wallB = MakeWall(0, 0, 0, 5000);
        wallA.SourceHandle = "AAA";
        wallB.SourceHandle = "AAA";

        hasher.ComputeHash(new BuildingElement[] { wallA }).Should().Be(
            hasher.ComputeHash(new BuildingElement[] { wallB }));
    }

    [Fact]
    public void MovedElement_ProducesDifferentHash()
    {
        var hasher = new FloorGeometryHasher();
        var wall = MakeWall(0, 0, 0, 5000);
        wall.SourceHandle = "AAA";

        var hashA = hasher.ComputeHash(new BuildingElement[] { wall });
        wall.StartPoint = new Point3D(100, 0, 0);
        wall.EndPoint = new Point3D(100, 5000, 0);
        var hashB = hasher.ComputeHash(new BuildingElement[] { wall });

        hashA.Should().NotBe(hashB);
    }

    [Fact]
    public void FloorSlopeChange_ProducesDifferentHash()
    {
        var hasher = new FloorGeometryHasher();
        var floorA = new FloorConfig { Name = "F1", Height = 5200, FinishThickness = 120, BottomSlabThickness = 800, TopSlabThickness = 600 };
        var floorB = new FloorConfig { Name = "F1", Height = 5200, FinishThickness = 120, BottomSlabThickness = 800, TopSlabThickness = 600 };
        floorB.TopBoundarySlab.SlopeEnabled = true;
        floorB.TopBoundarySlab.SlopeValue = 0.01;

        hasher.ComputeHash(Array.Empty<BuildingElement>(), floorA).Should().NotBe(
            hasher.ComputeHash(Array.Empty<BuildingElement>(), floorB));
    }
}
