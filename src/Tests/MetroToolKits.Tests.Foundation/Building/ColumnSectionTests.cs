using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Tests.Foundation.Building;

public class ColumnSectionTests
{
    private static readonly Vector3D ViewDir = new(0, 1, 0);

    private static SectionGeometryContext CreateContext(Line3D sectionLine)
        => new()
        {
            SectionLine = sectionLine,
            ViewDirection = ViewDir,
            Projector = new SectionCoordinateProjector(sectionLine)
        };

    [Fact]
    public void Column_SectionTouchingCorner_DoesNotReturnDegenerateLines()
    {
        var column = new Column
        {
            CenterPoint = new Point3D(0, 0, 0),
            Width = 400,
            Depth = 400,
            Height = 3000,
            BaseElevation = 0
        };

        var sectionLine = new Line3D(new Point3D(-1000, -1000, 0), new Point3D(-200, -200, 0));

        var lines = column.GetSectionGeometry(CreateContext(sectionLine)).ToList();

        lines.Should().BeEmpty("仅切到柱角时不应生成退化矩形");
    }

    [Fact]
    public void Column_DiagonalSection_ReturnsNonDegenerateRectangle()
    {
        var column = new Column
        {
            CenterPoint = new Point3D(0, 0, 0),
            Width = 400,
            Depth = 400,
            Height = 3000,
            BaseElevation = 0
        };

        var sectionLine = new Line3D(new Point3D(-500, -500, 0), new Point3D(500, 500, 0));

        var lines = column.GetSectionGeometry(CreateContext(sectionLine)).ToList();

        lines.Should().HaveCount(4);
        lines.Should().OnlyContain(line => line.Start.DistanceTo(line.End) > 1e-6);
    }
}
