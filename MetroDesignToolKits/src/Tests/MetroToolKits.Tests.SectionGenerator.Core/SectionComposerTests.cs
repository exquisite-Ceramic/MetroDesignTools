using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class SectionComposerTests
{
    private static readonly Vector3D ViewDir = new(0, 1, 0);

    private static FloorConfig DefaultFloor(bool hasSlope = false, double slopeValue = 0) => new()
    {
        Name                 = "F1",
        Height               = 5200,
        BottomSlabThickness  = 800,
        TopSlabThickness     = 600,
        FinishThickness      = 120,
        HasSlope             = hasSlope,
        SlopeValue           = slopeValue
    };

    // ── 空构件列表 ────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_NoElements_ReturnsSlabLinesOnly()
    {
        var composer    = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(6000, 0, 0));

        var data = composer.Generate(sectionLine, ViewDir, Enumerable.Empty<BuildingElement>(), DefaultFloor());

        data.Elements.Should().BeEmpty();
        data.SlabLines.Should().NotBeEmpty("楼板线应始终生成");
    }

    // ── 楼板线数量 ────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_SlabLines_ContainsSixLines()
    {
        // 底板顶/底 + 顶板顶/底 + 左封边 + 右封边 = 6
        var composer    = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(6000, 0, 0));

        var data = composer.Generate(sectionLine, ViewDir, Enumerable.Empty<BuildingElement>(), DefaultFloor());

        data.SlabLines.Should().HaveCount(6);
    }

    // ── 楼层名称 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_FloorName_MatchesConfig()
    {
        var composer    = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(6000, 0, 0));

        var data = composer.Generate(sectionLine, ViewDir, Enumerable.Empty<BuildingElement>(), DefaultFloor());

        data.FloorName.Should().Be("F1");
    }

    // ── 基础标高 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_BaseElevation_StoredCorrectly()
    {
        var composer    = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(6000, 0, 0));

        var data = composer.Generate(sectionLine, ViewDir, Enumerable.Empty<BuildingElement>(), DefaultFloor(), baseElevation: 1000);

        data.BaseElevation.Should().Be(1000);
    }

    // ── 坡度处理 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_WithSlope_TopSlabHasHeightDifference()
    {
        var composer    = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(6000, 0, 0));
        var config      = DefaultFloor(hasSlope: true, slopeValue: 0.01); // 1%

        var data = composer.Generate(sectionLine, ViewDir, Enumerable.Empty<BuildingElement>(), config);

        // 顶板线的起终点 Z 应有差值 = 6000 * 0.01 = 60
        var topSlabLine = data.SlabLines
            .FirstOrDefault(l => Math.Abs(l.Start.Z - config.Height) < 1e-3);

        topSlabLine.Should().NotBeNull("应存在顶板底面线");
        var delta = Math.Abs(topSlabLine!.End.Z - topSlabLine.Start.Z);
        delta.Should().BeApproximately(60, 1e-3, "坡度差 = 6000 * 0.01 = 60");
    }

    [Fact]
    public void Generate_NoSlope_TopSlabIsHorizontal()
    {
        var composer    = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(6000, 0, 0));

        var data = composer.Generate(sectionLine, ViewDir, Enumerable.Empty<BuildingElement>(), DefaultFloor());

        // 顶板底面线（Z = FloorHeight = 5200）应水平
        var topSlabBottom = data.SlabLines
            .FirstOrDefault(l => Math.Abs(l.Start.Z - 5200) < 1e-3 && Math.Abs(l.End.Z - 5200) < 1e-3);
        topSlabBottom.Should().NotBeNull("无坡度时顶板底面线应水平");
    }

    // ── 含墙体构件 ────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_WithWall_ElementDataIncluded()
    {
        var composer    = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(-500, 2500, 0), new Point3D(6500, 2500, 0));

        var wall = new Wall
        {
            StartPoint    = new Point3D(0, 0, 0),
            EndPoint      = new Point3D(0, 5000, 0),
            Height        = 3000,
            Thickness     = 200,
            BaseElevation = 0
        };

        var data = composer.Generate(sectionLine, ViewDir, new[] { wall }, DefaultFloor());

        data.Elements.Should().HaveCount(1);
        data.Elements[0].ElementType.Should().Be("Wall");
        data.Elements[0].CutLines.Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_WallMissesSectionLine_ElementNotIncluded()
    {
        var composer    = new SectionComposer();
        // 剖切线在 Y=8000，墙体在 Y=0~5000，不相交
        var sectionLine = new Line3D(new Point3D(-500, 8000, 0), new Point3D(6500, 8000, 0));

        var wall = new Wall
        {
            StartPoint = new Point3D(0, 0, 0),
            EndPoint   = new Point3D(0, 5000, 0),
            Height     = 3000
        };

        var data = composer.Generate(sectionLine, ViewDir, new[] { wall }, DefaultFloor());

        data.Elements.Should().BeEmpty("墙体不与剖切线相交，不应产生剖面数据");
    }

    // ── AllCutLines / AllSightLines ───────────────────────────────────────────

    [Fact]
    public void AllCutLines_IncludesSlabAndElementLines()
    {
        var composer    = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(-500, 2500, 0), new Point3D(6500, 2500, 0));
        var wall        = new Wall { StartPoint = new Point3D(0, 0, 0), EndPoint = new Point3D(0, 5000, 0), Height = 3000 };

        var data = composer.Generate(sectionLine, ViewDir, new[] { wall }, DefaultFloor());

        data.AllCutLines.Should().NotBeEmpty();
        // 至少包含楼板线（6条）+ 墙体线（1条）
        data.AllCutLines.Count().Should().BeGreaterThanOrEqualTo(7);
    }
}
