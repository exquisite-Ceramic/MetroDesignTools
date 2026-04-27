using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Contracts.Preflight;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Support;

public sealed class SectionPreflightReportAssembler : ISectionPreflightReportAssembler
{
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly FloorAlignmentResolver _alignmentResolver = new();
    private readonly FloorScopeResolver _scopeResolver = new();

    public SectionPreflightReportAssembler(ISlabAssemblyTemplateCatalog slabTemplateCatalog)
    {
        _slabTemplateCatalog = slabTemplateCatalog;
    }

    public SectionPreflightReportDto Assemble(GenerateSectionPreflightResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var document = NormalizeDocument(result.ConfigDocument);
        var config = document.Config;
        var runtimeState = document.RuntimeState;
        var blockingRequirements = result.MissingRequirements
            .Select(diagnostic => CreateCheckItem(
                "generation",
                "生成必配项",
                SectionPreflightSeverityDto.Blocking,
                diagnostic.Message,
                diagnostic.Suggestion ?? "请先补齐楼层配置中的必配项。",
                ResolveRelatedObjectName(diagnostic, "楼层配置"),
                SectionPreflightActionTargetDto.FloorConfig,
                "FloorConfig",
                ResolveFloorName(diagnostic)))
            .ToList();
        var floorSaveDiagnostics = FloorConfigSaveValidator.Validate(config, _slabTemplateCatalog);
        var outputDiagnostics = SectionOutputConfigSaveValidator.Validate(document.OutputConfig);
        var floorResolution = BuildFloorResolution(config);

        var checks = new List<SectionPreflightCheckItemDto>
        {
            BuildDrawingStateCheck(runtimeState),
            BuildConfigSourceCheck(runtimeState),
            BuildFloorCountCheck(config),
            BuildReadinessCheck(result.Readiness),
            BuildBaseFloorExistsCheck(config),
            BuildBaseAlignmentCheck(config),
            BuildBaseScopeCheck(config),
            BuildSkippedFloorCheck(floorResolution.ScopeResolution),
            BuildTemplateCheck(floorSaveDiagnostics),
            BuildOutputConfigCheck(outputDiagnostics)
        };

        checks.AddRange(BuildGeneralConfigChecks(floorSaveDiagnostics));
        checks.AddRange(blockingRequirements.Where(item =>
            !checks.Any(existing => existing.Summary == item.Summary && existing.Severity == item.Severity)));

        var reportFloors = config.Floors.Select(floor => BuildFloorStatusDto(floor, floorResolution)).ToArray();
        var blockingCount = checks.Count(item => item.Severity == SectionPreflightSeverityDto.Blocking);
        var warningCount = checks.Count(item => item.Severity == SectionPreflightSeverityDto.Warning);
        var infoCount = checks.Count(item => item.Severity == SectionPreflightSeverityDto.Info);
        var canGenerate = blockingCount == 0;

        return new SectionPreflightReportDto
        {
            DrawingDisplayName = string.IsNullOrWhiteSpace(runtimeState.DrawingDisplayName)
                ? "当前图纸"
                : runtimeState.DrawingDisplayName,
            DrawingStatusText = BuildDrawingStatusText(runtimeState),
            ConfigSourceText = BuildConfigSourceText(runtimeState),
            IsCurrentDrawingSaved = runtimeState.IsCurrentDrawingSaved,
            HasPersistedConfig = runtimeState.HasPersistedConfig,
            FloorCount = config.Floors.Count,
            AlignmentBaseFloorName = config.AlignmentBaseFloorName,
            CanGenerate = canGenerate,
            BlockingCount = blockingCount,
            WarningCount = warningCount,
            InfoCount = infoCount,
            SummaryText = BuildSummaryText(canGenerate, blockingCount, warningCount, infoCount),
            Checks = checks,
            Floors = reportFloors
        };
    }

    private static LoadedSectionConfig NormalizeDocument(LoadedSectionConfig? document)
    {
        document ??= new LoadedSectionConfig();
        document.Config ??= new SectionConfig();
        document.Config.Floors ??= new List<FloorConfig>();
        document.OutputConfig ??= new SectionOutputConfig();
        document.RuntimeState ??= new SectionConfigRuntimeState();
        document.RuntimeDiagnostics ??= new List<OperationDiagnostic>();
        return document;
    }

