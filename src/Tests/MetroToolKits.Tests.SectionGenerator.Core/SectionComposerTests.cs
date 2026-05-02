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
    public void Generate_WithRecognitionDataAndNoCandidates_MatchesLegacyOutput()
    {
        var composer = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(-500, 2500, 0), new Point3D(6500, 2500, 0));
        var floor = DefaultFloor();
        var wall = new Wall
        {
            SourceHandle = "CUT",
            StartPoint = new Point3D(0, 0, 0),
            EndPoint = new Point3D(0, 5000, 0),
            Height = 3000,
            Thickness = 200,
            BaseElevation = 0
        };

        var legacy = composer.Generate(sectionLine, ViewDirection, new BuildingElement[] { wall }, floor);
        var staged = composer.Generate(
            sectionLine,
            ViewDirection,
            new SectionFloorRecognitionData
            {
                Floor = floor,
                CutElements = new BuildingElement[] { wall }
            });

        staged.Elements.Should().HaveSameCount(legacy.Elements);
        staged.AllCutLines.Select(LineSignature).Should().Equal(legacy.AllCutLines.Select(LineSignature));
        staged.AllSightLines.Should().BeEmpty();
        staged.SlabLines.Select(LineSignature).Should().Equal(legacy.SlabLines.Select(LineSignature));
    }

    [Fact]
    public void Generate_WithSightLineCandidates_CreatesSightLinesOnlyForCandidate()
    {
        var composer = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(-500, 2500, 0), new Point3D(6500, 2500, 0));
        var cutWall = new Wall
        {
            SourceHandle = "CUT",
            StartPoint = new Point3D(0, 0, 0),
            EndPoint = new Point3D(0, 5000, 0),
            Height = 3000,
            Thickness = 200,
            BaseElevation = 0
        };
        var candidateWall = new Wall
        {
            SourceHandle = "CANDIDATE",
            StartPoint = new Point3D(1000, 1000, 0),
            EndPoint = new Point3D(2000, 1000, 0),
            Height = 3000,
            Thickness = 200,
            BaseElevation = 0
        };

        var data = composer.Generate(
            sectionLine,
            ViewDirection,
            new SectionFloorRecognitionData
            {
                Floor = DefaultFloor(),
                CutElements = new BuildingElement[] { cutWall },
                SightLineCandidates = new[]
                {
                    new SectionSightLineCandidate
                    {
                        Element = candidateWall,
                        SourceHandle = "CANDIDATE",
                        ElementType = "Wall",
                        MinChainage = 1000,
                        MaxChainage = 2000,
                        MinDepth = 100,
                        MaxDepth = 300
                    }
                }
            });

        data.Elements.Should().HaveCount(2);

        var cutData = data.Elements.Single(element => element.SourceHandle == "CUT");
        cutData.CutLineSegments.Should().NotBeEmpty();
        cutData.HatchRegions.Should().NotBeEmpty();
        cutData.SightLines.Should().BeEmpty();

        var candidateData = data.Elements.Single(element => element.SourceHandle == "CANDIDATE");
        candidateData.CutLines.Should().BeEmpty();
        candidateData.CutLineSegments.Should().BeEmpty();
        candidateData.HatchRegions.Should().BeEmpty();
        candidateData.SightLines.Select(LineSignature).Should().Equal(new[]
        {
            "1000.000000,0.000000,2000.000000,0.000000",
            "2000.000000,0.000000,2000.000000,3000.000000",
            "2000.000000,3000.000000,1000.000000,3000.000000",
            "1000.000000,3000.000000,1000.000000,0.000000"
        });
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

    [Fact]
    public void Generate_LegacySlabStructuralCore_RemainsContinuousThroughWallFace()
    {
        var composer = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 2500, 0), new Point3D(6000, 2500, 0));
        var wall = new Wall
        {
            StartPoint = new Point3D(3000, 0, 0),
            EndPoint = new Point3D(3000, 5000, 0),
            Height = 3000,
            Thickness = 200,
            BaseElevation = 0
        };
        var slab = new Slab
        {
            Outline = new List<Point3D>
            {
                new(0, 0, 0),
                new(6000, 0, 0),
                new(6000, 5000, 0),
                new(0, 5000, 0)
            },
            TopElevation = 0,
            Thickness = 200
        };

        var data = composer.Generate(sectionLine, ViewDirection, new BuildingElement[] { wall, slab }, DefaultFloor());
        var slabData = data.Elements.Single(element => element.ElementType == "Slab");

        slabData.CutLines.Should().HaveCount(2);
        slabData.CutLines.Should().OnlyContain(line =>
            Math.Abs(line.Start.X) < 1e-6 &&
            Math.Abs(line.End.X - 6000) < 1e-6);
    }

    private static string LineSignature(Line3D line)
        => $"{line.Start.X:F6},{line.Start.Y:F6},{line.End.X:F6},{line.End.Y:F6}";
}
