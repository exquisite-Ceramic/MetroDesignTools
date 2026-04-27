using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Contracts.Preflight;
using MetroToolKits.SectionGenerator.Core.Sections;
using NSubstitute;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class SectionPreflightReportAssemblerTests
{
    [Fact]
    public void Assemble_MissingBaseFloor_ReturnsBlocking()
    {
        var report = RunReport(new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                Floors = [Floor("F1"), Floor("F2")]
            },
            OutputConfig = ValidOutput()
        });

        report.CanGenerate.Should().BeFalse();
        report.Checks.Should().Contain(check =>
            check.Title == "基准层是否存在" &&
            check.Severity == SectionPreflightSeverityDto.Blocking);
    }

    [Fact]
    public void Assemble_BaseFloorMissingAlignment_ReturnsBlocking()
    {
        var report = RunReport(new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                AlignmentBaseFloorName = "F1",
                Floors =
                [
                    FloorWithScope("F1", null, Scope()),
                    FloorWithScope("F2", Alignment(100), Scope())
                ]
            },
            OutputConfig = ValidOutput()
        });

        report.CanGenerate.Should().BeFalse();
        report.Checks.Should().Contain(check =>
            check.Title == "基准层对齐点是否有效" &&
            check.Severity == SectionPreflightSeverityDto.Blocking);
    }

    [Fact]
    public void Assemble_BaseFloorMissingScope_ReturnsBlocking()
    {
        var report = RunReport(new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                AlignmentBaseFloorName = "F1",
                Floors =
                [
                    FloorWithScope("F1", Alignment(0), null),
                    FloorWithScope("F2", Alignment(100), Scope())
                ]
            },
            OutputConfig = ValidOutput()
        });

        report.CanGenerate.Should().BeFalse();
        report.Checks.Should().Contain(check =>
            check.Title == "基准层范围是否有效" &&
            check.Severity == SectionPreflightSeverityDto.Blocking);
    }

    [Fact]
    public void Assemble_NonBaseFloorMissingAlignment_ReturnsWarningAndSkippedFloor()
    {
        var report = RunReport(new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                AlignmentBaseFloorName = "F1",
                Floors =
                [
                    FloorWithScope("F1", Alignment(0), Scope()),
                    FloorWithScope("F2", null, Scope())
                ]
            },
            OutputConfig = ValidOutput()
        });

        report.Checks.Should().Contain(check =>
            check.Title == "非基准层是否会被跳过" &&
            check.Severity == SectionPreflightSeverityDto.Warning);
        report.Floors.Should().Contain(floor =>
            floor.FloorName == "F2" &&
            floor.WillBeSkipped &&
            floor.SkipReasonText.Contains("缺少对齐点"));
    }

    [Fact]
    public void Assemble_SingleFloorWithScope_CanGenerate()
    {
        var report = RunReport(new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                Floors =
                [
                    FloorWithScope("F1", null, Scope())
                ]
            },
            OutputConfig = ValidOutput()
        });

        report.CanGenerate.Should().BeTrue();
        report.Checks.Should().Contain(check =>
            check.Title == "基准层是否存在" &&
            check.Severity == SectionPreflightSeverityDto.Info);
    }

    [Fact]
    public void Assemble_MissingBoundaryTemplate_ReturnsBlocking()
    {
        var floor = Floor("F1");
        floor.TopBoundarySlab.TemplateId = "missing-top-template";

        var report = RunReport(new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                Floors = [floor]
            },
            OutputConfig = ValidOutput()
        });

        report.CanGenerate.Should().BeFalse();
        report.Checks.Should().Contain(check =>
            check.Title == "边界板模板是否缺失" &&
            check.Severity == SectionPreflightSeverityDto.Blocking &&
            check.Summary.Contains("模板不存在"));
    }

    [Fact]
    public void Assemble_InvalidOutputConfig_ReturnsBlocking()
    {
        var report = RunReport(new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                Floors = [Floor("F1")]
            },
            OutputConfig = new SectionOutputConfig
            {
                LayerOptions = new LayerOptions
                {
                    CutLineLayer = string.Empty,
                    SightLineLayer = "SIGHT",
                    AnnotationLayer = "ANNO",
                    WallHatchLayer = "WALL",
                    ColumnHatchLayer = "COL",
                    SlabHatchLayer = "SLAB",
                    StructuralLayer = "STRU",
                    FinishLayer = "FIN"
                },
                HatchOptions = new HatchOptions
                {
                    WallHatch = new HatchStyleOptions
                    {
                        PatternName = string.Empty,
                        Scale = 0
                    }
                }
            }
        });

        report.CanGenerate.Should().BeFalse();
        report.Checks.Should().Contain(check =>
            check.Title == "输出配置是否有明显错误" &&
            check.Severity == SectionPreflightSeverityDto.Blocking);
    }

    private static SectionPreflightReportDto RunReport(LoadedSectionConfig document)
    {
        var repository = Substitute.For<IFloorConfigRepository>();
        repository.Load().Returns(document);

        var checkUseCase = Substitute.For<ICheckSectionUpdatesUseCase>();
        checkUseCase.Execute().Returns(new CheckSectionUpdatesResult
        {
            Status = OperationStatus.Success
        });

        var readinessInspector = Substitute.For<IGenerationReadinessInspector>();
        readinessInspector.Inspect().Returns(new GenerationReadinessSummary());

        var useCase = new GenerateSectionPreflightUseCase(
            repository,
            checkUseCase,
            readinessInspector,
            NullLogger<GenerateSectionPreflightUseCase>.Instance);
        var assembler = new SectionPreflightReportAssembler(new InMemorySlabAssemblyTemplateCatalog());

        return assembler.Assemble(useCase.Execute());
    }

    private static SectionOutputConfig ValidOutput()
        => new()
        {
            LayerOptions = new LayerOptions
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
        };

    private static FloorConfig Floor(string name)
        => new()
        {
            Name = name,
            Height = 3000
        };

    private static FloorConfig FloorWithScope(string name, IReadOnlyList<Point3D>? alignment, ScopeBounds2D? scope)
        => new()
        {
            Name = name,
            Height = 3000,
            AlignmentPoints = alignment?.ToList() ?? [],
            ScopeBounds = scope
        };

    private static IReadOnlyList<Point3D> Alignment(double originX)
        =>
        [
            new Point3D(originX, 0, 0),
            new Point3D(originX + 10, 0, 0),
            new Point3D(originX, 10, 0)
        ];

    private static ScopeBounds2D Scope()
        => new()
        {
            MinX = 0,
            MinY = 0,
            MaxX = 100,
            MaxY = 100
        };
}
