using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
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
}
