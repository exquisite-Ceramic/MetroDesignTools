using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Tests.Foundation.Building;

public class WallSectionTests
{
    private static readonly Vector3D ViewDir = new(0, 1, 0);

    private static SectionGeometryContext CreateContext(Line3D sectionLine)
        => new()
        {
            SectionLine = sectionLine,
            ViewDirection = ViewDir,
            Projector = new SectionCoordinateProjector(sectionLine)
        };

    // ── 正交剖切 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Wall_OrthogonalSection_ReturnsOneLine()
    {
        // 墙体沿 Y 轴方向，剖切线沿 X 轴穿过
        var wall = new Wall
        {
            StartPoint    = new Point3D(0, 0, 0),
            EndPoint      = new Point3D(0, 5000, 0),
            Height        = 3000,
            Thickness     = 200,
            BaseElevation = 0
        };
        var sectionLine = new Line3D(new Point3D(-1000, 2500, 0), new Point3D(1000, 2500, 0));

        var lines = wall.GetSectionGeometry(CreateContext(sectionLine)).ToList();

        lines.Should().HaveCount(1);
        lines[0].Start.Z.Should().BeApproximately(0, 1e-6);
        lines[0].End.Z.Should().BeApproximately(3000, 1e-6);
    }

    [Fact]
    public void Wall_SectionLine_MissesWall_ReturnsEmpty()
    {
        var wall = new Wall
        {
            StartPoint = new Point3D(0, 0, 0),
            EndPoint   = new Point3D(0, 5000, 0),
            Height     = 3000,
            Thickness  = 200
        };
        // 剖切线在墙体延长线之外
        var sectionLine = new Line3D(new Point3D(-1000, 6000, 0), new Point3D(1000, 6000, 0));

        var lines = wall.GetSectionGeometry(CreateContext(sectionLine)).ToList();
        lines.Should().BeEmpty();
    }

    [Fact]
    public void Wall_BaseElevation_AffectsZCoordinates()
    {
        var wall = new Wall
        {
            StartPoint    = new Point3D(0, 0, 0),
            EndPoint      = new Point3D(0, 5000, 0),
            Height        = 3000,
            BaseElevation = 1000
        };
        var sectionLine = new Line3D(new Point3D(-500, 2500, 0), new Point3D(500, 2500, 0));

        var lines = wall.GetSectionGeometry(CreateContext(sectionLine)).ToList();

        lines.Should().HaveCount(1);
        lines[0].Start.Z.Should().BeApproximately(1000, 1e-6);
        lines[0].End.Z.Should().BeApproximately(4000, 1e-6);
    }

    [Fact]
    public void Wall_BoundingBox_HasFourVertices()
    {
        var wall = new Wall
        {
            StartPoint = new Point3D(0, 0, 0),
            EndPoint   = new Point3D(0, 5000, 0),
            Thickness  = 200
        };
        var bbox = wall.GetBoundingBox();
        bbox.Should().NotBeNull();
        bbox!.VertexCount.Should().Be(4);
    }
}
