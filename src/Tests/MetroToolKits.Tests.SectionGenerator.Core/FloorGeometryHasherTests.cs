using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FloorGeometryHasherTests
{
    private static Wall MakeWall(double x1, double y1, double x2, double y2) => new()
    {
        StartPoint = new Point3D(x1, y1, 0),
        EndPoint   = new Point3D(x2, y2, 0),
        Height     = 3000,
        Thickness  = 200
    };

    // ── 相同几何 → 相同哈希 ───────────────────────────────────────────────────

    [Fact]
    public void SameGeometry_ProducesSameHash()
    {
        var hasher = new FloorGeometryHasher();
        var list1  = new List<BuildingElement> { MakeWall(0, 0, 0, 5000) };
        var list2  = new List<BuildingElement> { MakeWall(0, 0, 0, 5000) };

        // 需要相同 SourceHandle 才能排序一致
        list1[0].SourceHandle = "AAA";
        list2[0].SourceHandle = "AAA";

        hasher.ComputeHash(list1).Should().Be(hasher.ComputeHash(list2));
    }

    // ── 移动构件 → 哈希变化 ───────────────────────────────────────────────────

    [Fact]
    public void MovedElement_ProducesDifferentHash()
    {
        var hasher = new FloorGeometryHasher();
        var wall   = MakeWall(0, 0, 0, 5000);
        wall.SourceHandle = "AAA";

        var hash1 = hasher.ComputeHash(new[] { wall });

        wall.StartPoint = new Point3D(100, 0, 0);
        wall.EndPoint   = new Point3D(100, 5000, 0);
        var hash2 = hasher.ComputeHash(new[] { wall });

        hash1.Should().NotBe(hash2);
    }

    // ── 增加构件 → 哈希变化 ───────────────────────────────────────────────────

    [Fact]
    public void AddedElement_ProducesDifferentHash()
    {
        var hasher = new FloorGeometryHasher();
        var wall   = MakeWall(0, 0, 0, 5000);
        wall.SourceHandle = "AAA";

        var hash1 = hasher.ComputeHash(new[] { wall });

        var wall2 = MakeWall(1000, 0, 1000, 5000);
        wall2.SourceHandle = "BBB";
        var hash2 = hasher.ComputeHash(new[] { wall, wall2 });

        hash1.Should().NotBe(hash2);
    }

    // ── 删除构件 → 哈希变化 ───────────────────────────────────────────────────

    [Fact]
    public void RemovedElement_ProducesDifferentHash()
    {
        var hasher = new FloorGeometryHasher();
        var w1 = MakeWall(0, 0, 0, 5000);    w1.SourceHandle = "AAA";
        var w2 = MakeWall(1000, 0, 1000, 5000); w2.SourceHandle = "BBB";

        var hash1 = hasher.ComputeHash(new[] { w1, w2 });
        var hash2 = hasher.ComputeHash(new[] { w1 });

        hash1.Should().NotBe(hash2);
    }

    // ── 空列表 ────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyList_ReturnsConsistentHash()
    {
        var hasher = new FloorGeometryHasher();
        var h1 = hasher.ComputeHash(Enumerable.Empty<BuildingElement>());
        var h2 = hasher.ComputeHash(Enumerable.Empty<BuildingElement>());
        h1.Should().Be(h2);
    }

    // ── 哈希格式 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_IsHexString_Length16()
    {
        var hasher = new FloorGeometryHasher();
        var hash   = hasher.ComputeHash(Enumerable.Empty<BuildingElement>());
        hash.Should().HaveLength(16);
        hash.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void CompositeWall_TemplateChange_ProducesDifferentHash()
    {
        var hasher = new FloorGeometryHasher();
        var wallA = CreateCompositeWall("wall-a", 20, 20);
        var wallB = CreateCompositeWall("wall-b", 20, 20);

        var hashA = hasher.ComputeHash(new BuildingElement[] { wallA });
        var hashB = hasher.ComputeHash(new BuildingElement[] { wallB });

        hashA.Should().NotBe(hashB);
    }

    [Fact]
    public void CompositeWall_LayerThicknessChange_ProducesDifferentHash()
    {
        var hasher = new FloorGeometryHasher();
        var wallA = CreateCompositeWall("wall-a", 20, 20);
        var wallB = CreateCompositeWall("wall-a", 40, 20);

        var hashA = hasher.ComputeHash(new BuildingElement[] { wallA });
        var hashB = hasher.ComputeHash(new BuildingElement[] { wallB });

        hashA.Should().NotBe(hashB);
    }

    [Fact]
    public void CompositeWall_VerticalAnchorModeChange_ProducesDifferentHash()
    {
        var hasher = new FloorGeometryHasher();
        var wallA = CreateCompositeWall("wall-a", 20, 20, WallVerticalAnchorMode.StructuralSlabFaces);
        var wallB = CreateCompositeWall("wall-a", 20, 20, WallVerticalAnchorMode.FinishSurfaceFaces);

        var hashA = hasher.ComputeHash(new BuildingElement[] { wallA });
        var hashB = hasher.ComputeHash(new BuildingElement[] { wallB });

        hashA.Should().NotBe(hashB);
    }

    private static CompositeWallElement CreateCompositeWall(
        string templateId,
        double leftThickness,
        double rightThickness,
        WallVerticalAnchorMode verticalAnchorMode = WallVerticalAnchorMode.StructuralSlabFaces)
    {
        var builder = new WallAssemblyBuilder();
        return builder.Build(
            new CoreWallSegment
            {
                StartPoint = new Point3D(0, 0, 0),
                EndPoint = new Point3D(5000, 0, 0),
                Thickness = 200,
                Height = 3000,
                BaseElevation = 0,
                TemplateId = templateId,
                SourceHandles = new List<string> { "AAA", "AAB" }
            },
            new WallAssemblyTemplate
            {
                TemplateId = templateId,
                TemplateName = "复合墙",
                VerticalAnchorMode = verticalAnchorMode,
                CoreRule = new WallCoreRule
                {
                    Name = "结构芯",
                    Thickness = 200,
                    MaterialOrCategory = "结构"
                },
                LeftLayers =
                {
                    new WallLayerRule
                    {
                        Name = "左附加层",
                        Side = WallLayerSide.Left,
                        Order = 1,
                        Thickness = leftThickness,
                        MaterialOrCategory = "抹灰"
                    }
                },
                RightLayers =
                {
                    new WallLayerRule
                    {
                        Name = "右附加层",
                        Side = WallLayerSide.Right,
                        Order = 1,
                        Thickness = rightThickness,
                        MaterialOrCategory = "抹灰"
                    }
                }
            });
    }
}
