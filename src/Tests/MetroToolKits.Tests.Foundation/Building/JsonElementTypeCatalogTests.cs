using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.SectionGenerator.Infrastructure.Repositories;

namespace MetroToolKits.Tests.Foundation.Building;

public sealed class JsonElementTypeCatalogTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"ElementTypes_{Guid.NewGuid():N}.json");

    [Fact]
    public void GetAllTypes_NoFile_CreatesDefaultTypes()
    {
        var catalog = CreateCatalog();

        var types = catalog.GetAllTypes();

        types.Should().NotBeEmpty();
        types.Should().Contain(type => type.TypeId == "Wall");
        types.Should().Contain(type => type.TypeId == "Beam");
    }

    [Fact]
    public void GetAllTypes_ValidJson_DeserializesCorrectly()
    {
        File.WriteAllText(_tempFile, """
            [
              { "TypeId": "TestWall", "TypeName": "测试墙", "ShortcutKey": "X",
                "TargetLayerPrefix": "MK_测试墙", "LayerColorIndex": 9, "IsEnabled": true }
            ]
            """);

        var catalog = CreateCatalog();

        var types = catalog.GetAllTypes();

        types.Should().HaveCount(1);
        types[0].TypeId.Should().Be("TestWall");
        types[0].ShortcutKey.Should().Be("X");
    }

    [Fact]
    public void GetEnabledTypes_ReturnsOnlyEnabledTypes()
    {
        File.WriteAllText(_tempFile, """
            [
              { "TypeId": "Enabled", "TypeName": "启用", "ShortcutKey": "E", "IsEnabled": true },
              { "TypeId": "Disabled", "TypeName": "禁用", "ShortcutKey": "D", "IsEnabled": false }
            ]
            """);

        var catalog = CreateCatalog();

        var types = catalog.GetEnabledTypes();

        types.Should().ContainSingle();
        types[0].TypeId.Should().Be("Enabled");
    }

    [Fact]
    public void GetByShortcutKey_IsCaseInsensitive()
    {
        var catalog = CreateCatalog();

        catalog.GetByShortcutKey("w").Should().NotBeNull();
        catalog.GetByShortcutKey("W").Should().NotBeNull();
    }

    [Fact]
    public void GetByTypeId_UnknownType_ReturnsNull()
    {
        var catalog = CreateCatalog();

        catalog.GetByTypeId("Missing").Should().BeNull();
    }

    private JsonElementTypeCatalog CreateCatalog()
        => new(_tempFile, templatePath: null, NullLogger<JsonElementTypeCatalog>.Instance);

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }
}
