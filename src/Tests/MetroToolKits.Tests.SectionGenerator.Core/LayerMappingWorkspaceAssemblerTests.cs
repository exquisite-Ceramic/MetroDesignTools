using FluentAssertions;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Cad.Layering.Services;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Core.Sections;
using NSubstitute;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class LayerMappingWorkspaceAssemblerTests
{
    private readonly ILayerService _layerService = Substitute.For<ILayerService>();
    private readonly IElementTypeCatalog _elementTypeCatalog = Substitute.For<IElementTypeCatalog>();
    private readonly IWallAssemblyTemplateCatalog _wallTemplateCatalog = Substitute.For<IWallAssemblyTemplateCatalog>();
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog = Substitute.For<ISlabAssemblyTemplateCatalog>();

    [Fact]
    public void Assemble_MapsLayerListInSortedOrder()
    {
        var assembler = CreateAssembler();
        _layerService.GetAllLayerNames().Returns(["MK_Z", "mk_a", "MK_B"]);
        _elementTypeCatalog.GetAllTypes().Returns(Array.Empty<ElementTypeDefinition>());
        _wallTemplateCatalog.GetAllTemplates().Returns(Array.Empty<WallAssemblyTemplate>());
        _slabTemplateCatalog.GetAllTemplates().Returns(Array.Empty<SlabAssemblyTemplate>());

        var workspace = assembler.Assemble();

        workspace.Layers.Select(layer => layer.LayerName).Should().Equal("mk_a", "MK_B", "MK_Z");
    }

    [Fact]
    public void Assemble_MapsEnabledElementTypes()
    {
        var assembler = CreateAssembler();
        _layerService.GetAllLayerNames().Returns(Array.Empty<string>());
        _elementTypeCatalog.GetAllTypes().Returns(
        [
            new ElementTypeDefinition
            {
                TypeId = "Wall",
                TypeName = "墙",
                ShortcutKey = "W",
                TargetLayerPrefix = "MK_结构墙",
                LayerColorIndex = 1,
                IsEnabled = true
            },
            new ElementTypeDefinition
            {
                TypeId = "Door",
                TypeName = "门",
                ShortcutKey = "D",
                TargetLayerPrefix = "MK_门",
                LayerColorIndex = 4,
                IsEnabled = false
            }
        ]);
        _wallTemplateCatalog.GetAllTemplates().Returns(Array.Empty<WallAssemblyTemplate>());
        _slabTemplateCatalog.GetAllTemplates().Returns(Array.Empty<SlabAssemblyTemplate>());

        var workspace = assembler.Assemble();

        workspace.ElementTypes.Should().ContainSingle();
        workspace.ElementTypes[0].TypeId.Should().Be("Wall");
        workspace.ElementTypes[0].ShortcutKey.Should().Be("W");
        workspace.ElementTypes[0].TargetLayerPrefix.Should().Be("MK_结构墙");
        workspace.ElementTypes[0].ColorIndex.Should().Be(1);
    }

    [Fact]
    public void Assemble_MapsWallTemplateOptions()
    {
        var assembler = CreateAssembler();
        _layerService.GetAllLayerNames().Returns(Array.Empty<string>());
        _elementTypeCatalog.GetAllTypes().Returns(Array.Empty<ElementTypeDefinition>());
        _wallTemplateCatalog.GetAllTemplates().Returns(
        [
            new WallAssemblyTemplate { TemplateId = "wall-1", TemplateName = "墙模板 1" },
            new WallAssemblyTemplate { TemplateId = "wall-2", TemplateName = "墙模板 2" }
        ]);
        _slabTemplateCatalog.GetAllTemplates().Returns(Array.Empty<SlabAssemblyTemplate>());

        var workspace = assembler.Assemble();

        workspace.WallTemplateOptions.Select(option => option.TemplateId).Should().Equal("wall-1", "wall-2");
        workspace.WallTemplateOptions.Select(option => option.TemplateName).Should().Equal("墙模板 1", "墙模板 2");
    }

    [Fact]
    public void Assemble_MapsSlabTemplateOptions()
    {
        var assembler = CreateAssembler();
        _layerService.GetAllLayerNames().Returns(Array.Empty<string>());
        _elementTypeCatalog.GetAllTypes().Returns(Array.Empty<ElementTypeDefinition>());
        _wallTemplateCatalog.GetAllTemplates().Returns(Array.Empty<WallAssemblyTemplate>());
        _slabTemplateCatalog.GetAllTemplates().Returns(
        [
            new SlabAssemblyTemplate { TemplateId = "slab-1", TemplateName = "板模板 1" }
        ]);

        var workspace = assembler.Assemble();

        workspace.SlabTemplateOptions.Should().ContainSingle();
        workspace.SlabTemplateOptions[0].TemplateId.Should().Be("slab-1");
        workspace.SlabTemplateOptions[0].TemplateName.Should().Be("板模板 1");
    }

    [Fact]
    public void Assemble_WhenTemplateCatalogsEmpty_DoesNotThrow()
    {
        var assembler = CreateAssembler();
        _layerService.GetAllLayerNames().Returns(["L1"]);
        _elementTypeCatalog.GetAllTypes().Returns(Array.Empty<ElementTypeDefinition>());
        _wallTemplateCatalog.GetAllTemplates().Returns(Array.Empty<WallAssemblyTemplate>());
        _slabTemplateCatalog.GetAllTemplates().Returns(Array.Empty<SlabAssemblyTemplate>());

        var action = () => assembler.Assemble();
        action.Should().NotThrow();

        var workspace = assembler.Assemble();
        workspace.WallTemplateOptions.Should().BeEmpty();
        workspace.SlabTemplateOptions.Should().BeEmpty();
    }

    [Fact]
    public void Assemble_BuildsExpectedSummaryText()
    {
        var assembler = CreateAssembler();
        _layerService.GetAllLayerNames().Returns(["A", "B"]);
        _elementTypeCatalog.GetAllTypes().Returns(
        [
            new ElementTypeDefinition { TypeId = "Wall", TypeName = "墙", IsEnabled = true },
            new ElementTypeDefinition { TypeId = "Slab", TypeName = "板", IsEnabled = true }
        ]);
        _wallTemplateCatalog.GetAllTemplates().Returns(
        [
            new WallAssemblyTemplate { TemplateId = "w1", TemplateName = "墙模板" }
        ]);
        _slabTemplateCatalog.GetAllTemplates().Returns(
        [
            new SlabAssemblyTemplate { TemplateId = "s1", TemplateName = "板模板 1" },
            new SlabAssemblyTemplate { TemplateId = "s2", TemplateName = "板模板 2" }
        ]);

        var workspace = assembler.Assemble();

        workspace.SummaryText.Should().Be("图层 2 个，启用构件类型 2 个，墙体模板 1 个，楼板模板 2 个");
    }

    private LayerMappingWorkspaceAssembler CreateAssembler()
        => new(
            _layerService,
            _elementTypeCatalog,
            _wallTemplateCatalog,
            _slabTemplateCatalog);
}
