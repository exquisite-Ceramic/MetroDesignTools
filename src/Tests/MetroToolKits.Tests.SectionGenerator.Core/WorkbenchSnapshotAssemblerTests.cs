using FluentAssertions;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Contracts.Workbench;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class WorkbenchSnapshotAssemblerTests
{
    private readonly IWorkbenchSnapshotAssembler _assembler = new WorkbenchSnapshotAssembler();

    [Fact]
    public void Assemble_WhenMissingRequirements_ReturnsFloorConfigRecommendation()
    {
        var result = CreateResult(
            drawingName: "A.dwg",
            source: SectionConfigStorageSource.Missing,
            missingRequirements:
            [
                new OperationDiagnostic
                {
                    Code = "MissingBaseFloor",
                    Message = "缺少基准层",
                    Level = DiagnosticLevel.Error,
                    TargetHandle = "F1"
                }
            ]);

        var snapshot = _assembler.Assemble(result);

        snapshot.FloorConfig.CanGenerate.Should().BeFalse();
        snapshot.FloorConfig.SummaryText.Should().Be("配置：当前图纸 [A.dwg] 仍缺少 1 项必配内容。");
        snapshot.RecommendedAction.Kind.Should().Be(RecommendedActionKindDto.ConfigureFloors);
        snapshot.RecommendedAction.CommandTag.Should().Be("FloorConfig");
        snapshot.RecommendedAction.ButtonText.Should().Be("补齐楼层配置");
        snapshot.RecommendedAction.Message.Should().Be("当前图纸还缺少楼层基准层、基准点或整层范围等必配项，建议先补齐楼层配置。");
        snapshot.Issues.Should().ContainSingle();
        snapshot.Issues[0].Code.Should().Be("MissingBaseFloor");
        snapshot.Issues[0].Severity.Should().Be(ValidationSeverityDto.Error);
        snapshot.Issues[0].Target.Should().Be("F1");
        snapshot.Drawing.HasPersistedConfig.Should().BeFalse();
    }

    [Fact]
    public void Assemble_WhenNoRecognizableElements_ReturnsLayerMappingRecommendation()
    {
        var result = CreateResult(
            drawingName: "B.dwg",
            source: SectionConfigStorageSource.EmbeddedDwg,
            readiness: new GenerationReadinessSummary());

        var snapshot = _assembler.Assemble(result);

        snapshot.ElementReadiness.HasRecognizableElements.Should().BeFalse();
        snapshot.ElementReadiness.SummaryText.Should().Be("构件：当前图纸中尚未发现可识别的墙/柱/板。若图纸仍是原始图层，请先执行图层映射或区域转换。");
        snapshot.RecommendedAction.Kind.Should().Be(RecommendedActionKindDto.OpenLayerMapping);
        snapshot.RecommendedAction.CommandTag.Should().Be("LayerMapping");
        snapshot.RecommendedAction.ButtonText.Should().Be("去做图层映射");
        snapshot.RecommendedAction.Message.Should().Be("当前图纸还没有可识别构件，建议先完成图层映射或区域转换，再开始生成剖面。");
    }

    [Fact]
    public void Assemble_WhenExistingSectionsNeedAttention_ReturnsReviewRecommendation()
    {
        var result = CreateResult(
            drawingName: "C.dwg",
            source: SectionConfigStorageSource.EmbeddedDwg,
            readiness: new GenerationReadinessSummary
            {
                TotalRecognizableElementCount = 3,
                RecognizableWallCount = 1,
                RecognizableColumnCount = 1,
                RecognizableSlabCount = 1,
                TemplatedWallCount = 1,
                TemplatedSlabCount = 1,
                LegacyWallCount = 0,
                LegacySlabCount = 0
            },
            existingSections: new ExistingSectionStatusSummary
            {
                TotalCount = 2,
                OutdatedCount = 1,
                UnknownCount = 1,
                PartialCount = 1
            });

        var snapshot = _assembler.Assemble(result);

        snapshot.ExistingSections.SummaryText.Should().Be("剖面：共 2 个，需更新 1 个，未知 1 个，部分检查 1 个。");
        snapshot.RecommendedAction.Kind.Should().Be(RecommendedActionKindDto.ReviewExistingSections);
        snapshot.RecommendedAction.CommandTag.Should().Be("GenSection");
        snapshot.RecommendedAction.ButtonText.Should().Be("开始生成剖面");
        snapshot.RecommendedAction.Message.Should().Be("当前图纸已可生成新剖面，但已有剖面里存在需更新或未知项，建议生成前先留意更新状态。");
    }

    [Fact]
    public void Assemble_WhenEverythingReady_ReturnsGenerateRecommendation()
    {
        var result = CreateResult(
            drawingName: "D.dwg",
            source: SectionConfigStorageSource.EmbeddedDwg,
            readiness: new GenerationReadinessSummary
            {
                TotalRecognizableElementCount = 4,
                RecognizableWallCount = 2,
                RecognizableColumnCount = 1,
                RecognizableSlabCount = 1,
                TemplatedWallCount = 1,
                TemplatedSlabCount = 1,
                LegacyWallCount = 1,
                LegacySlabCount = 0
            },
            existingSections: new ExistingSectionStatusSummary
            {
                TotalCount = 1,
                UpToDateCount = 1
            });

        var snapshot = _assembler.Assemble(result);

        snapshot.FloorConfig.SummaryText.Should().Be("配置：当前图纸 [D.dwg] 已满足最小生成要求。");
        snapshot.ElementReadiness.SummaryText.Should().Be("构件：已发现 4 个可识别墙/柱/板（墙 2，柱 1，板 1）。模板模式：墙 1，板 1；稳定模式：墙 1，板 0。");
        snapshot.RecommendedAction.Kind.Should().Be(RecommendedActionKindDto.GenerateSection);
        snapshot.RecommendedAction.CommandTag.Should().Be("GenSection");
        snapshot.RecommendedAction.ButtonText.Should().Be("开始生成剖面");
        snapshot.RecommendedAction.Message.Should().Be("当前图纸已满足生成条件，可以直接开始生成剖面。");
    }

    [Fact]
    public void Assemble_WhenRuntimeStateHasPersistedConfigFalseButSourceEmbeddedDwg_UsesEmbeddedRule()
    {
        var result = CreateResult(
            drawingName: "E.dwg",
            source: SectionConfigStorageSource.EmbeddedDwg,
            hasPersistedConfig: false);

        var snapshot = _assembler.Assemble(result);

        snapshot.Drawing.StorageSource.Should().Be(DrawingConfigSourceDto.EmbeddedDwg);
        snapshot.Drawing.HasPersistedConfig.Should().BeTrue();
    }

    private static GenerateSectionPreflightResult CreateResult(
        string drawingName,
        SectionConfigStorageSource source,
        IReadOnlyList<OperationDiagnostic>? missingRequirements = null,
        GenerationReadinessSummary? readiness = null,
        ExistingSectionStatusSummary? existingSections = null,
        bool hasPersistedConfig = true)
    {
        return new GenerateSectionPreflightResult
        {
            ConfigDocument = new LoadedSectionConfig
            {
                Config = new SectionConfig
                {
                    AlignmentBaseFloorName = "F1",
                    Floors = new List<FloorConfig>
                    {
                        new()
                        {
                            Name = "F1"
                        }
                    }
                },
                RuntimeState = new SectionConfigRuntimeState
                {
                    Source = source,
                    DrawingDisplayName = drawingName,
                    IsCurrentDrawingSaved = true,
                    HasPersistedConfig = hasPersistedConfig
                }
            },
            MissingRequirements = missingRequirements ?? Array.Empty<OperationDiagnostic>(),
            Readiness = readiness ?? new GenerationReadinessSummary(),
            ExistingSections = existingSections ?? new ExistingSectionStatusSummary()
        };
    }
}
