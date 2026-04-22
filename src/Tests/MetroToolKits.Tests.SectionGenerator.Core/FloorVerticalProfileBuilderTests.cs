using FluentAssertions;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FloorVerticalProfileBuilderTests
{
    [Fact]
    public void Build_BottomBoundaryWithTopFinish_SeparatesStructuralTopFromFinishTop()
    {
        var bottomTemplate = new SlabAssemblyTemplate
        {
            TemplateId = "bottom-finish",
            TemplateName = "Bottom With Finish",
            CoreRule = new SlabCoreRule
            {
                Thickness = 800
            },
            TopLayers = new List<SlabLayerRule>
            {
                new()
                {
                    Name = "找平层",
                    Side = SlabLayerSide.Top,
                    Order = 1,
                    Thickness = 20,
                    MaterialOrCategory = "找平"
                },
                new()
                {
                    Name = "装修面层",
                    Side = SlabLayerSide.Top,
                    Order = 2,
                    Thickness = 10,
                    MaterialOrCategory = "装修"
                }
            }
        };

        var builder = new FloorVerticalProfileBuilder(
            new InMemorySlabAssemblyTemplateCatalog(new[] { bottomTemplate }),
            new SlabAssemblyBuilder());

        var floor = new FloorConfig
        {
            Name = "F1",
            Height = 5200,
            BottomBoundarySlab = new BoundarySlabConfig
            {
                TemplateId = bottomTemplate.TemplateId
            },
            TopBoundarySlab = new BoundarySlabConfig()
        };

        var profile = builder.Build(floor, sectionLength: 10000, baseElevation: 0);

        profile.GetBottomBoundaryTop(0).Should().Be(0);
        profile.GetBottomStructuralTop(0).Should().Be(-30);
        profile.GetBottomStructuralBottom(0).Should().Be(-830);
    }

    [Fact]
    public void Build_TopBoundaryWithBottomFinish_SeparatesStructuralBottomFromFinishBottom()
    {
        var topTemplate = new SlabAssemblyTemplate
        {
            TemplateId = "top-bottom-finish",
            TemplateName = "Top With Soffit",
            CoreRule = new SlabCoreRule
            {
                Thickness = 600
            },
            BottomLayers = new List<SlabLayerRule>
            {
                new()
                {
                    Name = "吊顶基层",
                    Side = SlabLayerSide.Bottom,
                    Order = 1,
                    Thickness = 40,
                    MaterialOrCategory = "基层"
                }
            }
        };

        var builder = new FloorVerticalProfileBuilder(
            new InMemorySlabAssemblyTemplateCatalog(new[] { topTemplate }),
            new SlabAssemblyBuilder());

        var floor = new FloorConfig
        {
            Name = "F1",
            Height = 5200,
            BottomBoundarySlab = new BoundarySlabConfig(),
            TopBoundarySlab = new BoundarySlabConfig
            {
                TemplateId = topTemplate.TemplateId
            }
        };

        var profile = builder.Build(floor, sectionLength: 10000, baseElevation: 0);

        profile.GetTopBoundaryBottom(0).Should().Be(5200);
        profile.GetTopStructuralBottom(0).Should().Be(5240);
        profile.GetTopStructuralTop(0).Should().Be(5840);
    }
}
