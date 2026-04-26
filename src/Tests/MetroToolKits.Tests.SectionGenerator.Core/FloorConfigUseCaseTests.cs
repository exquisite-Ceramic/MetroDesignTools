using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Contracts.Floors;
using MetroToolKits.SectionGenerator.Contracts.Output;
using MetroToolKits.SectionGenerator.Core.Sections;
using NSubstitute;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FloorConfigUseCaseTests
{
    [Fact]
    public void Save_WhenDraftConfigMissingAlignmentAndScope_Succeeds()
    {
        var repository = Substitute.For<IFloorConfigRepository>();
        repository.Load().Returns(new LoadedSectionConfig
        {
            Config = new SectionConfig(),
            OutputConfig = new SectionOutputConfig()
        });

        var useCase = CreateUseCase(repository);
        var request = CreateValidRequest(floors:
        [
            new FloorConfigEditDto
            {
                Name = "F1",
                Height = 5200
            },
            new FloorConfigEditDto
            {
                Name = "F2",
                Height = 5300
            }
        ]);

        var result = useCase.Save(request);

        result.Success.Should().BeTrue();
        repository.Received(1).Save(Arg.Any<LoadedSectionConfig>());
    }

    [Fact]
    public void Save_WhenFloorConfigIsInvalid_ReturnsDiagnosticsAndDoesNotPersist()
    {
        var repository = Substitute.For<IFloorConfigRepository>();
        repository.Load().Returns(new LoadedSectionConfig
        {
            Config = new SectionConfig(),
            OutputConfig = new SectionOutputConfig()
        });

        var useCase = CreateUseCase(repository);
        var request = CreateValidRequest(floors:
        [
            new FloorConfigEditDto
            {
                Name = " ",
                Height = 0,
                LegacySlopeEnabled = true,
                LegacySlopePercent = 101,
                ScopeBounds = new ScopeBoundsDto
                {
                    MinX = 20,
                    MinY = 10,
                    MaxX = 10,
                    MaxY = 5
                },
                TopBoundarySlab = new BoundarySlabEditDto
                {
                    TemplateId = "missing-template",
                    SlopeEnabled = true,
                    SlopePercent = -150
                },
                BottomBoundarySlab = new BoundarySlabEditDto
                {
                    SlopeEnabled = true,
                    SlopePercent = 200
                }
            },
            new FloorConfigEditDto
            {
                Name = "F1",
                Height = 3200
            },
            new FloorConfigEditDto
            {
                Name = "f1",
                Height = 3300
            }
        ]);

        var result = useCase.Save(request);

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Message.Contains("楼层名称不能为空", StringComparison.Ordinal));
        result.Diagnostics.Should().Contain(d => d.Message.Contains("楼层名称重复", StringComparison.Ordinal));
        result.Diagnostics.Should().Contain(d => d.Message.Contains("层高必须大于 0", StringComparison.Ordinal));
        result.Diagnostics.Should().Contain(d => d.Message.Contains("兼容坡度", StringComparison.Ordinal));
        result.Diagnostics.Should().Contain(d => d.Message.Contains("顶边界板坡度", StringComparison.Ordinal));
        result.Diagnostics.Should().Contain(d => d.Message.Contains("底边界板坡度", StringComparison.Ordinal));
        result.Diagnostics.Should().Contain(d => d.Message.Contains("范围框无效", StringComparison.Ordinal));
        result.Diagnostics.Should().Contain(d => d.Message.Contains("顶边界板模板不存在", StringComparison.Ordinal));
        repository.DidNotReceive().Save(Arg.Any<LoadedSectionConfig>());
    }

    [Fact]
    public void Save_WhenOutputConfigIsInvalid_ReturnsDiagnosticsAndDoesNotPersist()
    {
        var repository = Substitute.For<IFloorConfigRepository>();
        repository.Load().Returns(new LoadedSectionConfig
        {
            Config = new SectionConfig(),
            OutputConfig = new SectionOutputConfig()
        });

        var useCase = CreateUseCase(repository);
        var request = CreateValidRequest(outputConfig: new SectionOutputConfigDto
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
                    PatternName = " ",
                    Scale = 0
                },
                ColumnHatch = new HatchStyleDto
                {
                    PatternName = "ANSI31",
                    Scale = 10
                },
                SlabHatch = new HatchStyleDto
                {
                    PatternName = "ANSI31",
                    Scale = 10
                }
            },
            LayerOptions = new LayerOptionsDto
            {
                CutLineLayer = "",
                SightLineLayer = "SIGHT",
                AnnotationLayer = "ANNO",
                WallHatchLayer = "WALL",
                ColumnHatchLayer = "COL",
                SlabHatchLayer = "SLAB",
                StructuralLayer = "STRU",
                FinishLayer = "FIN"
            }
        });

        var result = useCase.Save(request);

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Message.Contains("剖切线图层不能为空", StringComparison.Ordinal));
        result.Diagnostics.Should().Contain(d => d.Message.Contains("图案名称不能为空", StringComparison.Ordinal));
        result.Diagnostics.Should().Contain(d => d.Message.Contains("填充比例必须大于 0", StringComparison.Ordinal));
        repository.DidNotReceive().Save(Arg.Any<LoadedSectionConfig>());
    }

    [Fact]
    public void Save_WhenRequestIsValid_AppliesFloorAndOutputConfigBeforePersisting()
    {
        var repository = Substitute.For<IFloorConfigRepository>();
        repository.Load().Returns(new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                AlignmentBaseFloorName = "OLD",
                Floors =
                [
                    new FloorConfig
                    {
                        Name = "OLD",
                        Height = 1000
                    }
                ]
            },
            OutputConfig = new SectionOutputConfig()
        });

        var useCase = CreateUseCase(repository);
        var request = CreateValidRequest();

        var result = useCase.Save(request);

        result.Success.Should().BeTrue();
        repository.Received(1).Save(Arg.Is<LoadedSectionConfig>(document =>
            document.Config.AlignmentBaseFloorName == "F1" &&
            document.Config.Floors.Count == 1 &&
            document.Config.Floors[0].Name == "F1" &&
            document.Config.Floors[0].Height == 5200 &&
            document.Config.Floors[0].TopBoundarySlab.TemplateId == "slab-120-finish" &&
            document.OutputConfig.LayerOptions.CutLineLayer == "CUT" &&
            document.OutputConfig.LayerOptions.FinishLayer == "FIN" &&
            document.OutputConfig.HatchOptions.WallHatch.Scale == 10));
    }

    [Fact]
    public void Save_WhenRepositoryThrows_ReturnsFailureResult()
    {
        var repository = Substitute.For<IFloorConfigRepository>();
        repository.Load().Returns(new LoadedSectionConfig
        {
            Config = new SectionConfig(),
            OutputConfig = new SectionOutputConfig()
        });
        repository
            .When(r => r.Save(Arg.Any<LoadedSectionConfig>()))
            .Do(_ => throw new InvalidOperationException("boom"));

        var useCase = CreateUseCase(repository);
        FloorConfigSaveResult? result = null;

        var action = () => result = useCase.Save(CreateValidRequest());

        action.Should().NotThrow();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("boom");
    }

    private static FloorConfigUseCase CreateUseCase(IFloorConfigRepository repository)
        => new(
            repository,
            new FloorConfigSaveRequestMapper(),
            new SectionOutputConfigMapper(),
            new InMemorySlabAssemblyTemplateCatalog(new[]
            {
                new SlabAssemblyTemplate
                {
                    TemplateId = "slab-120-finish",
                    TemplateName = "120板",
                    CoreRule = new SlabCoreRule
                    {
                        Thickness = 120
                    }
                }
            }),
            NullLogger<FloorConfigUseCase>.Instance);

    private static SaveFloorConfigDocumentRequestDto CreateValidRequest(
        IReadOnlyList<FloorConfigEditDto>? floors = null,
        SectionOutputConfigDto? outputConfig = null)
        => new()
        {
            FloorConfig = new SaveFloorConfigRequestDto
            {
                AlignmentBaseFloorName = "F1",
                GlobalSlopeEnabled = true,
                GlobalSlopePercent = 2,
                GlobalSlopeTarget = "StructuralSlab",
                GlobalTopSlopeEnabled = true,
                GlobalTopSlopePercent = 2,
                GlobalTopSlopeTarget = "StructuralSlab",
                GlobalBottomSlopeEnabled = false,
                GlobalBottomSlopePercent = 0,
                GlobalBottomSlopeTarget = "StructuralSlab",
                Floors = floors ??
                [
                    new FloorConfigEditDto
                    {
                        Name = "F1",
                        Height = 5200,
                        TopBoundarySlab = new BoundarySlabEditDto
                        {
                            TemplateId = "slab-120-finish",
                            SlopeEnabled = true,
                            SlopePercent = 1
                        },
                        BottomBoundarySlab = new BoundarySlabEditDto
                        {
                            TemplateId = "slab-120-finish",
                            SlopeEnabled = false,
                            SlopePercent = 0
                        }
                    }
                ]
            },
            OutputConfig = outputConfig ?? new SectionOutputConfigDto
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
                        PatternName = "ANSI31",
                        Scale = 10,
                        Angle = 0
                    },
                    ColumnHatch = new HatchStyleDto
                    {
                        PatternName = "ANSI31",
                        Scale = 20,
                        Angle = 0
                    },
                    SlabHatch = new HatchStyleDto
                    {
                        PatternName = "ANSI31",
                        Scale = 30,
                        Angle = 0
                    }
                },
                LayerOptions = new LayerOptionsDto
                {
                    CutLineLayer = "CUT",
                    SightLineLayer = "SIGHT",
                    AnnotationLayer = "ANNO",
                    WallHatchLayer = "WALL",
                    ColumnHatchLayer = "COL",
                    SlabHatchLayer = "SLAB",
                    StructuralLayer = "STRU",
                    FinishLayer = "FIN"
                }
            }
        };
}
