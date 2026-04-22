using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.Infrastructure.Repositories;

namespace MetroToolKits.Tests.Foundation;

public class JsonWallAssemblyTemplateCatalogTests : IDisposable
{
    private readonly string _tempDirectory;

    public JsonWallAssemblyTemplateCatalogTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MetroToolKitsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void GetAllTemplates_MissingConfig_SeedsFromTemplateFile()
    {
        var configPath = Path.Combine(_tempDirectory, "WallAssemblyTemplates.json");
        var templatePath = Path.Combine(_tempDirectory, "WallAssemblyTemplates.template.json");
        File.WriteAllText(templatePath, """
        [
          {
            "TemplateId": "wall-seeded",
            "TemplateName": "种子模板",
            "CoreRule": {
              "Name": "结构芯",
              "Thickness": 240,
              "MaterialOrCategory": "结构",
              "VisibleInSection": true,
              "RecognitionMode": "BoundaryPair"
            },
            "LeftLayers": [
              {
                "Name": "左抹灰",
                "Side": "Left",
                "Order": 1,
                "Thickness": 20,
                "MaterialOrCategory": "抹灰",
                "VisibleInSection": true
              }
            ],
            "RightLayers": []
          }
        ]
        """);

        var catalog = CreateCatalog(configPath, templatePath);
        var templates = catalog.GetAllTemplates();

        templates.Should().ContainSingle(template => template.TemplateId == "wall-seeded");
        File.Exists(configPath).Should().BeTrue();
        File.ReadAllText(configPath).Should().Contain("wall-seeded");
    }

    [Fact]
    public void SaveAll_AndGetById_RoundTripsAsymmetricLayers()
    {
        var configPath = Path.Combine(_tempDirectory, "WallAssemblyTemplates.json");
        var catalog = CreateCatalog(configPath, templatePath: null);

        catalog.SaveAll(new[]
        {
            new WallAssemblyTemplate
            {
                TemplateId = "wall-roundtrip",
                TemplateName = "非对称模板",
                CoreRule = new WallCoreRule
                {
                    Name = "结构芯",
                    Thickness = 200,
                    MaterialOrCategory = "结构",
                    RecognitionMode = WallCoreRecognitionMode.Centerline
                },
                LeftLayers =
                {
                    new WallLayerRule
                    {
                        Name = "左保温",
                        Side = WallLayerSide.Left,
                        Order = 1,
                        Thickness = 45,
                        MaterialOrCategory = "保温"
                    }
                },
                RightLayers =
                {
                    new WallLayerRule
                    {
                        Name = "右抹灰",
                        Side = WallLayerSide.Right,
                        Order = 1,
                        Thickness = 20,
                        MaterialOrCategory = "抹灰"
                    },
                    new WallLayerRule
                    {
                        Name = "右饰面",
                        Side = WallLayerSide.Right,
                        Order = 2,
                        Thickness = 12,
                        MaterialOrCategory = "饰面"
                    }
                }
            }
        });

        var reloaded = CreateCatalog(configPath, templatePath: null);
        var template = reloaded.GetById("wall-roundtrip");

        template.Should().NotBeNull();
        template!.CoreRule.RecognitionMode.Should().Be(WallCoreRecognitionMode.Centerline);
        template.LeftLayers.Should().ContainSingle(layer => layer.Name == "左保温" && layer.Thickness == 45);
        template.RightLayers.Select(layer => layer.Name).Should().ContainInOrder("右抹灰", "右饰面");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static JsonWallAssemblyTemplateCatalog CreateCatalog(string configPath, string? templatePath)
        => new(
            configPath,
            templatePath,
            NullLogger<JsonWallAssemblyTemplateCatalog>.Instance);
}
