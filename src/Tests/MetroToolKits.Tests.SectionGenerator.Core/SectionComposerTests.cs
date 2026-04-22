using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;
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
    public void Generate_SlabLines_ContainsSevenLines()
    {
        // 底板顶/底 + 顶板顶/底 + 顶部完成面 + 左封边 + 右封边 = 7
        var composer    = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(6000, 0, 0));

        var data = composer.Generate(sectionLine, ViewDir, Enumerable.Empty<BuildingElement>(), DefaultFloor());

        data.SlabLines.Should().HaveCount(7);
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
            .FirstOrDefault(l => Math.Abs(l.Start.Y - config.Height) < 1e-3);

        topSlabLine.Should().NotBeNull("应存在顶板底面线");
        var delta = Math.Abs(topSlabLine!.End.Y - topSlabLine.Start.Y);
        delta.Should().BeApproximately(60, 1e-3, "坡度差 = 6000 * 0.01 = 60");
        topSlabLine.Start.Z.Should().BeApproximately(0, 1e-6);
        topSlabLine.End.Z.Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void Generate_NoSlope_TopSlabIsHorizontal()
    {
        var composer    = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(6000, 0, 0));

        var data = composer.Generate(sectionLine, ViewDir, Enumerable.Empty<BuildingElement>(), DefaultFloor());

        // 顶板底面线（Y = FloorHeight = 5200）应水平
        var topSlabBottom = data.SlabLines
            .FirstOrDefault(l => Math.Abs(l.Start.Y - 5200) < 1e-3 && Math.Abs(l.End.Y - 5200) < 1e-3);
        topSlabBottom.Should().NotBeNull("无坡度时顶板底面线应水平");
        topSlabBottom!.Start.Z.Should().BeApproximately(0, 1e-6);
        topSlabBottom.End.Z.Should().BeApproximately(0, 1e-6);
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

    [Fact]
    public void Generate_VerticalSectionLine_ProducesFlatLocalSectionGeometry()
    {
        var composer = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(0, 6000, 0));

        var data = composer.Generate(sectionLine, ViewDir, Enumerable.Empty<BuildingElement>(), DefaultFloor());

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
    public void Generate_DiagonalSectionLine_UsesSectionLengthForHorizontalSpan()
    {
        var composer = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(3000, 4000, 0));

        var data = composer.Generate(sectionLine, ViewDir, Enumerable.Empty<BuildingElement>(), DefaultFloor());

        var bottomSlabLine = data.SlabLines
            .First(line => Math.Abs(line.Start.Y + DefaultFloor().BottomSlabThickness) < 1e-6 &&
                           Math.Abs(line.End.Y + DefaultFloor().BottomSlabThickness) < 1e-6);

        bottomSlabLine.Start.X.Should().BeApproximately(0, 1e-6);
        bottomSlabLine.End.X.Should().BeApproximately(5000, 1e-6);
        bottomSlabLine.Start.Z.Should().BeApproximately(0, 1e-6);
        bottomSlabLine.End.Z.Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void Generate_WithCompositeSlab_ShowsCoreLinesAndSingleFinishLine()
    {
        var composer = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(-500, 2000, 0), new Point3D(6500, 2000, 0));
        var slab = CreateCompositeSlab("SLAB-1");

        var data = composer.Generate(sectionLine, ViewDir, new BuildingElement[] { slab }, DefaultFloor());

        data.Elements.Should().ContainSingle(element => element.ElementType == "Slab");
        var slabData = data.Elements.Single(element => element.ElementType == "Slab");
        slabData.CutLines.Should().HaveCount(3, "结构板应保留上下两条线，装修层只保留完成面线");
        slabData.CutLines.Select(line => line.Start.Y).Should().Contain(new[] { 0d, -200d, 50d });
    }

    [Fact]
    public void Generate_WithCompositeSlabAndWall_FinishLineStopsAtWallFace()
    {
        var composer = new SectionComposer();
        var sectionLine = new Line3D(new Point3D(-500, 2000, 0), new Point3D(6500, 2000, 0));
        var slab = CreateCompositeSlab("SLAB-2");
        var wall = CreateCompositeWall();

        var data = composer.Generate(sectionLine, ViewDir, new BuildingElement[] { wall, slab }, DefaultFloor());

        var slabData = data.Elements.Single(element => element.ElementType == "Slab");
        var finishLines = slabData.CutLines.Where(line => Math.Abs(line.Start.Y - 50) < 1e-6).ToList();
        finishLines.Should().HaveCount(2, "装修完成面到墙边应被截断成左右两段");
        finishLines.Should().OnlyContain(line => line.End.X <= 2900 || line.Start.X >= 3100);
    }

    private static CompositeSlabElement CreateCompositeSlab(string handle)
    {
        var builder = new SlabAssemblyBuilder();
        return builder.Build(
            new CoreSlabArea
            {
                TemplateId = "slab-core-finish",
                CoreTopElevation = 0,
                SourceHandles = new List<string> { handle },
                Outline = new List<Point3D>
                {
                    new(0, 0, 0),
                    new(6000, 0, 0),
                    new(6000, 4000, 0),
                    new(0, 4000, 0)
                }
            },
            new SlabAssemblyTemplate
            {
                TemplateId = "slab-core-finish",
                TemplateName = "结构板带面层",
                WallJunctionMode = SlabWallJunctionMode.StopAtWallFace,
                CoreRule = new SlabCoreRule
                {
                    Name = "结构板",
                    Thickness = 200,
                    MaterialOrCategory = "结构"
                },
                TopLayers =
                {
                    new SlabLayerRule
                    {
                        Name = "面层",
                        Side = SlabLayerSide.Top,
                        Order = 1,
                        Thickness = 50,
                        MaterialOrCategory = "装修"
                    }
                }
            });
    }

    private static CompositeWallElement CreateCompositeWall()
    {
        var builder = new WallAssemblyBuilder();
        return builder.Build(
            new CoreWallSegment
            {
                StartPoint = new Point3D(3000, 0, 0),
                EndPoint = new Point3D(3000, 4000, 0),
                Thickness = 200,
                Height = 3000,
                BaseElevation = 0,
                TemplateId = "wall-structural",
                SourceHandles = new List<string> { "WA", "WB" }
            },
            new WallAssemblyTemplate
            {
                TemplateId = "wall-structural",
                TemplateName = "结构墙",
                VerticalAnchorMode = WallVerticalAnchorMode.StructuralSlabFaces,
                CoreRule = new WallCoreRule
                {
                    Name = "结构芯",
                    Thickness = 200,
                    MaterialOrCategory = "结构"
                }
            });
    }
}
