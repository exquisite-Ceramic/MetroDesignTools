using System.Text.Json;
using FluentAssertions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.SectionGenerator.Infrastructure.Repositories;

namespace MetroToolKits.Tests.Foundation;

public class DwgFloorConfigStorageEnvelopeTests
{
    [Fact]
    public void Serialize_WritesSchemaVersionToolVersionAndSavedAt()
    {
        var json = DwgFloorConfigStorageEnvelope.Serialize(CreateDocument());

        using var jsonDocument = JsonDocument.Parse(json);
        var root = jsonDocument.RootElement;

        root.GetProperty("SchemaVersion").GetInt32().Should().Be(DwgFloorConfigStorageEnvelope.CurrentSchemaVersion);
        root.GetProperty("ToolVersion").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("SavedAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void TryDeserialize_WhenLegacyPayloadHasNoSchemaVersion_PreservesConfigAndOutput()
    {
        const string json = """
        {
          "Config": {
            "AlignmentBaseFloorName": "F1",
            "Floors": [
              {
                "Name": "F1",
                "Height": 5200
              }
            ]
          },
          "OutputConfig": {
            "LayerOptions": {
              "CutLineLayer": "CUT",
              "FinishLayer": "FIN"
            }
          }
        }
        """;

        var success = DwgFloorConfigStorageEnvelope.TryDeserialize(json, out var document, out var diagnostics);

        success.Should().BeTrue();
        diagnostics.Should().BeEmpty();
        document.Should().NotBeNull();
        document!.Config.AlignmentBaseFloorName.Should().Be("F1");
        document.Config.Floors.Should().ContainSingle();
        document.OutputConfig.LayerOptions.CutLineLayer.Should().Be("CUT");
        document.OutputConfig.LayerOptions.FinishLayer.Should().Be("FIN");
    }

    [Fact]
    public void TryDeserialize_WhenSchemaVersionIsFuture_ReturnsWarningAndPreservesKnownFields()
    {
        const string json = """
        {
          "SchemaVersion": 99,
          "ToolVersion": "9.9.9",
          "SavedAt": "2026-04-26T10:30:00+00:00",
          "Config": {
            "AlignmentBaseFloorName": "F2",
            "Floors": [
              {
                "Name": "F2",
                "Height": 5300
              }
            ]
          },
          "OutputConfig": {
            "LayerOptions": {
              "CutLineLayer": "CUT-FUTURE"
            }
          }
        }
        """;

        var success = DwgFloorConfigStorageEnvelope.TryDeserialize(json, out var document, out var diagnostics);

        success.Should().BeTrue();
        document.Should().NotBeNull();
        document!.Config.AlignmentBaseFloorName.Should().Be("F2");
        document.OutputConfig.LayerOptions.CutLineLayer.Should().Be("CUT-FUTURE");
        diagnostics.Should().ContainSingle(d => d.Code == "SectionGenerator.DwgConfig.FutureSchemaVersion");
    }

    private static LoadedSectionConfig CreateDocument()
        => new()
        {
            Config = new SectionConfig
            {
                AlignmentBaseFloorName = "F1",
                Floors =
                [
                    new FloorConfig
                    {
                        Name = "F1",
                        Height = 5200
                    }
                ]
            },
            OutputConfig = new SectionOutputConfig
            {
                LayerOptions = new LayerOptions
                {
                    CutLineLayer = "CUT",
                    FinishLayer = "FIN"
                }
            }
        };
}
