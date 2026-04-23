using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class SectionComposerTests
{
    private static readonly Vector3D ViewDirection = new(0, 1, 0);

    private static FloorConfig DefaultFloor(bool hasSlope = false, double slopeValue = 0) => new()
    {
        Name = "F1",
        Height = 5200,
        BottomSlabThickness = 800,
        TopSlabThickness = 600,
        FinishThickness = 120,
        HasSlope = hasSlope,
        SlopeValue = slopeValue
    };

    [Fact]
    public void Generate_NoElements_ReturnsBoundaryLinesOnly()
    {
        var composer = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(6000, 0, 0));

        var data = composer.Generate(sectionLine, ViewDirection, Array.Empty<BuildingElement>(), DefaultFloor());

        data.Elements.Should().BeEmpty();
        data.SlabLines.Should().HaveCount(7);
    }

    [Fact]
    public void Generate_WithWall_IncludesWallSection()
    {
        var composer = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(-500, 2500, 0), new Point3D(6500, 2500, 0));
        var wall = new Wall
        {
            StartPoint = new Point3D(0, 0, 0),
            EndPoint = new Point3D(0, 5000, 0),
            Height = 3000,
            Thickness = 200,
            BaseElevation = 0
        };

        var data = composer.Generate(sectionLine, ViewDirection, new BuildingElement[] { wall }, DefaultFloor());

        data.Elements.Should().ContainSingle();
        data.Elements[0].ElementType.Should().Be("Wall");
        data.Elements[0].CutLines.Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_VerticalSectionLine_StillProduces2DGeometry()
    {
        var composer = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(0, 6000, 0));

        var data = composer.Generate(sectionLine, ViewDirection, Array.Empty<BuildingElement>(), DefaultFloor());

        data.SlabLines.Should().OnlyContain(line =>
            Math.Abs(line.Start.Z) < 1e-6 &&
            Math.Abs(line.End.Z) < 1e-6);
        data.SlabLines.Should().Contain(line =>
            Math.Abs(line.Start.X - line.End.X) < 1e-6 &&
            Math.Abs(line.Start.Y - line.End.Y) > 1e-6);
        data.SlabLines.Should().Contain(line =>
            Math.Abs(line.Start.Y - line.End.Y) < 1e-6 &&
            Math.Abs(line.Start.X - line.End.X) > 1e-6);
    }

    [Fact]
    public void Generate_DiagonalSectionLine_UsesSectionLengthForSpan()
    {
        var composer = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(3000, 4000, 0));

        var data = composer.Generate(sectionLine, ViewDirection, Array.Empty<BuildingElement>(), DefaultFloor());
        var bottomLine = data.SlabLines.First(line =>
            Math.Abs(line.Start.Y + DefaultFloor().BottomSlabThickness) < 1e-6 &&
            Math.Abs(line.End.Y + DefaultFloor().BottomSlabThickness) < 1e-6);

        bottomLine.Start.X.Should().BeApproximately(0, 1e-6);
        bottomLine.End.X.Should().BeApproximately(5000, 1e-6);
    }
}
