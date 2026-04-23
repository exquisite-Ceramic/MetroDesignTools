using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class TemplateAssemblyBuilderTests
{
    [Fact]
    public void WallAssemblyBuilder_Build_CreatesCoreAndAsymmetricSideLayers()
    {
        var builder = new WallAssemblyBuilder();
        var template = new WallAssemblyTemplate
        {
            TemplateId = "wall-test",
            TemplateName = "测试墙",
            VerticalAnchorMode = WallVerticalAnchorMode.StructuralSlabFaces,
            CoreRule = new WallCoreRule
            {
                Name = "结构芯",
                Thickness = 200,
                MaterialOrCategory = "结构",
                VisibleInSection = true,
                RecognitionMode = WallCoreRecognitionMode.BoundaryPair
            },
            LeftLayers =
            {
                new WallLayerRule { Name = "左抹灰", Side = WallLayerSide.Left, Order = 1, Thickness = 20, MaterialOrCategory = "抹灰", VisibleInSection = true },
                new WallLayerRule { Name = "左饰面", Side = WallLayerSide.Left, Order = 2, Thickness = 10, MaterialOrCategory = "饰面", VisibleInSection = true }
            },
            RightLayers =
            {
                new WallLayerRule { Name = "右抹灰", Side = WallLayerSide.Right, Order = 1, Thickness = 30, MaterialOrCategory = "抹灰", VisibleInSection = true }
            }
        };
        var core = new CoreWallSegment
        {
            StartPoint = new Point3D(0, 0, 0),
            EndPoint = new Point3D(1000, 0, 0),
            Thickness = 200,
            Height = 3000,
            SourceLayer = "MK_结构墙_0",
            SourceHandles = new List<string> { "1A", "1B" }
        };

        var wall = builder.Build(core, template);

        wall.TemplateId.Should().Be("wall-test");
        wall.VerticalAnchorMode.Should().Be(WallVerticalAnchorMode.StructuralSlabFaces);
        wall.SourceHandles.Should().Equal("1A", "1B");
        wall.LayerSections.Should().HaveCount(4);
        wall.LayerSections.Should().ContainSingle(section =>
            section.IsCore &&
            section.Thickness == 200 &&
            section.InnerOffset == -100 &&
            section.OuterOffset == 100);
        wall.LayerSections.Min(section => Math.Min(section.InnerOffset, section.OuterOffset)).Should().Be(-130);
        wall.LayerSections.Max(section => Math.Max(section.InnerOffset, section.OuterOffset)).Should().Be(130);
    }

    [Fact]
    public void SlabAssemblyBuilder_Build_CreatesCoreAndTopBottomFinishLayers()
    {
        var builder = new SlabAssemblyBuilder();
        var template = new SlabAssemblyTemplate
        {
            TemplateId = "slab-test",
            TemplateName = "测试板",
            WallJunctionMode = SlabWallJunctionMode.StopAtWallFace,
            CoreRule = new SlabCoreRule
            {
                Name = "结构板",
                Thickness = 120,
                MaterialOrCategory = "结构",
                VisibleInSection = true
            },
            TopLayers =
            {
                new SlabLayerRule { Name = "找平", Side = SlabLayerSide.Top, Order = 1, Thickness = 20, MaterialOrCategory = "找平", VisibleInSection = true },
                new SlabLayerRule { Name = "面层", Side = SlabLayerSide.Top, Order = 2, Thickness = 10, MaterialOrCategory = "装修", VisibleInSection = true }
            },
            BottomLayers =
            {
                new SlabLayerRule { Name = "底抹灰", Side = SlabLayerSide.Bottom, Order = 1, Thickness = 15, MaterialOrCategory = "抹灰", VisibleInSection = true }
            }
        };
        var core = new CoreSlabArea
        {
            Outline = new List<Point3D>
            {
                new(0, 0, 0),
                new(1000, 0, 0),
                new(1000, 800, 0),
                new(0, 800, 0)
            },
            CoreTopElevation = 100,
            SourceLayer = "MK_楼板_0",
            SourceHandles = new List<string> { "2A" }
        };

        var slab = builder.Build(core, template);

        slab.TemplateId.Should().Be("slab-test");
        slab.WallJunctionMode.Should().Be(SlabWallJunctionMode.StopAtWallFace);
        slab.LayerSections.Should().HaveCount(4);
        slab.LayerSections.Should().ContainSingle(section =>
            section.IsCore &&
            section.TopOffset == 0 &&
            section.BottomOffset == -120);
        slab.LayerSections.Should().ContainSingle(section =>
            !section.IsCore &&
            section.Side == SlabLayerSide.Top &&
            section.TopOffset == 30 &&
            section.BottomOffset == 20);
        slab.LayerSections.Should().ContainSingle(section =>
            !section.IsCore &&
            section.Side == SlabLayerSide.Bottom &&
            section.TopOffset == -120 &&
            section.BottomOffset == -135);
    }

    [Fact]
    public void FloorVerticalProfileBuilder_Build_UsesTemplateBoundaryInsteadOfLegacyThickness()
    {
        var template = new SlabAssemblyTemplate
        {
            TemplateId = "top-boundary-template",
            TemplateName = "顶部边界板模板",
            WallJunctionMode = SlabWallJunctionMode.StopAtWallFace,
            CoreRule = new SlabCoreRule
            {
                Name = "结构顶板",
                Thickness = 200,
                MaterialOrCategory = "结构",
                VisibleInSection = true
            },
            TopLayers =
            {
                new SlabLayerRule { Name = "面层", Side = SlabLayerSide.Top, Order = 1, Thickness = 40, MaterialOrCategory = "装修", VisibleInSection = true }
            }
        };
        var builder = new FloorVerticalProfileBuilder(
            new InMemorySlabAssemblyTemplateCatalog(new[] { template }),
            new SlabAssemblyBuilder());
        var floor = new FloorConfig
        {
            Name = "F1",
            Height = 5200,
            BottomSlabThickness = 800,
            TopSlabThickness = 600,
            FinishThickness = 120,
            TopBoundarySlab = new BoundarySlabConfig
            {
                TemplateId = "top-boundary-template"
            },
            BottomBoundarySlab = new BoundarySlabConfig()
        };

        var profile = builder.Build(floor, sectionLength: 1000, baseElevation: 100);

        profile.GetTopBoundaryBottom(0).Should().Be(5300);
        profile.GetTopStructuralBottom(0).Should().Be(5300);
        profile.GetTopStructuralTop(0).Should().Be(5500);
        profile.GetTopBoundaryTop(0).Should().Be(5540);
    }
}
