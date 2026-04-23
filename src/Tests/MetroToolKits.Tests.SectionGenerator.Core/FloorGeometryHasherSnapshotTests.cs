using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

/// <summary>
/// 验证 FloorGeometryHasher 与快照场景的集成
/// </summary>
public class FloorGeometryHasherSnapshotTests
{
    private static Wall MakeWall(double x1, double y1, double x2, double y2,
        string handle = "AAA") => new()
    {
        StartPoint    = new Point3D(x1, y1, 0),
        EndPoint      = new Point3D(x2, y2, 0),
        Height        = 3000,
        Thickness     = 200,
        SourceHandle  = handle
    };

    // ── 快照构建场景 ──────────────────────────────────────────────────────────

    [Fact]
    public void Hash_AfterBuildingSnapshot_MatchesStoredHash()
    {
        var hasher   = new FloorGeometryHasher();
        var elements = new[] { MakeWall(0, 0, 0, 5000) };

        var hash = hasher.ComputeHash(elements);

        var snapshot = new FloorSnapshot
        {
            FloorName    = "F1",
            GeometryHash = hash,
            ElementCount = elements.Length
        };

        // 相同构件重新计算，应与快照一致
        hasher.ComputeHash(elements).Should().Be(snapshot.GeometryHash);
    }

    [Fact]
    public void Hash_AfterElementMoved_DiffersFromSnapshot()
    {
        var hasher = new FloorGeometryHasher();
        var wall   = MakeWall(0, 0, 0, 5000);

        var snapshotHash = hasher.ComputeHash(new[] { wall });

        // 模拟平面修改：移动墙体
        wall.StartPoint = new Point3D(500, 0, 0);
        wall.EndPoint   = new Point3D(500, 5000, 0);

        var currentHash = hasher.ComputeHash(new[] { wall });
        currentHash.Should().NotBe(snapshotHash, "移动构件后哈希应变化");
    }

    [Fact]
    public void Hash_AfterElementAdded_DiffersFromSnapshot()
    {
        var hasher = new FloorGeometryHasher();
        var wall1  = MakeWall(0, 0, 0, 5000, "AAA");

        var snapshotHash = hasher.ComputeHash(new[] { wall1 });

        var wall2       = MakeWall(1000, 0, 1000, 5000, "BBB");
        var currentHash = hasher.ComputeHash(new[] { wall1, wall2 });

        currentHash.Should().NotBe(snapshotHash, "增加构件后哈希应变化");
    }

    [Fact]
    public void Hash_AfterElementRemoved_DiffersFromSnapshot()
    {
        var hasher = new FloorGeometryHasher();
        var wall1  = MakeWall(0, 0, 0, 5000, "AAA");
        var wall2  = MakeWall(1000, 0, 1000, 5000, "BBB");

        var snapshotHash = hasher.ComputeHash(new[] { wall1, wall2 });
        var currentHash  = hasher.ComputeHash(new[] { wall1 });

        currentHash.Should().NotBe(snapshotHash, "删除构件后哈希应变化");
    }

    // ── 多楼层快照场景 ────────────────────────────────────────────────────────

    [Fact]
    public void MultiFloorSnapshot_EachFloorHasIndependentHash()
    {
        var hasher = new FloorGeometryHasher();
        var f1Wall = MakeWall(0, 0, 0, 5000, "F1W1");
        var f2Wall = MakeWall(100, 0, 100, 5000, "F2W1"); // 不同位置

        var hashF1 = hasher.ComputeHash(new[] { f1Wall });
        var hashF2 = hasher.ComputeHash(new[] { f2Wall });

        hashF1.Should().NotBe(hashF2, "不同楼层构件位置不同，哈希应不同");
    }

    [Fact]
    public void MultiFloorSnapshot_OnlyF2Changed_F1HashUnchanged()
    {
        var hasher = new FloorGeometryHasher();
        var f1Wall = MakeWall(0, 0, 0, 5000, "F1W1");
        var f2Wall = MakeWall(100, 0, 100, 5000, "F2W1");

        var hashF1Before = hasher.ComputeHash(new[] { f1Wall });

        // 只修改 F2
        f2Wall.StartPoint = new Point3D(200, 0, 0);
        f2Wall.EndPoint   = new Point3D(200, 5000, 0);

        var hashF1After = hasher.ComputeHash(new[] { f1Wall });

        hashF1After.Should().Be(hashF1Before, "F2 变化不影响 F1 的哈希");
    }

    // ── 空构件列表 ────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyElements_ProducesConsistentHash()
    {
        var hasher = new FloorGeometryHasher();
        var h1 = hasher.ComputeHash(Enumerable.Empty<BuildingElement>());
        var h2 = hasher.ComputeHash(Enumerable.Empty<BuildingElement>());
        h1.Should().Be(h2);
    }

    [Fact]
    public void EmptyElements_HashDiffersFromNonEmpty()
    {
        var hasher   = new FloorGeometryHasher();
        var emptyH   = hasher.ComputeHash(Enumerable.Empty<BuildingElement>());
        var nonEmptyH = hasher.ComputeHash(new[] { MakeWall(0, 0, 0, 5000) });
        emptyH.Should().NotBe(nonEmptyH);
    }

    [Fact]
    public void Hash_OutputConfigChanged_DiffersFromSnapshot()
    {
        var hasher = new FloorGeometryHasher();
        var elements = new[] { MakeWall(0, 0, 0, 5000) };
        var configA = new SectionOutputConfig
        {
            AnnotationOptions = new AnnotationOptions { GenerateAnnotations = false }
        };
        var configB = new SectionOutputConfig
        {
            AnnotationOptions = new AnnotationOptions { GenerateAnnotations = true }
        };

        var hashA = SectionOutputConfigHasher.Combine(hasher.ComputeHash(elements), configA);
        var hashB = SectionOutputConfigHasher.Combine(hasher.ComputeHash(elements), configB);

        hashB.Should().NotBe(hashA, "输出配置变化后哈希应变化");
    }
}
