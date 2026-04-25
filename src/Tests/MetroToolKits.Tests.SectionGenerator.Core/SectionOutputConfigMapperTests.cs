using FluentAssertions;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Contracts.Output;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class SectionOutputConfigMapperTests
{
    private readonly ISectionOutputConfigMapper _mapper = new SectionOutputConfigMapper();

    [Fact]
    public void ToDto_WhenOutputConfigIsNull_ReturnsCompleteDefaultDto()
    {
        var dto = _mapper.ToDto(null);

        dto.AnnotationOptions.Should().NotBeNull();
        dto.AnnotationOptions!.GenerateAnnotations.Should().BeFalse();
        dto.HatchOptions.Should().NotBeNull();
        dto.HatchOptions!.Enabled.Should().BeFalse();
        dto.HatchOptions.WallHatch.Should().NotBeNull();
        dto.HatchOptions.WallHatch!.PatternName.Should().Be("ANSI31");
        dto.HatchOptions.WallHatch.Scale.Should().Be(100.0);
        dto.HatchOptions.WallHatch.UseByLayer.Should().BeTrue();
        dto.LayerOptions.Should().NotBeNull();
        dto.LayerOptions!.CutLineLayer.Should().Be("MK_剖切线");
        dto.LayerOptions.FinishLayer.Should().Be("MK_装修输出");
    }

    [Fact]
    public void ToDto_MapsAnnotationHatchAndLayerOptions()
    {
        var outputConfig = new SectionOutputConfig
        {
            AnnotationOptions = new AnnotationOptions
            {
                GenerateAnnotations = true
            },
            HatchOptions = new HatchOptions
            {
                Enabled = true,
                WallHatch = new HatchStyleOptions
                {
                    PatternName = "SOLID",
                    Scale = 12.5,
                    Angle = 35,
                    UseByLayer = false
                },
                ColumnHatch = new HatchStyleOptions
                {
                    PatternName = "ANSI32",
                    Scale = 50,
                    Angle = 15,
                    UseByLayer = true
                },
                SlabHatch = new HatchStyleOptions
                {
                    PatternName = "AR-CONC",
                    Scale = 80,
                    Angle = 5,
                    UseByLayer = false
                }
            },
            LayerOptions = new LayerOptions
            {
                CutLineLayer = "CUT",
                SightLineLayer = "SIGHT",
                AnnotationLayer = "ANNO",
                WallHatchLayer = "WALL-H",
                ColumnHatchLayer = "COL-H",
                SlabHatchLayer = "SLAB-H",
                StructuralLayer = "STRU",
                FinishLayer = "FIN"
            }
        };

        var dto = _mapper.ToDto(outputConfig);

        dto.AnnotationOptions!.GenerateAnnotations.Should().BeTrue();
        dto.HatchOptions!.Enabled.Should().BeTrue();
        dto.HatchOptions.WallHatch!.PatternName.Should().Be("SOLID");
        dto.HatchOptions.WallHatch.Scale.Should().Be(12.5);
        dto.HatchOptions.WallHatch.Angle.Should().Be(35);
        dto.HatchOptions.WallHatch.UseByLayer.Should().BeFalse();
        dto.HatchOptions.ColumnHatch!.PatternName.Should().Be("ANSI32");
        dto.HatchOptions.SlabHatch!.PatternName.Should().Be("AR-CONC");
        dto.LayerOptions!.CutLineLayer.Should().Be("CUT");
        dto.LayerOptions.FinishLayer.Should().Be("FIN");
    }

    [Fact]
    public void ToDomain_WhenDtoChildrenAreNull_UsesDefaults()
    {
        var dto = new SectionOutputConfigDto
        {
            AnnotationOptions = null,
            HatchOptions = new HatchOptionsDto
            {
                Enabled = true,
                WallHatch = null,
                ColumnHatch = null,
                SlabHatch = null
            },
            LayerOptions = null
        };

        var outputConfig = _mapper.ToDomain(dto);

        outputConfig.AnnotationOptions.Should().NotBeNull();
        outputConfig.AnnotationOptions.GenerateAnnotations.Should().BeFalse();
        outputConfig.HatchOptions.Should().NotBeNull();
        outputConfig.HatchOptions.Enabled.Should().BeTrue();
        outputConfig.HatchOptions.WallHatch.PatternName.Should().Be("ANSI31");
        outputConfig.HatchOptions.ColumnHatch.Scale.Should().Be(100.0);
        outputConfig.HatchOptions.SlabHatch.UseByLayer.Should().BeTrue();
        outputConfig.LayerOptions.Should().NotBeNull();
        outputConfig.LayerOptions.CutLineLayer.Should().Be("MK_剖切线");
        outputConfig.LayerOptions.FinishLayer.Should().Be("MK_装修输出");
    }

    [Fact]
    public void ApplyToDocument_OnlyUpdatesOutputConfig()
    {
        var config = new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors = [new FloorConfig { Name = "F1", Height = 5200 }]
        };
        var runtimeState = new SectionConfigRuntimeState
        {
            Source = SectionConfigStorageSource.EmbeddedDwg,
            DrawingDisplayName = "Test.dwg"
        };
        var diagnostics = new List<OperationDiagnostic>
        {
            new()
            {
                Level = DiagnosticLevel.Warning,
                Code = "W1",
                Stage = PipelineStage.FloorConfigLoad,
                Module = "Test",
                Message = "warn"
            }
        };
        var document = new LoadedSectionConfig
        {
            Config = config,
            OutputConfig = new SectionOutputConfig(),
            RuntimeState = runtimeState,
            RuntimeDiagnostics = diagnostics
        };
        var dto = new SectionOutputConfigDto
        {
            AnnotationOptions = new AnnotationOptionsDto
            {
                GenerateAnnotations = true
            },
            HatchOptions = new HatchOptionsDto
            {
                Enabled = true,
                WallHatch = new HatchStyleDto
                {
                    PatternName = "SOLID",
                    Scale = 25,
                    Angle = 10,
                    UseByLayer = false
                },
                ColumnHatch = new HatchStyleDto
                {
                    PatternName = "ANSI32",
                    Scale = 30,
                    Angle = 20,
                    UseByLayer = true
                },
                SlabHatch = new HatchStyleDto
                {
                    PatternName = "AR-CONC",
                    Scale = 35,
                    Angle = 30,
                    UseByLayer = false
                }
            },
            LayerOptions = new LayerOptionsDto
            {
                CutLineLayer = "CUT",
                SightLineLayer = "SIGHT",
                AnnotationLayer = "ANNO",
                WallHatchLayer = "WALL-H",
                ColumnHatchLayer = "COL-H",
                SlabHatchLayer = "SLAB-H",
                StructuralLayer = "STRU",
                FinishLayer = "FIN"
            }
        };

        _mapper.ApplyToDocument(dto, document);

        document.OutputConfig.AnnotationOptions.GenerateAnnotations.Should().BeTrue();
        document.OutputConfig.HatchOptions.Enabled.Should().BeTrue();
        document.OutputConfig.HatchOptions.WallHatch.PatternName.Should().Be("SOLID");
        document.OutputConfig.HatchOptions.ColumnHatch.PatternName.Should().Be("ANSI32");
        document.OutputConfig.HatchOptions.SlabHatch.PatternName.Should().Be("AR-CONC");
        document.OutputConfig.LayerOptions.CutLineLayer.Should().Be("CUT");
        document.OutputConfig.LayerOptions.FinishLayer.Should().Be("FIN");
        document.Config.Should().BeSameAs(config);
        document.RuntimeState.Should().BeSameAs(runtimeState);
        document.RuntimeDiagnostics.Should().BeSameAs(diagnostics);
    }
}
