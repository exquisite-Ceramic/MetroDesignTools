using FluentAssertions;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Contracts.Templates;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class WallTemplateCatalogMapperTests
{
    private readonly WallTemplateCatalogMapper _mapper = new();

    [Fact]
    public void ToDto_MapsDomainTemplateCatalog()
    {
        var catalog = new[]
        {
            new WallAssemblyTemplate
            {
                TemplateId = "wall-1",
                TemplateName = "墙模板 1",
                VerticalAnchorMode = WallVerticalAnchorMode.FinishSurfaceFaces,
                CoreRule = new WallCoreRule
                {
                    Name = "结构芯",
                    Thickness = 240,
                    MaterialOrCategory = "结构",
                    VisibleInSection = false,
                    RecognitionMode = WallCoreRecognitionMode.Centerline
                },
                LeftLayers =
                [
                    new WallLayerRule
                    {
                        Name = "左找平",
                        Side = WallLayerSide.Left,
                        Order = 2,
                        Thickness = 15,
                        MaterialOrCategory = "砂浆",
                        VisibleInSection = true
                    }
                ],
                RightLayers =
                [
                    new WallLayerRule
                    {
                        Name = "右饰面",
                        Side = WallLayerSide.Right,
                        Order = 1,
                        Thickness = 8,
                        MaterialOrCategory = "涂料",
                        VisibleInSection = false
                    }
                ]
            }
        };

        var dto = _mapper.ToDto(catalog);

        dto.Templates.Should().ContainSingle();
        dto.Templates[0].TemplateId.Should().Be("wall-1");
        dto.Templates[0].TemplateName.Should().Be("墙模板 1");
        dto.Templates[0].VerticalAnchorMode.Should().Be(nameof(WallVerticalAnchorMode.FinishSurfaceFaces));
        dto.Templates[0].CoreRule.RecognitionMode.Should().Be(nameof(WallCoreRecognitionMode.Centerline));
        dto.Templates[0].CoreRule.VisibleInSection.Should().BeFalse();
        dto.Templates[0].LeftLayers.Select(layer => layer.Order).Should().Equal(2);
        dto.Templates[0].LeftLayers.Select(layer => layer.Thickness).Should().Equal(15d);
        dto.Templates[0].RightLayers.Select(layer => layer.MaterialOrCategory).Should().Equal("涂料");
    }

    [Fact]
    public void ToDomain_MapsDtoBackToDomain()
    {
        var dto = new WallTemplateCatalogDto
        {
            Templates =
            [
                new WallAssemblyTemplateDto
                {
                    TemplateId = "wall-2",
                    TemplateName = "墙模板 2",
                    VerticalAnchorMode = nameof(WallVerticalAnchorMode.StructuralSlabFaces),
                    CoreRule = new WallCoreRuleDto
                    {
                        Name = "芯层",
                        Thickness = 200,
                        MaterialOrCategory = "结构",
                        VisibleInSection = true,
                        RecognitionMode = nameof(WallCoreRecognitionMode.BoundaryPair)
                    },
                    LeftLayers =
                    [
                        new WallLayerRuleDto
                        {
                            Name = "左层 1",
                            Side = nameof(WallLayerSide.Left),
                            Order = 1,
                            Thickness = 20,
                            MaterialOrCategory = "附加层",
                            VisibleInSection = true
                        }
                    ],
                    RightLayers =
                    [
                        new WallLayerRuleDto
                        {
                            Name = "右层 1",
                            Side = nameof(WallLayerSide.Right),
                            Order = 2,
                            Thickness = 12,
                            MaterialOrCategory = "饰面",
                            VisibleInSection = false
                        }
                    ]
                }
            ]
        };

        var domain = _mapper.ToDomain(dto);

        domain.Should().ContainSingle();
        domain[0].TemplateId.Should().Be("wall-2");
        domain[0].TemplateName.Should().Be("墙模板 2");
        domain[0].VerticalAnchorMode.Should().Be(WallVerticalAnchorMode.StructuralSlabFaces);
        domain[0].CoreRule.RecognitionMode.Should().Be(WallCoreRecognitionMode.BoundaryPair);
        domain[0].LeftLayers.Select(layer => layer.Order).Should().Equal(1);
        domain[0].RightLayers.Select(layer => layer.Side).Should().Equal(WallLayerSide.Right);
        domain[0].RightLayers.Select(layer => layer.VisibleInSection).Should().Equal(false);
    }

    [Fact]
    public void ToDomain_InvalidEnumValues_FallBackToDefaults()
    {
        var domain = _mapper.ToDomain(new WallTemplateCatalogDto
        {
            Templates =
            [
                new WallAssemblyTemplateDto
                {
                    TemplateId = "wall-invalid",
                    TemplateName = "非法枚举模板",
                    VerticalAnchorMode = "invalid-anchor",
                    CoreRule = new WallCoreRuleDto
                    {
                        RecognitionMode = "invalid-recognition"
                    },
                    LeftLayers =
                    [
                        new WallLayerRuleDto
                        {
                            Name = "左层",
                            Side = "invalid-side",
                            Thickness = 10
                        }
                    ]
                }
            ]
        });

        domain.Should().ContainSingle();
        domain[0].VerticalAnchorMode.Should().Be(new WallAssemblyTemplate().VerticalAnchorMode);
        domain[0].CoreRule.RecognitionMode.Should().Be(new WallCoreRule().RecognitionMode);
        domain[0].LeftLayers[0].Side.Should().Be(new WallLayerRule().Side);
    }
}
