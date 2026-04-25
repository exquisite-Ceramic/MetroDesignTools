using FluentAssertions;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Contracts.Templates;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class SlabTemplateCatalogMapperTests
{
    private readonly SlabTemplateCatalogMapper _mapper = new();

    [Fact]
    public void ToDto_MapsDomainTemplateCatalog()
    {
        var catalog = new[]
        {
            new SlabAssemblyTemplate
            {
                TemplateId = "slab-1",
                TemplateName = "楼板模板 1",
                WallJunctionMode = SlabWallJunctionMode.ReturnUpAtWallFace,
                CoreRule = new SlabCoreRule
                {
                    Name = "结构板",
                    Thickness = 180,
                    MaterialOrCategory = "结构",
                    VisibleInSection = false
                },
                TopLayers =
                [
                    new SlabLayerRule
                    {
                        Name = "找平层",
                        Side = SlabLayerSide.Top,
                        Order = 1,
                        Thickness = 30,
                        MaterialOrCategory = "砂浆",
                        VisibleInSection = true
                    }
                ],
                BottomLayers =
                [
                    new SlabLayerRule
                    {
                        Name = "吊顶",
                        Side = SlabLayerSide.Bottom,
                        Order = 2,
                        Thickness = 12,
                        MaterialOrCategory = "石膏板",
                        VisibleInSection = false
                    }
                ]
            }
        };

        var dto = _mapper.ToDto(catalog);

        dto.Templates.Should().ContainSingle();
        dto.Templates[0].TemplateId.Should().Be("slab-1");
        dto.Templates[0].TemplateName.Should().Be("楼板模板 1");
        dto.Templates[0].WallJunctionMode.Should().Be(nameof(SlabWallJunctionMode.ReturnUpAtWallFace));
        dto.Templates[0].CoreRule.VisibleInSection.Should().BeFalse();
        dto.Templates[0].TopLayers.Select(layer => layer.Order).Should().Equal(1);
        dto.Templates[0].BottomLayers.Select(layer => layer.MaterialOrCategory).Should().Equal("石膏板");
    }

    [Fact]
    public void ToDomain_MapsDtoBackToDomain()
    {
        var dto = new SlabTemplateCatalogDto
        {
            Templates =
            [
                new SlabAssemblyTemplateDto
                {
                    TemplateId = "slab-2",
                    TemplateName = "楼板模板 2",
                    WallJunctionMode = nameof(SlabWallJunctionMode.ContinueUnderWall),
                    CoreRule = new SlabCoreRuleDto
                    {
                        Name = "核心板",
                        Thickness = 150,
                        MaterialOrCategory = "结构",
                        VisibleInSection = true
                    },
                    TopLayers =
                    [
                        new SlabLayerRuleDto
                        {
                            Name = "面层",
                            Side = nameof(SlabLayerSide.Top),
                            Order = 1,
                            Thickness = 20,
                            MaterialOrCategory = "饰面",
                            VisibleInSection = true
                        }
                    ],
                    BottomLayers =
                    [
                        new SlabLayerRuleDto
                        {
                            Name = "底层",
                            Side = nameof(SlabLayerSide.Bottom),
                            Order = 2,
                            Thickness = 8,
                            MaterialOrCategory = "涂料",
                            VisibleInSection = false
                        }
                    ]
                }
            ]
        };

        var domain = _mapper.ToDomain(dto);

        domain.Should().ContainSingle();
        domain[0].TemplateId.Should().Be("slab-2");
        domain[0].TemplateName.Should().Be("楼板模板 2");
        domain[0].WallJunctionMode.Should().Be(SlabWallJunctionMode.ContinueUnderWall);
        domain[0].TopLayers.Select(layer => layer.Side).Should().Equal(SlabLayerSide.Top);
        domain[0].BottomLayers.Select(layer => layer.Order).Should().Equal(2);
        domain[0].BottomLayers.Select(layer => layer.VisibleInSection).Should().Equal(false);
    }

    [Fact]
    public void ToDomain_InvalidEnumValues_FallBackToDefaults()
    {
        var domain = _mapper.ToDomain(new SlabTemplateCatalogDto
        {
            Templates =
            [
                new SlabAssemblyTemplateDto
                {
                    TemplateId = "slab-invalid",
                    TemplateName = "非法枚举模板",
                    WallJunctionMode = "invalid-junction",
                    TopLayers =
                    [
                        new SlabLayerRuleDto
                        {
                            Name = "顶层",
                            Side = "invalid-side",
                            Thickness = 10
                        }
                    ]
                }
            ]
        });

        domain.Should().ContainSingle();
        domain[0].WallJunctionMode.Should().Be(new SlabAssemblyTemplate().WallJunctionMode);
        domain[0].TopLayers[0].Side.Should().Be(new SlabLayerRule().Side);
    }
}
