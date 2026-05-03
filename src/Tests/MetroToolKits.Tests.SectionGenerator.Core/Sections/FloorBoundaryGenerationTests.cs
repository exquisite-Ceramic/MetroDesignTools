using FluentAssertions;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core.Sections;

public class FloorBoundaryGenerationTests
{
    [Fact]
    public void BuildBoundaryLineSegments_WhenBottomAndTopEnabled_GeneratesBothBoundariesAndClosures()
    {
        var builder = new FloorVerticalProfileBuilder();
        var floor = MakeFloor();
        var profile = builder.Build(floor, sectionLength: 1000, baseElevation: 0);
        var item = FloorStackLayoutItem.CreateDrawAll(floor);

        var lines = builder.BuildBoundaryLineSegments(floor, profile, Array.Empty<(double StartX, double EndX)>(), item);
        var hatches = builder.BuildBoundaryHatchRegions(floor, profile, item);

        lines.Should().HaveCount(6);
        lines.Should().Contain(segment => IsHorizontalAt(segment.Line, -100));
        lines.Should().Contain(segment => IsHorizontalAt(segment.Line, 0));
        lines.Should().Contain(segment => IsHorizontalAt(segment.Line, 3000));
        lines.Should().Contain(segment => IsHorizontalAt(segment.Line, 3200));
        lines.Should().Contain(segment => IsVerticalAt(segment.Line, 0, -100, 3200));
        lines.Should().Contain(segment => IsVerticalAt(segment.Line, 1000, -100, 3200));
        hatches.Should().HaveCount(2);
    }

    [Fact]
    public void BuildBoundaryLineSegments_WhenBottomSuppressed_GeneratesOnlyTopBoundary()
    {
        var builder = new FloorVerticalProfileBuilder();
        var floor = MakeFloor();
        var profile = builder.Build(floor, sectionLength: 1000, baseElevation: 0);
        var item = FloorStackLayoutItem.CreateDrawAll(floor) with
        {
            BottomBoundary = FloorBoundaryContribution.Suppress(FloorBoundaryKind.Bottom)
        };

        var lines = builder.BuildBoundaryLineSegments(floor, profile, Array.Empty<(double StartX, double EndX)>(), item);
        var hatches = builder.BuildBoundaryHatchRegions(floor, profile, item);

        lines.Should().HaveCount(2);
        lines.Should().OnlyContain(segment => segment.Line.Start.Y >= 3000 && segment.Line.End.Y >= 3000);
        lines.Should().Contain(segment => IsHorizontalAt(segment.Line, 3000));
        lines.Should().Contain(segment => IsHorizontalAt(segment.Line, 3200));
        hatches.Should().ContainSingle();
        hatches[0].Boundary.Select(point => point.Y).Should().OnlyContain(y => y >= 3000);
    }

    [Fact]
    public void BuildBoundaryLineSegments_WhenBottomSuppressed_DoesNotGenerateFullVerticalClosures()
    {
        var builder = new FloorVerticalProfileBuilder();
        var floor = MakeFloor();
        var profile = builder.Build(floor, sectionLength: 1000, baseElevation: 0);
        var item = FloorStackLayoutItem.CreateDrawAll(floor) with
        {
            BottomBoundary = FloorBoundaryContribution.Suppress(FloorBoundaryKind.Bottom)
        };

        var lines = builder.BuildBoundaryLineSegments(floor, profile, Array.Empty<(double StartX, double EndX)>(), item);

        lines.Should().NotContain(segment => IsVerticalAt(segment.Line, 0, -100, 3200));
        lines.Should().NotContain(segment => IsVerticalAt(segment.Line, 1000, -100, 3200));
        lines.Should().OnlyContain(segment => Math.Abs(segment.Line.Start.Y - segment.Line.End.Y) <= 1e-6);
    }

    [Fact]
    public void BuildBoundaryLineSegments_WithBottomStructuralSlope_AlignsBottomHatchWithBottomLines()
    {
        var builder = new FloorVerticalProfileBuilder();
        var floor = MakeFloor();
        floor.BottomBoundarySlab = new BoundarySlabConfig
        {
            SlopeEnabled = true,
            SlopeValue = 0.02,
            SlopeTarget = "StructuralSlab"
        };
        var profile = builder.Build(floor, sectionLength: 1000, baseElevation: 0);
        var item = FloorStackLayoutItem.CreateDrawAll(floor) with
        {
            TopBoundary = FloorBoundaryContribution.Suppress(FloorBoundaryKind.Top)
        };

        var lines = builder.BuildBoundaryLineSegments(floor, profile, Array.Empty<(double StartX, double EndX)>(), item);
        var hatches = builder.BuildBoundaryHatchRegions(floor, profile, item);

        lines.Should().HaveCount(2);
        lines.Should().Contain(segment => HasEndpoints(segment.Line, 0, 0, 1000, 20));
        lines.Should().Contain(segment => HasEndpoints(segment.Line, 0, -100, 1000, -80));
        hatches.Should().ContainSingle();
        hatches[0].Boundary.Should().Contain(point => IsPoint(point, 0, 0));
        hatches[0].Boundary.Should().Contain(point => IsPoint(point, 1000, 20));
        hatches[0].Boundary.Should().Contain(point => IsPoint(point, 1000, -80));
        hatches[0].Boundary.Should().Contain(point => IsPoint(point, 0, -100));
    }