    private FloorResolutionContext BuildFloorResolution(SectionConfig config)
    {
        var dummySectionLine = new Line3D(new Point3D(0, 0, 0), new Point3D(0, 1000, 0));
        var alignmentResolution = _alignmentResolver.Resolve(dummySectionLine, config);
        var scopeResolution = _scopeResolver.Resolve(config, alignmentResolution, null, null, null);
        return new FloorResolutionContext(alignmentResolution, scopeResolution);
    }

    private static SectionPreflightCheckItemDto BuildDrawingStateCheck(SectionConfigRuntimeState runtimeState)
    {
        var severity = runtimeState.IsCurrentDrawingSaved
            ? SectionPreflightSeverityDto.Info
            : SectionPreflightSeverityDto.Warning;
        var summary = runtimeState.IsCurrentDrawingSaved
            ? $"当前图纸：{runtimeState.DrawingDisplayName}。"
            : $"当前图纸未保存，配置仅驻留在当前会话内：{runtimeState.DrawingDisplayName}。";
        return CreateCheckItem(
            "drawing",
            "当前图纸状态",
            severity,
            summary,
            runtimeState.IsCurrentDrawingSaved
                ? "当前无需处理。"
                : "如需持久保存配置，请先保存图纸，再回到楼层配置窗口执行保存。",
            "当前图纸",
            runtimeState.IsCurrentDrawingSaved ? SectionPreflightActionTargetDto.None : SectionPreflightActionTargetDto.FloorConfig,
            runtimeState.IsCurrentDrawingSaved ? string.Empty : "FloorConfig");
    }

    private static SectionPreflightCheckItemDto BuildConfigSourceCheck(SectionConfigRuntimeState runtimeState)
    {
        var severity = runtimeState.Source switch
        {
            SectionConfigStorageSource.EmbeddedDwg => SectionPreflightSeverityDto.Info,
            SectionConfigStorageSource.TransientUnsavedDrawing => SectionPreflightSeverityDto.Warning,
            _ => SectionPreflightSeverityDto.Warning
        };

        return CreateCheckItem(
            "config-source",
            "配置来源",
            severity,
            BuildConfigSourceText(runtimeState),
            runtimeState.Source == SectionConfigStorageSource.EmbeddedDwg
                ? "当前无需处理。"
                : "如需把当前配置写入 DWG，请打开楼层配置窗口后显式保存。",
            "楼层配置",
            runtimeState.Source == SectionConfigStorageSource.EmbeddedDwg
                ? SectionPreflightActionTargetDto.None
                : SectionPreflightActionTargetDto.FloorConfig,
            runtimeState.Source == SectionConfigStorageSource.EmbeddedDwg ? string.Empty : "FloorConfig");
    }

    private static SectionPreflightCheckItemDto BuildFloorCountCheck(SectionConfig config)
        => CreateCheckItem(
            "floor-count",
            "楼层数量",
            config.Floors.Count > 0 ? SectionPreflightSeverityDto.Info : SectionPreflightSeverityDto.Warning,
            $"当前共配置 {config.Floors.Count} 个楼层。",
            config.Floors.Count > 0 ? "当前无需处理。" : "请先打开楼层配置，至少配置一个楼层。",
            "楼层配置",
            config.Floors.Count > 0 ? SectionPreflightActionTargetDto.None : SectionPreflightActionTargetDto.FloorConfig,
            config.Floors.Count > 0 ? string.Empty : "FloorConfig");

