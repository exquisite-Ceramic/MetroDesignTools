using FluentAssertions;
using MetroToolKits.Foundation.Building.Types;

namespace MetroToolKits.Tests.Foundation.Building;

public class ElementTypeLoaderTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"ElementTypes_{Guid.NewGuid()}.json");

    // ── 默认值 ────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_NoFile_CreatesDefaultTypes()
    {
        var loader = new ElementTypeLoader(_tempFile);
        var types = loader.Load();

        types.Should().NotBeEmpty();
        types.Should().Contain(t => t.TypeId == "Wall");
        types.Should().Contain(t => t.TypeId == "Slab");
    }

    [Fact]
    public void Load_NoFile_DefaultTypesHaveShortcutKeys()
    {
        var loader = new ElementTypeLoader(_tempFile);
        var types = loader.Load();

        types.Should().AllSatisfy(t => t.ShortcutKey.Should().NotBeNullOrEmpty());
    }

    // ── JSON 反序列化 ─────────────────────────────────────────────────────────

    [Fact]
    public void Load_ValidJson_DeserializesCorrectly()
    {
        var json = """
            [
              { "TypeId": "TestWall", "TypeName": "测试墙", "ShortcutKey": "X",
                "TargetLayerPrefix": "MK_测试墙", "LayerColorIndex": 9, "IsEnabled": true }
            ]
            """;
        File.WriteAllText(_tempFile, json);

        var loader = new ElementTypeLoader(_tempFile);
        var types = loader.Load();

        types.Should().HaveCount(1);
        types[0].TypeId.Should().Be("TestWall");
        types[0].ShortcutKey.Should().Be("X");
    }

    [Fact]
    public void Load_InvalidJson_FallsBackToDefaults()
    {
        File.WriteAllText(_tempFile, "{ invalid json }");
        var loader = new ElementTypeLoader(_tempFile);
        var types = loader.Load();

        types.Should().NotBeEmpty();
    }

    // ── 快捷键查询 ────────────────────────────────────────────────────────────

    [Fact]
    public void GetByShortcutKey_ExistingKey_ReturnsType()
    {
        var loader = new ElementTypeLoader(_tempFile);
        var type = loader.GetByShortcutKey("W");

        type.Should().NotBeNull();
        type!.TypeId.Should().Be("Wall");
    }

    [Fact]
    public void GetByShortcutKey_CaseInsensitive()
    {
        var loader = new ElementTypeLoader(_tempFile);
        loader.GetByShortcutKey("w").Should().NotBeNull();
        loader.GetByShortcutKey("W").Should().NotBeNull();
    }

    [Fact]
    public void GetByShortcutKey_NonExistentKey_ReturnsNull()
    {
        var loader = new ElementTypeLoader(_tempFile);
        loader.GetByShortcutKey("Z").Should().BeNull();
    }

    // ── 缓存 ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_CalledTwice_ReturnsSameContent()
    {
        var loader = new ElementTypeLoader(_tempFile);
        var first  = loader.Load();
        var second = loader.Load();
        // 内容相同（缓存生效），数量一致
        first.Count.Should().Be(second.Count);
        first.Select(t => t.TypeId).Should().BeEquivalentTo(second.Select(t => t.TypeId));
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }
}
