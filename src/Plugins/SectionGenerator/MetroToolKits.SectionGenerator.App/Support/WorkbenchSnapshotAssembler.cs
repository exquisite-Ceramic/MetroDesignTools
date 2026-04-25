using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Contracts.Workbench;

namespace MetroToolKits.SectionGenerator.App.Support;

public sealed class WorkbenchSnapshotAssembler : IWorkbenchSnapshotAssembler
{
    public SectionWorkbenchSnapshotDto Assemble(GenerateSectionPreflightResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var runtimeState = result.ConfigDocument.RuntimeState;
        var readiness = result.Readiness;
        var existingSections = result.ExistingSections;

        return new SectionWorkbenchSnapshotDto
        {
            Drawing = new DrawingStatusDto
            {
                DrawingDisplayName = runtimeState.DrawingDisplayName,
                StorageSource = MapStorageSource(runtimeState.Source),
                IsCurrentDrawingSaved = runtimeState.IsCurrentDrawingSaved,
                HasPersistedConfig = runtimeState.Source == SectionConfigStorageSource.EmbeddedDwg
            },
            FloorConfig = new FloorConfigStatusDto
            {
                CanGenerate = result.CanGenerate,
                FloorCount = result.ConfigDocument.Config.Floors.Count,
                MissingRequirementCount = result.MissingRequirements.Count,
                AlignmentBaseFloorName = result.ConfigDocument.Config.AlignmentBaseFloorName,
                SummaryText = result.CanGenerate
                    ? $"配置：当前图纸 [{runtimeState.DrawingDisplayName}] 已满足最小生成要求。"
                    : $"配置：当前图纸 [{runtimeState.DrawingDisplayName}] 仍缺少 {result.MissingRequirements.Count} 项必配内容。"
            },
            ElementReadiness = new ElementReadinessDto
            {
                HasRecognizableElements = readiness.HasRecognizableElements,
                TotalRecognizableElementCount = readiness.TotalRecognizableElementCount,
                RecognizableWallCount = readiness.RecognizableWallCount,
                RecognizableColumnCount = readiness.RecognizableColumnCount,
                RecognizableSlabCount = readiness.RecognizableSlabCount,
                TemplatedWallCount = readiness.TemplatedWallCount,
                TemplatedSlabCount = readiness.TemplatedSlabCount,
                LegacyWallCount = readiness.LegacyWallCount,
                LegacySlabCount = readiness.LegacySlabCount,
                SummaryText = readiness.HasRecognizableElements
                    ? $"构件：已发现 {readiness.TotalRecognizableElementCount} 个可识别墙/柱/板（墙 {readiness.RecognizableWallCount}，柱 {readiness.RecognizableColumnCount}，板 {readiness.RecognizableSlabCount}）。模板模式：墙 {readiness.TemplatedWallCount}，板 {readiness.TemplatedSlabCount}；稳定模式：墙 {readiness.LegacyWallCount}，板 {readiness.LegacySlabCount}。"
                    : "构件：当前图纸中尚未发现可识别的墙/柱/板。若图纸仍是原始图层，请先执行图层映射或区域转换。"
            },
            ExistingSections = new ExistingSectionStatusDto
            {
                CanInspect = string.IsNullOrWhiteSpace(existingSections.ErrorMessage),
                TotalCount = existingSections.TotalCount,
                UpToDateCount = existingSections.UpToDateCount,
                OutdatedCount = existingSections.OutdatedCount,
                UnknownCount = existingSections.UnknownCount,
                PartialCount = existingSections.PartialCount,
                ErrorMessage = existingSections.ErrorMessage,
                SummaryText = string.IsNullOrWhiteSpace(existingSections.ErrorMessage)
                    ? $"剖面：共 {existingSections.TotalCount} 个，需更新 {existingSections.OutdatedCount} 个，未知 {existingSections.UnknownCount} 个，部分检查 {existingSections.PartialCount} 个。"
                    : $"剖面：暂时无法检查已有剖面状态。{existingSections.ErrorMessage}"
            },
            RecommendedAction = BuildRecommendedAction(result.CanGenerate, readiness.HasRecognizableElements, existingSections),
            Issues = result.MissingRequirements.Select(MapIssue).ToArray()
        };
    }

    private static RecommendedActionDto BuildRecommendedAction(
        bool canGenerate,
        bool hasRecognizableElements,
        ExistingSectionStatusSummary existingSections)
    {
        if (!canGenerate)
        {
            return new RecommendedActionDto
            {
                Kind = RecommendedActionKindDto.ConfigureFloors,
                CommandTag = "FloorConfig",
                ButtonText = "补齐楼层配置",
                Message = "当前图纸还缺少楼层基准层、基准点或整层范围等必配项，建议先补齐楼层配置。"
            };
        }

        if (!hasRecognizableElements)
        {
            return new RecommendedActionDto
            {
                Kind = RecommendedActionKindDto.OpenLayerMapping,
                CommandTag = "LayerMapping",
                ButtonText = "去做图层映射",
                Message = "当前图纸还没有可识别构件，建议先完成图层映射或区域转换，再开始生成剖面。"
            };
        }

        if (existingSections.OutdatedCount > 0 || existingSections.UnknownCount > 0)
        {
            return new RecommendedActionDto
            {
                Kind = RecommendedActionKindDto.ReviewExistingSections,
                CommandTag = "GenSection",
                ButtonText = "开始生成剖面",
                Message = "当前图纸已可生成新剖面，但已有剖面里存在需更新或未知项，建议生成前先留意更新状态。"
            };
        }

        return new RecommendedActionDto
        {
            Kind = RecommendedActionKindDto.GenerateSection,
            CommandTag = "GenSection",
            ButtonText = "开始生成剖面",
            Message = "当前图纸已满足生成条件，可以直接开始生成剖面。"
        };
    }

    private static DrawingConfigSourceDto MapStorageSource(SectionConfigStorageSource source)
        => source switch
        {
            SectionConfigStorageSource.EmbeddedDwg => DrawingConfigSourceDto.EmbeddedDwg,
            SectionConfigStorageSource.TransientUnsavedDrawing => DrawingConfigSourceDto.TransientUnsavedDrawing,
            _ => DrawingConfigSourceDto.Missing
        };

    private static ValidationIssueDto MapIssue(OperationDiagnostic diagnostic)
        => new()
        {
            Code = diagnostic.Code,
            Message = diagnostic.Message,
            Severity = MapSeverity(diagnostic.Level),
            Target = diagnostic.TargetHandle
        };

    private static ValidationSeverityDto MapSeverity(DiagnosticLevel level)
        => level switch
        {
            DiagnosticLevel.Warning => ValidationSeverityDto.Warning,
            DiagnosticLevel.Error => ValidationSeverityDto.Error,
            _ => ValidationSeverityDto.Info
        };
}