    private static SectionPreflightCheckItemDto BuildReadinessCheck(GenerationReadinessSummary readiness)
        => CreateCheckItem(
            "readiness",
            "图层映射 / 构件识别状态",
            readiness.HasRecognizableElements ? SectionPreflightSeverityDto.Info : SectionPreflightSeverityDto.Warning,
            readiness.HasRecognizableElements
                ? $"当前已识别 {readiness.TotalRecognizableElementCount} 个墙/柱/板构件。"
                : "当前图纸中尚未发现可识别的墙/柱/板，后续生成可能无法得到有效剖面。",
            readiness.HasRecognizableElements
                ? "当前无需处理。"
                : "请检查图层映射或区域转换，使墙/柱/板进入可识别状态。",
            "图层映射",
            readiness.HasRecognizableElements ? SectionPreflightActionTargetDto.None : SectionPreflightActionTargetDto.LayerMapping,
            readiness.HasRecognizableElements ? string.Empty : "LayerMapping");

    private static SectionPreflightCheckItemDto BuildBaseFloorExistsCheck(SectionConfig config)
    {
        if (config.Floors.Count <= 1)
        {
            return CreateCheckItem(
                "base-floor",
                "基准层是否存在",
                SectionPreflightSeverityDto.Info,
                "当前为单楼层模式，不强制要求基准层。",
                "当前无需处理。",
                "楼层配置",
                SectionPreflightActionTargetDto.None,
                string.Empty);
        }

        if (string.IsNullOrWhiteSpace(config.AlignmentBaseFloorName))
        {
            return CreateCheckItem(
                "base-floor",
                "基准层是否存在",
                SectionPreflightSeverityDto.Blocking,
                "多楼层模式下尚未选择基准层。",
                "请打开楼层配置并指定一个基准层。",
                "楼层配置",
                SectionPreflightActionTargetDto.FloorConfig,
                "FloorConfig");
        }

        var exists = config.Floors.Any(floor =>
            string.Equals(floor.Name, config.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase));
        return CreateCheckItem(
            "base-floor",
            "基准层是否存在",
            exists ? SectionPreflightSeverityDto.Passed : SectionPreflightSeverityDto.Blocking,
            exists
                ? $"基准层已设置为 {config.AlignmentBaseFloorName}。"
                : $"未找到名为 {config.AlignmentBaseFloorName} 的基准层。",
            exists ? "当前无需处理。" : "请打开楼层配置，重新选择有效的基准层。",
            string.IsNullOrWhiteSpace(config.AlignmentBaseFloorName) ? "楼层配置" : config.AlignmentBaseFloorName,
            exists ? SectionPreflightActionTargetDto.None : SectionPreflightActionTargetDto.FloorConfig,
            exists ? string.Empty : "FloorConfig",
            exists ? config.AlignmentBaseFloorName : null);
    }

    private static SectionPreflightCheckItemDto BuildBaseAlignmentCheck(SectionConfig config)
    {
        if (config.Floors.Count <= 1)
        {
            return CreateCheckItem(
                "base-alignment",
                "基准层对齐点是否有效",
                SectionPreflightSeverityDto.Info,
                "当前为单楼层模式，不强制要求基准层三点对齐。",
                "当前无需处理。",
                "楼层配置",
                SectionPreflightActionTargetDto.None,
                string.Empty);
        }

        var baseFloor = FindBaseFloor(config);
        if (baseFloor == null)
        {
            return CreateCheckItem(
                "base-alignment",
                "基准层对齐点是否有效",
                SectionPreflightSeverityDto.Blocking,
                "基准层不存在，无法检查对齐点。",
                "请先在楼层配置中修正基准层设置。",
                "楼层配置",
                SectionPreflightActionTargetDto.FloorConfig,
                "FloorConfig");
        }

        return FloorSectionLineTransformer.TryValidateAlignmentPoints(baseFloor.AlignmentPoints, out var error)
            ? CreateCheckItem(
                "base-alignment",
                "基准层对齐点是否有效",
                SectionPreflightSeverityDto.Passed,
                $"基准层 {baseFloor.Name} 已设置有效对齐点。",
                "当前无需处理。",
                baseFloor.Name,
                SectionPreflightActionTargetDto.None,
                string.Empty,
                baseFloor.Name)
            : CreateCheckItem(
                "base-alignment",
                "基准层对齐点是否有效",
                SectionPreflightSeverityDto.Blocking,
                $"基准层 {baseFloor.Name} 的对齐点无效: {error}",
                "请打开楼层配置，重新拾取基准层的三点对齐。",
                baseFloor.Name,
                SectionPreflightActionTargetDto.FloorConfig,
                "FloorConfig",
                baseFloor.Name);
    }

