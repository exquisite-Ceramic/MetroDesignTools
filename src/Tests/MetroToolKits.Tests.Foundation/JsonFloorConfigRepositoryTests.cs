using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.Infrastructure.Repositories;

namespace MetroToolKits.Tests.Foundation;

public class JsonFloorConfigRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;

    public JsonFloorConfigRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MetroToolKitsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Load_LegacyConfig_MigratesToNewFormat()
    {
        var filePath = Path.Combine(_tempDirectory, "section-config.json");
        File.WriteAllText(filePath, """
        {
          "GlobalSlopeEnabled": true,
          "GlobalSlopeValue": 0.002,
          "GlobalSlopeTarget": "StructuralSlab",
          "Floors": [
            {
              "Name": "F1",
              "Height": 3000,
              "BottomSlabThickness": 800,
              "TopSlabThickness": 600,
              "AlignmentSourcePoints": [
                { "X": 0, "Y": 0, "Z": 0 },
                { "X": 10, "Y": 0, "Z": 0 },
                { "X": 0, "Y": 10, "Z": 0 }
              ],
              "AlignmentTargetPoints": [
                { "X": 0, "Y": 0, "Z": 0 },
                { "X": 10, "Y": 0, "Z": 0 },
                { "X": 0, "Y": 10, "Z": 0 }
              ]
            },
            {
              "Name": "F2",
              "Height": 3000,
              "BottomSlabThickness": 800,
              "TopSlabThickness": 600,
              "AlignmentSourcePoints": [
                { "X": 0, "Y": 0, "Z": 0 },
                { "X": 10, "Y": 0, "Z": 0 },
                { "X": 0, "Y": 10, "Z": 0 }
              ],
              "AlignmentTargetPoints": [
                { "X": 100, "Y": 0, "Z": 0 },
                { "X": 110, "Y": 0, "Z": 0 },
                { "X": 100, "Y": 10, "Z": 0 }
              ]
            }
          ]
        }
        """);

        var repository = CreateRepository(filePath);
        var config = repository.Load();

        config.Config.AlignmentBaseFloorName.Should().Be("F1");
        config.Config.Floors[0].AlignmentPoints.Should().HaveCount(3);
        config.Config.Floors[1].AlignmentPoints[0].X.Should().Be(100);
        config.Config.Floors[0].ScopeBounds.Should().BeNull();

        var persisted = File.ReadAllText(filePath);
        persisted.Should().Contain("AlignmentBaseFloorName");
        persisted.Should().Contain("AlignmentPoints");
        persisted.Should().NotContain("AlignmentSourcePoints");
    }

    [Fact]
    public void Load_LegacyConfigWithConflictingSource_ClearsAlignmentPointsAndAddsDiagnostic()
    {
        var filePath = Path.Combine(_tempDirectory, "section-config-conflict.json");
        File.WriteAllText(filePath, """
        {
          "Floors": [
            {
              "Name": "F1",
              "Height": 3000,
              "BottomSlabThickness": 800,
              "TopSlabThickness": 600,
              "AlignmentSourcePoints": [
                { "X": 0, "Y": 0, "Z": 0 },
                { "X": 10, "Y": 0, "Z": 0 },
                { "X": 0, "Y": 10, "Z": 0 }
              ],
              "AlignmentTargetPoints": [
                { "X": 0, "Y": 0, "Z": 0 },
                { "X": 10, "Y": 0, "Z": 0 },
                { "X": 0, "Y": 10, "Z": 0 }
              ]
            },
            {
              "Name": "F2",
              "Height": 3000,
              "BottomSlabThickness": 800,
              "TopSlabThickness": 600,
              "AlignmentSourcePoints": [
                { "X": 50, "Y": 0, "Z": 0 },
                { "X": 60, "Y": 0, "Z": 0 },
                { "X": 50, "Y": 10, "Z": 0 }
              ],
              "AlignmentTargetPoints": [
                { "X": 100, "Y": 0, "Z": 0 },
                { "X": 110, "Y": 0, "Z": 0 },
                { "X": 100, "Y": 10, "Z": 0 }
              ]
            }
          ]
        }
        """);

        var repository = CreateRepository(filePath);
        var config = repository.Load();

        config.Config.Floors.Single(f => f.Name == "F2").AlignmentPoints.Should().BeEmpty();
        config.RuntimeDiagnostics.Should().Contain(d =>
            d.Code == SectionGenerationErrorCodes.AlignmentMigrationConflict);
    }

    [Fact]
    public void Save_AndLoad_NewFormat_RoundTripsAlignmentPoints()
    {
        var filePath = Path.Combine(_tempDirectory, "section-config-new.json");
        var repository = CreateRepository(filePath);

        var original = new MetroToolKits.SectionGenerator.Core.Sections.SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors =
            {
                new MetroToolKits.SectionGenerator.Core.Sections.FloorConfig
                {
                    Name = "F1",
                    ScopeBounds = new MetroToolKits.Foundation.Core.Geometry.ScopeBounds2D
                    {
                        MinX = 10,
                        MinY = 20,
                        MaxX = 110,
                        MaxY = 220
                    },
                    AlignmentPoints =
                    {
                        new(0, 0, 0),
                        new(10, 0, 0),
                        new(0, 10, 0)
                    }
                }
            }
        };

        repository.Save(new LoadedSectionConfig
        {
            Config = original,
            OutputConfig = new SectionOutputConfig()
        });
        var loaded = repository.Load();

        loaded.Config.AlignmentBaseFloorName.Should().Be("F1");
        loaded.Config.Floors.Single().AlignmentPoints.Should().HaveCount(3);
        loaded.Config.Floors.Single().ScopeBounds.Should().Be(new MetroToolKits.Foundation.Core.Geometry.ScopeBounds2D
        {
            MinX = 10,
            MinY = 20,
            MaxX = 110,
            MaxY = 220
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private static JsonFloorConfigRepository CreateRepository(string filePath)
        => new(
            filePath,
            templatePath: null,
            NullLogger<JsonFloorConfigRepository>.Instance);
}
