using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class MultiFloorSectionComposerTests
{
    private static readonly Vector3D ViewDir = new(0, 1, 0);
    private static readonly Line3D SectionLine =
        new(new Point3D(-500, 2500, 0), new Point3D(6500, 2500, 0));

    private static FloorConfig MakeFloor(string name, double height = 3000,
        double bottomSlab = 800, double topSlab = 600) => new()
    {
        Name                = name,
        Height              = height,
        BottomSlabThickness = bottomSlab,
        TopSlabThickness    = topSlab,
        FinishThickness     = 120
    };

    private static MultiFloorSectionComposer MakeComposer()
        => new(new SectionComposer());

    // ── 楼层数量 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_TwoFloors_ReturnsTwoFloorData()
    {
        var composer = MakeComposer();
        var floors   = new[] { MakeFloor("F1"), MakeFloor("F2") };
        var elements = new Dictionary<string, IReadOnlyList<BuildingElement>>();

        var result = composer.Generate(SectionLine, ViewDir, elements, floors);

        result.Floors.Should().HaveCount(2);
    }

    [Fact]
    public void Generate_SingleFloor_ReturnsSingleFloorData()
    {
        var composer = MakeComposer();
        var floors   = new[] { MakeFloor("F1") };
        var elements = new Dictionary<string, IReadOnlyList<BuildingElement>>();

        var result = composer.Generate(SectionLine, ViewDir, elements, floors);

        result.Floors.Should().HaveCount(1);
    }

    // ── 楼层名称 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_FloorNames_MatchConfig()
    {
        var composer = MakeComposer();
        var floors   = new[] { MakeFloor("F1"), MakeFloor("F2"), MakeFloor("F3") };
        var elements = new Dictionary<string, IReadOnlyList<BuildingElement>>();

        var result = composer.Generate(SectionLine, ViewDir, elements, floors);

        result.Floors.Select(f => f.FloorName)
            .Should().BeEquivalentTo(new[] { "F1", "F2", "F3" }, o => o.WithStrictOrdering());
    }

    // ── Z 轴堆叠 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_TwoFloors_SecondFloorBaseElevationEqualsFirstFloorTotal()
    {
        var composer = MakeComposer();
        // F1: bottomSlab=800, height=3000, topSlab=600 → 累计 = 4400
        var f1 = MakeFloor("F1", height: 3000, bottomSlab: 800, topSlab: 600);
        var f2 = MakeFloor("F2", height: 4000, bottomSlab: 1000, topSlab: 700);
        var elements = new Dictionary<string, IReadOnlyList<BuildingElement>>();

        var result = composer.Generate(SectionLine, ViewDir, elements, new[] { f1, f2 });

        result.Floors[0].BaseElevation.Should().Be(0, "F1 从 0 开始");
        result.Floors[1].BaseElevation.Should().Be(4400, "F2 = F1.bottomSlab + F1.height + F1.topSlab = 800+3000+600");
    }

    [Fact]
    public void Generate_ThreeFloors_ElevationsAccumulate()
    {
        var composer = MakeComposer();
        var f1 = MakeFloor("F1", height: 3000, bottomSlab: 800, topSlab: 600); // 累计 4400
        var f2 = MakeFloor("F2", height: 3000, bottomSlab: 800, topSlab: 600); // 累计 8800
        var f3 = MakeFloor("F3", height: 3000, bottomSlab: 800, topSlab: 600);
        var elements = new Dictionary<string, IReadOnlyList<BuildingElement>>();

        var result = composer.Generate(SectionLine, ViewDir, elements, new[] { f1, f2, f3 });

        result.Floors[0].BaseElevation.Should().Be(0);
        result.Floors[1].BaseElevation.Should().Be(4400);
        result.Floors[2].BaseElevation.Should().Be(8800);
    }

    // ── 总高度 ────────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_TotalHeight_EqualsSumOfAllFloors()
    {
        var composer = MakeComposer();
        // F1: 800+3000+600=4400, F2: 1000+4000+700=5700 → 总计 10100
        var f1 = MakeFloor("F1", height: 3000, bottomSlab: 800, topSlab: 600);
        var f2 = MakeFloor("F2", height: 4000, bottomSlab: 1000, topSlab: 700);
        var elements = new Dictionary<string, IReadOnlyList<BuildingElement>>();

        var result = composer.Generate(SectionLine, ViewDir, elements, new[] { f1, f2 });

        result.TotalHeight.Should().Be(10100);
    }

    // ── 含构件 ────────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_WithWallInF1_F1HasElementData()
    {
        var composer = MakeComposer();
        var floors   = new[] { MakeFloor("F1"), MakeFloor("F2") };

        var wall = new Wall
        {
            StartPoint = new Point3D(0, 0, 0),
            EndPoint   = new Point3D(0, 5000, 0),
            Height     = 3000
        };

        var elements = new Dictionary<string, IReadOnlyList<BuildingElement>>
        {
            ["F1"] = new[] { wall }
        };

        var result = composer.Generate(SectionLine, ViewDir, elements, floors);

        result.Floors[0].Elements.Should().HaveCount(1, "F1 有一堵墙");
        result.Floors[1].Elements.Should().BeEmpty("F2 无构件");
    }

    // ── 无对齐点时直接使用原剖切线 ───────────────────────────────────────────

    [Fact]
    public void Generate_NoAlignmentPoints_UsesOriginalSectionLine()
    {
        var composer = MakeComposer();
        var floor    = MakeFloor("F1"); // 无对齐点
        var elements = new Dictionary<string, IReadOnlyList<BuildingElement>>();

        // 不应抛出异常
        var act = () => composer.Generate(SectionLine, ViewDir, elements, new[] { floor });
        act.Should().NotThrow();
    }

    // ── 对齐变换 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_WithIdentityAlignment_SectionLineUnchanged()
    {
        var composer = MakeComposer();
        var floor    = MakeFloor("F1");

        // 恒等对齐（源=目标）
        floor.AlignmentSourcePoints = new List<Point3D>
        {
            new(0, 0, 0), new(1000, 0, 0), new(0, 1000, 0)
        };
        floor.AlignmentTargetPoints = new List<Point3D>
        {
            new(0, 0, 0), new(1000, 0, 0), new(0, 1000, 0)
        };

        var elements = new Dictionary<string, IReadOnlyList<BuildingElement>>();
        var result   = composer.Generate(SectionLine, ViewDir, elements, new[] { floor });

        // 恒等变换不改变楼板线数量
        result.Floors[0].SlabLines.Should().HaveCount(6);
    }

    // ── AllCutLines / AllSightLines ───────────────────────────────────────────

    [Fact]
    public void AllCutLines_IncludesAllFloors()
    {
        var composer = MakeComposer();
        var floors   = new[] { MakeFloor("F1"), MakeFloor("F2") };
        var elements = new Dictionary<string, IReadOnlyList<BuildingElement>>();

        var result = composer.Generate(SectionLine, ViewDir, elements, floors);

        // 每层 6 条楼板线，两层共 12 条
        result.AllCutLines.Count().Should().Be(12);
    }
}