    private static SectionPreflightCheckItemDto BuildBaseScopeCheck(SectionConfig config)
    {
        if (config.Floors.Count <= 1)
        {
            return CreateCheckItem(
                "base-scope",
                "基准层范围是否有效",
                SectionPreflightSeverityDto.Info,
                "当前为单楼层模式，不强制要求基准层整层范围。",
                "当前无需处理。",
                "楼层配置",
                SectionPreflightActionTargetDto.None,
                string.Empty);
        }

        var baseFloor = FindBaseFloor(config);
        if (baseFloor == null)
        {
            return CreateCheckItem(
                "base-scope",
                "基准层范围是否有效",
                SectionPreflightSeverityDto.Blocking,
                "基准层不存在，无法检查整层范围。",
                "请先在楼层配置中修正基准层设置。",
                "楼层配置",
                SectionPreflightActionTargetDto.FloorConfig,
                "FloorConfig");
        }

        if (!baseFloor.ScopeBounds.HasValue)
        {
            return CreateCheckItem(
                "base-scope",
                "基准层范围是否有效",
                SectionPreflightSeverityDto.Blocking,
                $"基准层 {baseFloor.Name} 缺少整层范围框。",
                "请打开楼层配置，重新拾取基准层整层范围。",
                baseFloor.Name,
                SectionPreflightActionTargetDto.FloorConfig,
                "FloorConfig",
                baseFloor.Name);
        }

        return baseFloor.ScopeBounds.Value.IsValid()
            ? CreateCheckItem(
                "base-scope",
                "基准层范围是否有效",
                SectionPreflightSeverityDto.Passed,
                $"基准层 {baseFloor.Name} 已设置有效整层范围。",
                "当前无需处理。",
                baseFloor.Name,
                SectionPreflightActionTargetDto.None,
                string.Empty,
                baseFloor.Name)
            : CreateCheckItem(
                "base-scope",
                "基准层范围是否有效",
                SectionPreflightSeverityDto.Blocking,
                $"基准层 {baseFloor.Name} 的整层范围框无效。",
                "请打开楼层配置，重新拾取基准层整层范围。",
                baseFloor.Name,
                SectionPreflightActionTargetDto.FloorConfig,
                "FloorConfig",
                baseFloor.Name);
    }

    private static SectionPreflightCheckItemDto BuildSkippedFloorCheck(FloorScopeResolution scopeResolution)
    {
        var skippedFloors = scopeResolution.Floors.Values
            .Where(context => !context.IsBaseFloor && !context.CanParticipate)
            .ToList();

        if (skippedFloors.Count == 0)
        {
            return CreateCheckItem(
                "skipped-floors",
                "非基准层是否会被跳过",
                SectionPreflightSeverityDto.Passed,
                "当前没有非基准层会因对齐点或范围问题被跳过。",
                "当前无需处理。",
                "楼层配置",
                SectionPreflightActionTargetDto.None,
                string.Empty);
        }

        var details = skippedFloors
            .Select(context => $"{context.FloorName}: {context.AlignmentIssue?.Message ?? context.ScopeIssue?.Message ?? "将被跳过"}");
        return CreateCheckItem(
            "skipped-floors",
            "非基准层是否会被跳过",
            SectionPreflightSeverityDto.Warning,
            $"以下楼层生成时会被跳过：{string.Join("；", details)}。",
            "请打开楼层配置，补齐这些楼层的对齐点或范围框。",
            string.Join("、", skippedFloors.Select(context => context.FloorName)),
            SectionPreflightActionTargetDto.FloorConfig,
            "FloorConfig");
    }

