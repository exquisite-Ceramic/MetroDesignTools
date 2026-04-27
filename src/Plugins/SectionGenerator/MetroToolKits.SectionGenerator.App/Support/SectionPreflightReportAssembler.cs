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
            .Select(diagnostic => CreateCheckItem("generation", "生成必配项", SectionPreflightSeverityDto.Blocking, diagnostic.Message))
            .ToList();
        var floorSaveDiagnostics = FloorConfigSaveValidator.Validate(config, _slabTemplateCatalog);
        var outputDiagnostics = SectionOutputConfigSaveValidator.Validate(document.OutputConfig);
        var floorResolution = BuildFloorResolution(config);

        var checks = new List<SectionPreflightCheckItemDto>
        {
            BuildDrawingStateCheck(runtimeState),
            BuildConfigSourceCheck(runtimeState),
            BuildFloorCountCheck(config),
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
        return CreateCheckItem("drawing", "当前图纸状态", severity, summary);
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
            BuildConfigSourceText(runtimeState));
    }

    private static SectionPreflightCheckItemDto BuildFloorCountCheck(SectionConfig config)
        => CreateCheckItem(
            "floor-count",
            "楼层数量",
            config.Floors.Count > 0 ? SectionPreflightSeverityDto.Info : SectionPreflightSeverityDto.Warning,
            $"当前共配置 {config.Floors.Count} 个楼层。");

    private static SectionPreflightCheckItemDto BuildBaseFloorExistsCheck(SectionConfig config)
    {
        if (config.Floors.Count <= 1)
        {
            return CreateCheckItem(
                "base-floor",
                "基准层是否存在",
                SectionPreflightSeverityDto.Info,
                "当前为单楼层模式，不强制要求基准层。");
        }

        if (string.IsNullOrWhiteSpace(config.AlignmentBaseFloorName))
        {
            return CreateCheckItem(
                "base-floor",
                "基准层是否存在",
                SectionPreflightSeverityDto.Blocking,
                "多楼层模式下尚未选择基准层。");
        }

        var exists = config.Floors.Any(floor =>
            string.Equals(floor.Name, config.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase));
        return CreateCheckItem(
            "base-floor",
            "基准层是否存在",
            exists ? SectionPreflightSeverityDto.Passed : SectionPreflightSeverityDto.Blocking,
            exists
                ? $"基准层已设置为 {config.AlignmentBaseFloorName}。"
                : $"未找到名为 {config.AlignmentBaseFloorName} 的基准层。");
    }

    private static SectionPreflightCheckItemDto BuildBaseAlignmentCheck(SectionConfig config)
    {
        if (config.Floors.Count <= 1)
        {
            return CreateCheckItem(
                "base-alignment",
                "基准层对齐点是否有效",
                SectionPreflightSeverityDto.Info,
                "当前为单楼层模式，不强制要求基准层三点对齐。");
        }

        var baseFloor = FindBaseFloor(config);
        if (baseFloor == null)
        {
            return CreateCheckItem(
                "base-alignment",
                "基准层对齐点是否有效",
                SectionPreflightSeverityDto.Blocking,
                "基准层不存在，无法检查对齐点。");
        }

        return FloorSectionLineTransformer.TryValidateAlignmentPoints(baseFloor.AlignmentPoints, out var error)
            ? CreateCheckItem(
                "base-alignment",
                "基准层对齐点是否有效",
                SectionPreflightSeverityDto.Passed,
                $"基准层 {baseFloor.Name} 已设置有效对齐点。")
            : CreateCheckItem(
                "base-alignment",
                "基准层对齐点是否有效",
                SectionPreflightSeverityDto.Blocking,
                $"基准层 {baseFloor.Name} 的对齐点无效: {error}");
    }

    private static SectionPreflightCheckItemDto BuildBaseScopeCheck(SectionConfig config)
    {
        if (config.Floors.Count <= 1)
        {
            return CreateCheckItem(
                "base-scope",
                "基准层范围是否有效",
                SectionPreflightSeverityDto.Info,
                "当前为单楼层模式，不强制要求基准层整层范围。");
        }

        var baseFloor = FindBaseFloor(config);
        if (baseFloor == null)
        {
            return CreateCheckItem(
                "base-scope",
                "基准层范围是否有效",
                SectionPreflightSeverityDto.Blocking,
                "基准层不存在，无法检查整层范围。");
        }

        if (!baseFloor.ScopeBounds.HasValue)
        {
            return CreateCheckItem(
                "base-scope",
                "基准层范围是否有效",
                SectionPreflightSeverityDto.Blocking,
                $"基准层 {baseFloor.Name} 缺少整层范围框。");
        }

        return baseFloor.ScopeBounds.Value.IsValid()
            ? CreateCheckItem(
                "base-scope",
                "基准层范围是否有效",
                SectionPreflightSeverityDto.Passed,
                $"基准层 {baseFloor.Name} 已设置有效整层范围。")
            : CreateCheckItem(
                "base-scope",
                "基准层范围是否有效",
                SectionPreflightSeverityDto.Blocking,
                $"基准层 {baseFloor.Name} 的整层范围框无效。");
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
                "当前没有非基准层会因对齐点或范围问题被跳过。");
        }

        var details = skippedFloors
            .Select(context => $"{context.FloorName}: {context.AlignmentIssue?.Message ?? context.ScopeIssue?.Message ?? "将被跳过"}");
        return CreateCheckItem(
            "skipped-floors",
            "非基准层是否会被跳过",
            SectionPreflightSeverityDto.Warning,
            $"以下楼层生成时会被跳过：{string.Join("；", details)}。");
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
                "当前所有已配置边界板模板都可以解析。");
        }

        return CreateCheckItem(
            "templates",
            "边界板模板是否缺失",
            SectionPreflightSeverityDto.Blocking,
            string.Join("；", templateDiagnostics.Select(diagnostic => diagnostic.Message)));
    }

    private static SectionPreflightCheckItemDto BuildOutputConfigCheck(IReadOnlyList<OperationDiagnostic> outputDiagnostics)
    {
        if (outputDiagnostics.Count == 0)
        {
            return CreateCheckItem(
                "output-config",
                "输出配置是否有明显错误",
                SectionPreflightSeverityDto.Passed,
                "输出图层和填充配置看起来有效。");
        }

        return CreateCheckItem(
            "output-config",
            "输出配置是否有明显错误",
            SectionPreflightSeverityDto.Blocking,
            string.Join("；", outputDiagnostics.Select(diagnostic => diagnostic.Message)));
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
                diagnostic.Message));
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
            : $"当前预检未通过。Blocking {blockingCount}，Warning {warningCount}，Info {infoCount}。";

    private static SectionPreflightCheckItemDto CreateCheckItem(
        string key,
        string title,
        SectionPreflightSeverityDto severity,
        string summary,
        string? floorName = null)
        => new()
        {
            Key = key,
            Title = title,
            Severity = severity,
            Summary = summary,
            FloorName = floorName
        };

    private sealed record FloorResolutionContext(
        FloorAlignmentResolution AlignmentResolution,
        FloorScopeResolution ScopeResolution);
}