    [Fact]
    public void BuildBoundaryLineSegments_WithTopStructuralSlope_AlignsTopHatchWithTopLines()
    {
        var builder = new FloorVerticalProfileBuilder();
        var floor = MakeFloor();
        floor.TopBoundarySlab = new BoundarySlabConfig
        {
            SlopeEnabled = true,
            SlopeValue = 0.02,
            SlopeTarget = "StructuralSlab"
        };
        var profile = builder.Build(floor, sectionLength: 1000, baseElevation: 0);
        var item = FloorStackLayoutItem.CreateDrawAll(floor) with
        {
            BottomBoundary = FloorBoundaryContribution.Suppress(FloorBoundaryKind.Bottom)
        };

        var lines = builder.BuildBoundaryLineSegments(floor, profile, Array.Empty<(double StartX, double EndX)>(), item);
        var hatches = builder.BuildBoundaryHatchRegions(floor, profile, item);

        lines.Should().HaveCount(2);
        lines.Should().Contain(segment => HasEndpoints(segment.Line, 0, 3000, 1000, 3020));
        lines.Should().Contain(segment => HasEndpoints(segment.Line, 0, 3200, 1000, 3220));
        hatches.Should().ContainSingle();
        hatches[0].Boundary.Should().Contain(point => IsPoint(point, 0, 3000));
        hatches[0].Boundary.Should().Contain(point => IsPoint(point, 1000, 3020));
        hatches[0].Boundary.Should().Contain(point => IsPoint(point, 1000, 3220));
        hatches[0].Boundary.Should().Contain(point => IsPoint(point, 0, 3200));
    }

    [Fact]
    public void BuildBoundaryLineSegments_WhenBottomSuppressedWithBottomSlope_DoesNotEmitBottomLinesOrHatch()
    {
        var builder = new FloorVerticalProfileBuilder();
        var floor = MakeFloor();
        floor.BottomBoundarySlab = new BoundarySlabConfig
        {
            SlopeEnabled = true,
            SlopeValue = 0.02,
            SlopeTarget = "StructuralSlab"
        };
        var profile = builder.Build(floor, sectionLength: 1000, baseElevation: 0);
        var item = FloorStackLayoutItem.CreateDrawAll(floor) with
        {
            BottomBoundary = FloorBoundaryContribution.Suppress(FloorBoundaryKind.Bottom)
        };

        var lines = builder.BuildBoundaryLineSegments(floor, profile, Array.Empty<(double StartX, double EndX)>(), item);
        var hatches = builder.BuildBoundaryHatchRegions(floor, profile, item);

        lines.Should().HaveCount(2);
        lines.Should().OnlyContain(segment => segment.Line.Start.Y >= 3000 && segment.Line.End.Y >= 3000);
        lines.Should().Contain(segment => IsHorizontalAt(segment.Line, 3000));
        lines.Should().Contain(segment => IsHorizontalAt(segment.Line, 3200));
        hatches.Should().ContainSingle();
        hatches[0].Boundary.Select(point => point.Y).Should().OnlyContain(y => y >= 3000);
    }

    private static FloorConfig MakeFloor() => new()
    {
        Name = "F1",
        Height = 3000,
        BottomSlabThickness = 100,
        TopSlabThickness = 200,
        FinishThickness = 0
    };

    private static bool IsHorizontalAt(Line3D line, double y)
        => Math.Abs(line.Start.Y - y) <= 1e-6 && Math.Abs(line.End.Y - y) <= 1e-6;

    private static bool IsVerticalAt(Line3D line, double x, double minY, double maxY)
        => Math.Abs(line.Start.X - x) <= 1e-6 &&
           Math.Abs(line.End.X - x) <= 1e-6 &&
           Math.Abs(Math.Min(line.Start.Y, line.End.Y) - minY) <= 1e-6 &&
           Math.Abs(Math.Max(line.Start.Y, line.End.Y) - maxY) <= 1e-6;

    private static bool HasEndpoints(Line3D line, double startX, double startY, double endX, double endY)
        => Math.Abs(line.Start.X - startX) <= 1e-6 &&
           Math.Abs(line.Start.Y - startY) <= 1e-6 &&
           Math.Abs(line.End.X - endX) <= 1e-6 &&
           Math.Abs(line.End.Y - endY) <= 1e-6;

    private static bool IsPoint(Point3D point, double x, double y)
        => Math.Abs(point.X - x) <= 1e-6 && Math.Abs(point.Y - y) <= 1e-6;
}