    private static SectionPreflightCheckItemDto BuildTemplateCheck(IReadOnlyList<OperationDiagnostic> floorDiagnostics)
    {
        var templateDiagnostics = floorDiagnostics
            .Where(diagnostic =>
                diagnostic.Code.EndsWith(".TopBoundaryTemplateMissing", StringComparison.OrdinalIgnoreCase) ||
                diagnostic.Code.EndsWith(".BottomBoundaryTemplateMissing", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (templateDiagnostics.Count == 0)
        {
            return CreateCheckItem(
                "templates",
                "边界板模板是否缺失",
                SectionPreflightSeverityDto.Passed,
                "当前所有已配置边界板模板都可以解析。",
                "当前无需处理。",
                "楼层配置",
                SectionPreflightActionTargetDto.None,
                string.Empty);
        }

        return CreateCheckItem(
            "templates",
            "边界板模板是否缺失",
            SectionPreflightSeverityDto.Blocking,
            string.Join("；", templateDiagnostics.Select(diagnostic => diagnostic.Message)),
            "请打开楼层配置，改用存在的边界板模板或清空错误模板标识。",
            ResolveRelatedObjectName(templateDiagnostics.First(), "楼层配置"),
            SectionPreflightActionTargetDto.FloorConfig,
            "FloorConfig",
            ResolveFloorName(templateDiagnostics.First()));
    }

    private static SectionPreflightCheckItemDto BuildOutputConfigCheck(IReadOnlyList<OperationDiagnostic> outputDiagnostics)
    {
        if (outputDiagnostics.Count == 0)
        {
            return CreateCheckItem(
                "output-config",
                "输出配置是否有明显错误",
                SectionPreflightSeverityDto.Passed,
                "输出图层和填充配置看起来有效。",
                "当前无需处理。",
                "输出配置",
                SectionPreflightActionTargetDto.None,
                string.Empty);
        }

        return CreateCheckItem(
            "output-config",
            "输出配置是否有明显错误",
            SectionPreflightSeverityDto.Blocking,
            string.Join("；", outputDiagnostics.Select(diagnostic => diagnostic.Message)),
            "请打开楼层配置中的输出设置，修正图层名和填充参数。",
            "输出配置",
            SectionPreflightActionTargetDto.FloorConfig,
            "FloorConfig");
    }

    private static IEnumerable<SectionPreflightCheckItemDto> BuildGeneralConfigChecks(
        IReadOnlyList<OperationDiagnostic> floorDiagnostics)
    {
        return floorDiagnostics
            .Where(diagnostic =>
                !diagnostic.Code.EndsWith(".TopBoundaryTemplateMissing", StringComparison.OrdinalIgnoreCase) &&
                !diagnostic.Code.EndsWith(".BottomBoundaryTemplateMissing", StringComparison.OrdinalIgnoreCase))
            .Select(diagnostic => CreateCheckItem(
                diagnostic.Code,
                "配置字段合法性",
                SectionPreflightSeverityDto.Blocking,
                diagnostic.Message,
                diagnostic.Suggestion ?? "请修正配置中的无效字段。",
                ResolveRelatedObjectName(diagnostic, "楼层配置"),
                SectionPreflightActionTargetDto.FloorConfig,
                "FloorConfig",
                ResolveFloorName(diagnostic)));
    }

    private static SectionPreflightFloorStatusDto BuildFloorStatusDto(FloorConfig floor, FloorResolutionContext resolution)
    {
        resolution.AlignmentResolution.Floors.TryGetValue(floor.Name, out var alignment);
        resolution.ScopeResolution.Floors.TryGetValue(floor.Name, out var scope);

        var boundaryMessages = new List<string>();
        if (!string.IsNullOrWhiteSpace(floor.TopBoundarySlab.TemplateId))
        {
            boundaryMessages.Add($"顶板模板: {floor.TopBoundarySlab.TemplateId}");
        }

        if (!string.IsNullOrWhiteSpace(floor.BottomBoundarySlab.TemplateId))
        {
            boundaryMessages.Add($"底板模板: {floor.BottomBoundarySlab.TemplateId}");
        }

        return new SectionPreflightFloorStatusDto
        {
            FloorName = floor.Name,
            IsBaseFloor = alignment?.IsBaseFloor == true,
            WillBeSkipped = scope is { IsBaseFloor: false, CanParticipate: false },
            AlignmentStatusText = BuildAlignmentStatusText(floor, alignment),
            ScopeStatusText = BuildScopeStatusText(floor, scope),
            BoundaryTemplateStatusText = boundaryMessages.Count == 0
                ? "未显式指定模板，允许使用兼容回退。"
                : string.Join("；", boundaryMessages),
            SkipReasonText = !string.IsNullOrWhiteSpace(scope?.AlignmentIssue?.Message)
                ? scope.AlignmentIssue.Message
                : scope?.ScopeIssue?.Message ?? string.Empty
        };
    }

    private static string BuildAlignmentStatusText(FloorConfig floor, FloorAlignmentResult? alignment)
    {
        if (alignment?.Issue != null)
        {
            return alignment.Issue.Message;
        }

        if (floor.AlignmentPoints.Count == 0)
        {
            return "未设置对齐点。";
        }

        return FloorSectionLineTransformer.TryValidateAlignmentPoints(floor.AlignmentPoints, out _)
            ? "对齐点有效。"
            : "对齐点无效。";
    }

    private static string BuildScopeStatusText(FloorConfig floor, FloorExecutionContext? context)
    {
        if (context?.ScopeIssue != null)
        {
            return context.ScopeIssue.Message;
        }

        if (!floor.ScopeBounds.HasValue)
        {
            return "未设置整层范围框。";
        }

        return floor.ScopeBounds.Value.IsValid()
            ? "整层范围有效。"
            : "整层范围无效。";
    }

    private static FloorConfig? FindBaseFloor(SectionConfig config)
        => string.IsNullOrWhiteSpace(config.AlignmentBaseFloorName)
            ? null
            : config.Floors.FirstOrDefault(floor =>
                string.Equals(floor.Name, config.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase));

    private static string BuildDrawingStatusText(SectionConfigRuntimeState runtimeState)
        => runtimeState.IsCurrentDrawingSaved
            ? $"当前图纸已保存：{runtimeState.DrawingDisplayName}"
            : $"当前图纸未保存：{runtimeState.DrawingDisplayName}";

    private static string BuildConfigSourceText(SectionConfigRuntimeState runtimeState)
        => runtimeState.Source switch
        {
            SectionConfigStorageSource.EmbeddedDwg => "配置来源：DWG 内嵌持久化配置。",
            SectionConfigStorageSource.TransientUnsavedDrawing => "配置来源：未保存图纸的会话内临时配置。",
            _ => "配置来源：当前图纸尚未找到已保存配置。"
        };

    private static string BuildSummaryText(bool canGenerate, int blockingCount, int warningCount, int infoCount)
        => canGenerate
            ? $"当前预检通过，可生成。Warning {warningCount}，Info {infoCount}。"
            : $"当前预检未通过，需先处理 {blockingCount} 个 Blocking 项。Warning {warningCount}，Info {infoCount}。";

    private static SectionPreflightCheckItemDto CreateCheckItem(
        string key,
        string title,
        SectionPreflightSeverityDto severity,
        string summary,
        string suggestedActionText,
        string relatedObjectName,
        SectionPreflightActionTargetDto actionTarget,
        string suggestedCommandTag,
        string? floorName = null)
        => new()
        {
            Key = key,
            Title = title,
            Severity = severity,
            Summary = summary,
            SuggestedActionText = suggestedActionText,
            RelatedObjectName = relatedObjectName,
            SuggestedCommandTag = suggestedCommandTag,
            ActionTarget = actionTarget,
            FloorName = floorName
        };

    private static string? ResolveFloorName(OperationDiagnostic diagnostic)
    {
        if (diagnostic.Metadata.TryGetValue("FloorName", out var floorName) &&
            !string.IsNullOrWhiteSpace(floorName))
        {
            return floorName;
        }

        return null;
    }

    private static string ResolveRelatedObjectName(OperationDiagnostic diagnostic, string fallback)
        => ResolveFloorName(diagnostic) ?? fallback;

    private sealed record FloorResolutionContext(
        FloorAlignmentResolution AlignmentResolution,
        FloorScopeResolution ScopeResolution);
}
