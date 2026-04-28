using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using MetroToolKits.SectionGenerator.Contracts.Floors;
using MetroToolKits.SectionGenerator.Contracts.Output;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public sealed class ContractExampleRoundTripTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    static ContractExampleRoundTripTests()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    [Fact]
    public void SaveFloorConfigDocumentExample_CanDeserialize_AndRoundTrip()
    {
        var json = ReadExample("save-floor-config-document.request.example.json");

        var dto = JsonSerializer.Deserialize<SaveFloorConfigDocumentRequestDto>(json, SerializerOptions);

        dto.Should().NotBeNull();
        dto!.FloorConfig.Should().NotBeNull();
        dto.OutputConfig.Should().NotBeNull();
        dto.FloorConfig.GlobalSlopePercent.Should().Be(2.0, "2.0 means 2%, not 0.02");
        dto.FloorConfig.GlobalTopSlopePercent.Should().Be(1.5);
        dto.FloorConfig.GlobalBottomSlopePercent.Should().Be(0.0);
        dto.FloorConfig.Floors.Should().HaveCount(2);
        dto.FloorConfig.Floors[0].TopBoundarySlab.TemplateId.Should().Be("slab-top-standard");
        dto.FloorConfig.Floors[1].ScopeBounds.Should().BeNull("draft save allows null scope bounds");

        var roundTrippedJson = JsonSerializer.Serialize(dto, SerializerOptions);
        var roundTrippedDto = JsonSerializer.Deserialize<SaveFloorConfigDocumentRequestDto>(roundTrippedJson, SerializerOptions);

        roundTrippedDto.Should().NotBeNull();
        roundTrippedDto!.FloorConfig.Should().NotBeNull();
        roundTrippedDto.OutputConfig.Should().NotBeNull();
        roundTrippedDto.FloorConfig.GlobalSlopePercent.Should().Be(2.0);
        roundTrippedDto.FloorConfig.Floors.Should().HaveCount(2);
        roundTrippedDto.FloorConfig.Floors[0].AlignmentPoints.Should().HaveCount(3);
        roundTrippedDto.FloorConfig.Floors[1].TopBoundarySlab.TemplateId.Should().BeEmpty();
    }

    [Fact]
    public void SectionOutputConfigExample_CanDeserialize_AndRoundTripWithoutLosingSections()
    {
        var json = ReadExample("section-output-config.example.json");

        var dto = JsonSerializer.Deserialize<SectionOutputConfigDto>(json, SerializerOptions);

        dto.Should().NotBeNull();
        dto!.AnnotationOptions.Should().NotBeNull();
        dto.HatchOptions.Should().NotBeNull();
        dto.LayerOptions.Should().NotBeNull();
        dto.AnnotationOptions!.GenerateAnnotations.Should().BeTrue();
        dto.HatchOptions!.Enabled.Should().BeTrue();
        dto.HatchOptions.WallHatch.Should().NotBeNull();
        dto.HatchOptions.ColumnHatch.Should().NotBeNull();
        dto.HatchOptions.SlabHatch.Should().NotBeNull();
        dto.HatchOptions.WallHatch!.PatternName.Should().Be("ANSI31");
        dto.HatchOptions.WallHatch.Scale.Should().Be(100.0);
        dto.HatchOptions.ColumnHatch!.PatternName.Should().Be("SOLID");
        dto.HatchOptions.ColumnHatch.Scale.Should().Be(50.0);
        dto.LayerOptions!.CutLineLayer.Should().Be("MK_剖切线");
        dto.LayerOptions.FinishLayer.Should().Be("MK_装修输出");

        var roundTrippedJson = JsonSerializer.Serialize(dto, SerializerOptions);
        var roundTrippedDto = JsonSerializer.Deserialize<SectionOutputConfigDto>(roundTrippedJson, SerializerOptions);

        roundTrippedDto.Should().NotBeNull();
        roundTrippedDto!.AnnotationOptions.Should().NotBeNull();
        roundTrippedDto.HatchOptions.Should().NotBeNull();
        roundTrippedDto.LayerOptions.Should().NotBeNull();
        roundTrippedDto.AnnotationOptions!.GenerateAnnotations.Should().BeTrue();
        roundTrippedDto.HatchOptions!.Enabled.Should().BeTrue();
        roundTrippedDto.HatchOptions.WallHatch!.PatternName.Should().Be("ANSI31");
        roundTrippedDto.HatchOptions.ColumnHatch!.UseByLayer.Should().BeFalse();
        roundTrippedDto.LayerOptions!.AnnotationLayer.Should().Be("MK_标注");
        roundTrippedDto.LayerOptions.StructuralLayer.Should().Be("MK_结构输出");
    }

    [Fact]
    public void SectionPreflightReportExample_PreservesCurrentExampleSemantics()
    {
        // Current mismatch note:
        // The example response is an external grouped-report shape, but the current
        // SectionPreflightReportDto is still an internal/UI-facing DTO with fields such as
        // DrawingDisplayName, SummaryText, BlockingCount and FloorStatus detail texts.
        // The example also exposes BlockingChecks/WarningChecks/InfoChecks directly, while
        // the current DTO derives those groups in SectionPreflightViewModel.
        // This test therefore validates example contract semantics from raw JSON without
        // changing business DTOs in P3.
        using var document = JsonDocument.Parse(ReadExample("section-preflight-report.response.example.json"));
        var root = document.RootElement;

        root.GetProperty("CanGenerate").GetBoolean().Should().BeFalse();
        root.GetProperty("Summary").GetString().Should().NotBeNullOrWhiteSpace();

        var checks = root.GetProperty("Checks").EnumerateArray().ToArray();
        var blockingChecks = root.GetProperty("BlockingChecks").EnumerateArray().ToArray();
        var warningChecks = root.GetProperty("WarningChecks").EnumerateArray().ToArray();
        var infoChecks = root.GetProperty("InfoChecks").EnumerateArray().ToArray();
        var floors = root.GetProperty("Floors").EnumerateArray().ToArray();

        checks.Should().NotBeEmpty();
        floors.Should().HaveCount(2);

        var severities = checks
            .Select(static element => element.GetProperty("Severity").GetString())
            .Where(static value => value is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        severities.Should().BeEquivalentTo(["Passed", "Info", "Warning", "Blocking"]);

        var actionTargets = checks
            .Select(static element => element.GetProperty("ActionTarget").GetString())
            .Where(static value => value is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        actionTargets.Should().BeEquivalentTo(["None", "FloorConfig", "LayerMapping"]);

        var suggestedCommandTags = checks
            .Select(static element => element.GetProperty("SuggestedCommandTag").GetString())
            .Where(static value => value is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        suggestedCommandTags.Should().BeEquivalentTo(["FloorConfig", "LayerMapping", string.Empty]);

        checks.Should().OnlyContain(element => HasRequiredPreflightCheckFields(element));
        blockingChecks.Should().OnlyContain(element => HasRequiredPreflightCheckFields(element));
        warningChecks.Should().OnlyContain(element => HasRequiredPreflightCheckFields(element));
        infoChecks.Should().OnlyContain(element => HasRequiredPreflightCheckFields(element));

        var passedChecks = checks
            .Where(static element => string.Equals(element.GetProperty("Severity").GetString(), "Passed", StringComparison.Ordinal))
            .ToArray();
        passedChecks.Should().ContainSingle();
        blockingChecks.Should().NotContain(static element => string.Equals(element.GetProperty("Severity").GetString(), "Passed", StringComparison.Ordinal));
        warningChecks.Should().NotContain(static element => string.Equals(element.GetProperty("Severity").GetString(), "Passed", StringComparison.Ordinal));
        infoChecks.Should().NotContain(static element => string.Equals(element.GetProperty("Severity").GetString(), "Passed", StringComparison.Ordinal));

        floors.Should().OnlyContain(static floor =>
            HasRequiredFloorStatusFields(floor));
    }

    private static string ReadExample(string fileName)
    {
        var path = Path.Combine(GetExamplesDirectory(), fileName);
        File.Exists(path).Should().BeTrue($"example file should exist: {path}");
        return File.ReadAllText(path);
    }

    private static string GetExamplesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "contracts", "examples");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate docs/contracts/examples from test base directory.");
    }

    private static bool HasRequiredPreflightCheckFields(JsonElement element)
    {
        return element.TryGetProperty("Severity", out _) &&
               element.TryGetProperty("Summary", out _) &&
               element.TryGetProperty("SuggestedActionText", out _) &&
               element.TryGetProperty("RelatedObjectName", out _) &&
               element.TryGetProperty("SuggestedCommandTag", out _) &&
               element.TryGetProperty("ActionTarget", out _) &&
               element.TryGetProperty("FloorName", out _);
    }

    private static bool HasRequiredFloorStatusFields(JsonElement element)
    {
        return element.TryGetProperty("FloorName", out _) &&
               element.TryGetProperty("IsBaseFloor", out _) &&
               element.TryGetProperty("CanGenerate", out _) &&
               element.TryGetProperty("WillBeSkipped", out _) &&
               element.TryGetProperty("Summary", out _);
    }
}
