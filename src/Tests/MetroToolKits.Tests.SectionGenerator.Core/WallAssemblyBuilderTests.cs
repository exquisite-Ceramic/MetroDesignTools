using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class WallAssemblyBuilderTests
{
    [Fact]
    public void Build_CoreWithLayersOnBothSides_ReturnsCompositeWallWithThreeSections()
    {
        var builder = new WallAssemblyBuilder();
        var wall = builder.Build(
            CreateCoreSegment(thickness: 200),
            new WallAssemblyTemplate
            {
                TemplateId = "wall-a",
                TemplateName = "双侧附加层墙",
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
                        Name = "左抹灰",
                        Side = WallLayerSide.Left,
                        Order = 1,
                        Thickness = 20,
                        MaterialOrCategory = "抹灰"
                    }
                },
                RightLayers =
                {
                    new WallLayerRule
                    {
                        Name = "右饰面",
                        Side = WallLayerSide.Right,
                        Order = 1,
                        Thickness = 30,
                        MaterialOrCategory = "饰面"
                    }
                }
            });

        wall.Should().BeOfType<TemplatedCompositeWallElement>();
        wall.TemplateId.Should().Be("wall-a");
        wall.LayerSections.Should().HaveCount(3);
        wall.LayerSections.Select(layer => layer.Name).Should().ContainInOrder("左抹灰", "结构芯", "右饰面");
        wall.LayerSections[0].InnerOffset.Should().BeApproximately(-100, 1e-6);
        wall.LayerSections[0].OuterOffset.Should().BeApproximately(-120, 1e-6);
        wall.LayerSections[1].InnerOffset.Should().BeApproximately(-100, 1e-6);
        wall.LayerSections[1].OuterOffset.Should().BeApproximately(100, 1e-6);
        wall.LayerSections[2].InnerOffset.Should().BeApproximately(100, 1e-6);
        wall.LayerSections[2].OuterOffset.Should().BeApproximately(130, 1e-6);
    }

    [Fact]
    public void Build_AsymmetricLayers_AccumulatesOffsetsPerSide()
    {
        var builder = new WallAssemblyBuilder();
        var wall = builder.Build(
            CreateCoreSegment(thickness: 240),
            new WallAssemblyTemplate
            {
                TemplateId = "wall-b",
                TemplateName = "非对称墙",
                CoreRule = new WallCoreRule
                {
                    Name = "结构芯",
                    Thickness = 240,
                    MaterialOrCategory = "结构"
                },
                LeftLayers =
                {
                    new WallLayerRule
                    {
                        Name = "左保温",
                        Side = WallLayerSide.Left,
                        Order = 1,
                        Thickness = 50,
                        MaterialOrCategory = "保温"
                    },
                    new WallLayerRule
                    {
                        Name = "左抹灰",
                        Side = WallLayerSide.Left,
                        Order = 2,
                        Thickness = 20,
                        MaterialOrCategory = "抹灰"
                    },
                    new WallLayerRule
                    {
                        Name = "左饰面",
                        Side = WallLayerSide.Left,
                        Order = 3,
                        Thickness = 15,
                        MaterialOrCategory = "饰面"
                    }
                },
                RightLayers =
                {
                    new WallLayerRule
                    {
                        Name = "右抹灰",
                        Side = WallLayerSide.Right,
                        Order = 1,
                        Thickness = 25,
                        MaterialOrCategory = "抹灰"
                    }
                }
            });

        wall.LayerSections.Should().HaveCount(5);
        wall.LayerSections.Select(layer => layer.Name).Should()
            .ContainInOrder("左饰面", "左抹灰", "左保温", "结构芯", "右抹灰");

        wall.LayerSections.Single(layer => layer.Name == "左保温")
            .OuterOffset.Should().BeApproximately(-170, 1e-6);
        wall.LayerSections.Single(layer => layer.Name == "左抹灰")
            .OuterOffset.Should().BeApproximately(-190, 1e-6);
        wall.LayerSections.Single(layer => layer.Name == "左饰面")
            .OuterOffset.Should().BeApproximately(-205, 1e-6);
        wall.LayerSections.Single(layer => layer.Name == "右抹灰")
            .OuterOffset.Should().BeApproximately(145, 1e-6);
    }

    [Fact]
    public void Build_PreservesCoreSourceHandlesAndTemplateMetadata()
    {
        var builder = new WallAssemblyBuilder();
        var wall = builder.Build(
            CreateCoreSegment(
                thickness: 180,
                sourceHandles: new[] { "1A2", "1A3" },
                sourceLayer: "MK_结构墙_0"),
            new WallAssemblyTemplate
            {
                TemplateId = "wall-c",
                TemplateName = "复合墙",
                CoreRule = new WallCoreRule
                {
                    Name = "结构芯",
                    Thickness = 180,
                    MaterialOrCategory = "结构"
                }
            });

        wall.SourceHandle.Should().Be("1A2");
        wall.SourceHandles.Should().Equal("1A2", "1A3");
        wall.SourceLayer.Should().Be("MK_结构墙_0");
        wall.TemplateId.Should().Be("wall-c");
        wall.TemplateName.Should().Be("复合墙");
    }

    private static CoreWallSegment CreateCoreSegment(
        double thickness,
        IEnumerable<string>? sourceHandles = null,
        string? sourceLayer = null)
    {
        return new CoreWallSegment
        {
            StartPoint = new Point3D(0, 0, 0),
            EndPoint = new Point3D(5000, 0, 0),
            Thickness = thickness,
            Height = 3000,
            BaseElevation = 0,
            TemplateId = "core-template",
            SourceLayer = sourceLayer,
            SourceHandles = sourceHandles?.ToList() ?? new List<string> { "AAA" }
        };
    }
}
