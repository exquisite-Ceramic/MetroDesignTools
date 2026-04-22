using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Tests.Foundation.Core;

public class SectionCoordinateProjectorTests
{
    [Fact]
    public void ProjectPoint_HorizontalSection_UsesChainageAsXAndElevationAsY()
    {
        var sectionLine = new Line3D(new Point3D(10, 20, 0), new Point3D(110, 20, 0));
        var projector = new SectionCoordinateProjector(sectionLine);

        var projected = projector.ProjectPoint(new Point3D(60, 20, 3500));

        projected.X.Should().BeApproximately(50, 1e-6);
        projected.Y.Should().BeApproximately(3500, 1e-6);
        projected.Z.Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void GetChainage_VerticalSection_UsesYDistance()
    {
        var sectionLine = new Line3D(new Point3D(0, 100, 0), new Point3D(0, 600, 0));
        var projector = new SectionCoordinateProjector(sectionLine);

        projector.GetChainage(new Point3D(0, 250, 0)).Should().BeApproximately(150, 1e-6);
    }

    [Fact]
    public void ProjectLine_DiagonalSection_UsesDistanceAlongSectionLine()
    {
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(100, 100, 0));
        var projector = new SectionCoordinateProjector(sectionLine);
        var worldLine = new Line3D(new Point3D(50, 50, 0), new Point3D(100, 100, 3000));

        var projected = projector.ProjectLine(worldLine);

        projected.Start.X.Should().BeApproximately(Math.Sqrt(50 * 50 + 50 * 50), 1e-6);
        projected.End.X.Should().BeApproximately(Math.Sqrt(100 * 100 + 100 * 100), 1e-6);
        projected.Start.Y.Should().BeApproximately(0, 1e-6);
        projected.End.Y.Should().BeApproximately(3000, 1e-6);
        projected.Start.Z.Should().BeApproximately(0, 1e-6);
        projected.End.Z.Should().BeApproximately(0, 1e-6);
    }
}
