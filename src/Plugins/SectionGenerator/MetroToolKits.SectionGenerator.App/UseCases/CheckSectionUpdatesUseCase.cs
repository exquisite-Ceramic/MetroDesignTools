using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 变更检测用例 - 扫描所有剖面块，比对快照哈希，返回检测结果
/// </summary>
public sealed class CheckSectionUpdatesUseCase : ICheckSectionUpdatesUseCase
{
    private readonly ISectionSnapshotRepository _snapshotRepo;
    private readonly ISectionBlockQueryService _sectionBlockQueryService;
    private readonly ISectionLineResolver _sectionLineResolver;
    private readonly IElementRecognizer _elementRecognizer;
    private readonly IFloorConfigRepository _configRepo;
    private readonly FloorGeometryHasher _hasher;
    private readonly FloorAlignmentResolver _alignmentResolver;
    private readonly FloorScopeResolver _scopeResolver;
    private readonly FloorVerticalProfileBuilder _verticalProfileBuilder;
    private readonly ILogger<CheckSectionUpdatesUseCase> _logger;

    public CheckSectionUpdatesUseCase(
        ISectionSnapshotRepository snapshotRepo,
        ISectionBlockQueryService sectionBlockQueryService,
        ISectionLineResolver sectionLineResolver,
        IElementRecognizer elementRecognizer,
        IFloorConfigRepository configRepo,
        FloorGeometryHasher hasher,
        ILogger<CheckSectionUpdatesUseCase> logger)
        : this(
            snapshotRepo,
            sectionBlockQueryService,
            sectionLineResolver,
            elementRecognizer,
            configRepo,
            hasher,
            new FloorVerticalProfileBuilder(),
            logger)
    {
    }

    public CheckSectionUpdatesUseCase(
        ISectionSnapshotRepository snapshotRepo,
        ISectionBlockQueryService sectionBlockQueryService,
        ISectionLineResolver sectionLineResolver,
        IElementRecognizer elementRecognizer,
        IFloorConfigRepository configRepo,
        FloorGeometryHasher hasher,
        FloorVerticalProfileBuilder verticalProfileBuilder,
        ILogger<CheckSectionUpdatesUseCase> logger)
    {
        _snapshotRepo      = snapshotRepo;
        _sectionBlockQueryService = sectionBlockQueryService;
        _sectionLineResolver = sectionLineResolver;
        _elementRecognizer = elementRecognizer;
        _configRepo        = configRepo;
        _hasher            = hasher;
        _alignmentResolver = new FloorAlignmentResolver();
        _scopeResolver     = new FloorScopeResolver();
        _verticalProfileBuilder = verticalProfileBuilder;
        _logger            = logger;
    }

    public CheckSectionUpdatesResult Execute()
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("执行变更检测命令");
        var diagnostics = new List<OperationDiagnostic>();

        var handles = _sectionBlockQueryService.FindAllSectionBlockHandles();
        _logger.LogDebug("扫描剖面块，共 {Count} 个", handles.Count);

        var results = new List<SectionCheckResult>();
        var sourceDocument  = _configRepo.Load();
        diagnostics.AddRange(sourceDocument.RuntimeDiagnostics);

        foreach (var handle in handles)
        {
            var snapshot = _snapshotRepo.Load(handle);
            if (snapshot == null)
            {
                results.Add(new SectionCheckResult
                {
                    BlockHandle = handle,
                    BlockName   = handle,
                    Status      = SectionUpdateStatus.Unknown
                });
                _logger.LogDebug("剖面块 {Handle} 无快照记录", handle);
                continue;
            }

            // 重新计算当前哈希，与快照比对
            var outdatedFloors = new List<string>();
            var skippedFloors = new List<string>();
            var expectedFloorNames = SectionSnapshotFloorScope.ResolveExecutionFloorNames(snapshot);

            if (!SectionExecutionConfigBuilder.TryBuild(
                    sourceDocument,
                    expectedFloorNames,
                    snapshot.TargetFloorName,
                    out var executionDocument,
                    out var configError))
            {
                results.Add(new SectionCheckResult
                {
                    BlockHandle = handle,
                    BlockName = snapshot.BlockName,
                    Status = SectionUpdateStatus.Unknown,
                    SkippedFloors = expectedFloorNames.ToList(),
                    WarningMessage = configError,
                    Snapshot = snapshot
                });
                continue;
            }

            var config = executionDocument.Config;
            var outputConfig = executionDocument.OutputConfig;
            var currentSourceLine = AlignToSnapshotDirection(ResolveSectionLine(snapshot), snapshot);
            var alignmentResolution = _alignmentResolver.Resolve(currentSourceLine, config);
            if (alignmentResolution.FatalIssue != null)
            {
                return BuildFailedResult(
                    MapAlignmentFailure(alignmentResolution.FatalIssue),
                    diagnostics,
                    sw);
            }

            var scopeResolution = _scopeResolver.Resolve(
                config,
                alignmentResolution,
                snapshot.TargetFloorName,
                snapshot.LocalScopeBounds,
                snapshot.LocalScopeFloorName);

            if (scopeResolution.FatalIssue != null)
            {
                return BuildFailedResult(
                    MapScopeFailure(scopeResolution.FatalIssue, config),
                    diagnostics,
                    sw);
            }

            var hasUncheckableGeneratedFloor = false;
            foreach (var floorSnap in snapshot.FloorSnapshots)
            {
                var floor = config.Floors.FirstOrDefault(f => f.Name == floorSnap.FloorName);
                if (floor == null)
                {
                    skippedFloors.Add(floorSnap.FloorName);
                    hasUncheckableGeneratedFloor = true;
                    continue;
                }

                if (!scopeResolution.Floors.TryGetValue(floor.Name, out var context))
                {
                    skippedFloors.Add(floor.Name);
                    hasUncheckableGeneratedFloor = true;
                    continue;
                }

                if (context.AlignmentIssue != null)
                {
                    diagnostics.Add(MapAlignmentDiagnostic(context));
                }

                if (context.ScopeIssue != null)
                {
                    diagnostics.Add(MapScopeDiagnostic(context));
                }

                if (!context.CanParticipate || !context.SectionLine.HasValue)
                {
                    skippedFloors.Add(floor.Name);
                    hasUncheckableGeneratedFloor = true;
                    continue;
                }
                var elements = _elementRecognizer.RecognizeElements(
                    context.SectionLine.Value,
                    snapshot.ViewDepth,
                    context.EffectiveScope).Elements;

                var baseElevation = ComputeBaseElevation(config, floor.Name, currentSourceLine.Length);
                var verticalProfile = _verticalProfileBuilder.Build(floor, currentSourceLine.Length, baseElevation);
                var currentHash = SectionOutputConfigHasher.Combine(
                    _hasher.ComputeHash(elements, floor, verticalProfile),
                    outputConfig);

                _logger.LogDebug("剖面块 {BlockName}，楼层 {FloorName}，旧哈希: {OldHash}，新哈希: {NewHash}",
                    snapshot.BlockName, floorSnap.FloorName, floorSnap.GeometryHash, currentHash);

                if (currentHash != floorSnap.GeometryHash)
                    outdatedFloors.Add(floorSnap.FloorName);
            }

            var status = hasUncheckableGeneratedFloor
                ? SectionUpdateStatus.Unknown
                : outdatedFloors.Count > 0
                    ? SectionUpdateStatus.Outdated
                    : SectionUpdateStatus.UpToDate;

            results.Add(new SectionCheckResult
            {
                BlockHandle    = handle,
                BlockName      = snapshot.BlockName,
                Status         = status,
                OutdatedFloors = outdatedFloors,
                SkippedFloors  = skippedFloors,
                WarningMessage = skippedFloors.Count > 0
                    ? $"部分楼层未参与检查: {string.Join(", ", skippedFloors)}"
                    : null,
                Snapshot       = snapshot
            });
        }

        sw.Stop();
        var outdatedCount = results.Count(r => r.Status == SectionUpdateStatus.Outdated);
        _logger.LogInformation("变更检测完成，共 {Total} 个剖面，{Outdated} 个需要更新，耗时 {ElapsedMs}ms",
            results.Count, outdatedCount, sw.ElapsedMilliseconds);

        var finalStatus = diagnostics.Any(d => d.Level == DiagnosticLevel.Warning || d.Level == DiagnosticLevel.Error) ||
                          results.Any(r => r.SkippedFloors.Count > 0)
            ? OperationStatus.PartialSuccess
            : OperationStatus.Success;

        return new CheckSectionUpdatesResult
        {
            Status = finalStatus,
            Diagnostics = diagnostics,
            Items = results
        };
    }

    private double ComputeBaseElevation(SectionConfig config, string floorName, double sectionLength)
    {
        double cumulative = 0;
        foreach (var floor in config.Floors)
        {
            if (string.Equals(floor.Name, floorName, StringComparison.OrdinalIgnoreCase))
            {
                return cumulative;
            }

            var profile = _verticalProfileBuilder.Build(floor, sectionLength, cumulative);
            cumulative +=
                (profile.GetBottomBoundaryTop(0) - profile.GetBottomBoundaryBottom(0)) +
                floor.Height +
                (profile.GetTopBoundaryTop(0) - profile.GetTopBoundaryBottom(0));
        }

        return cumulative;
    }

    private Foundation.Core.Geometry.Line3D ResolveSectionLine(SectionSnapshot snapshot)
    {
        var resolved = _sectionLineResolver.ResolveCurrentLine(snapshot.SourceCutLineHandle);
        if (resolved.HasValue)
            return resolved.Value;

        _logger.LogWarning(
            "无法解析源剖切线 {CutLineHandle}，回退到快照线坐标。Block={BlockName}",
            snapshot.SourceCutLineHandle,
            snapshot.BlockName);

        return new Foundation.Core.Geometry.Line3D(snapshot.CutLineStart, snapshot.CutLineEnd);
    }

    private static Foundation.Core.Geometry.Line3D AlignToSnapshotDirection(
        Foundation.Core.Geometry.Line3D currentLine,
        SectionSnapshot snapshot)
    {
        var snapshotDirection = snapshot.SectionDirection.HasValue
            ? new Vector3D(snapshot.SectionDirection.Value.X, snapshot.SectionDirection.Value.Y, snapshot.SectionDirection.Value.Z)
            : new Vector3D(
                snapshot.CutLineEnd.X - snapshot.CutLineStart.X,
                snapshot.CutLineEnd.Y - snapshot.CutLineStart.Y,
                0);

        if (snapshotDirection.Length <= 1e-9)
        {
            return currentLine;
        }

        var currentDirection = currentLine.Direction;
        if (currentDirection.Length <= 1e-9)
        {
            return currentLine;
        }

        return Vector3D.Dot(snapshotDirection.Normalized, currentDirection.Normalized) < 0
            ? new Foundation.Core.Geometry.Line3D(currentLine.End, currentLine.Start)
            : currentLine;
    }

    private CheckSectionUpdatesResult BuildFailedResult(
        OperationFailure failure,
        IReadOnlyList<OperationDiagnostic> diagnostics,
        Stopwatch sw)
    {
        sw.Stop();
        _logger.LogError(
            failure.InnerException,
            "变更检测失败，Code={Code}，Stage={Stage}，Module={Module}，耗时: {ElapsedMs}ms",
            failure.Code,
            failure.Stage,
            failure.Module,
            sw.ElapsedMilliseconds);

        return new CheckSectionUpdatesResult
        {
            Status = OperationStatus.Failed,
            Failure = failure,
            FailedStage = failure.Stage,
            Diagnostics = diagnostics
        };
    }

    private static OperationFailure MapAlignmentFailure(FloorAlignmentIssue issue)
    {
        return issue.Kind switch
        {
            FloorAlignmentIssueKind.AlignmentBaseFloorMissing =>
                SectionGenerationFailures.AlignmentBaseFloorMissing(issue.Message),
            _ => SectionGenerationFailures.AlignmentBaseFloorInvalid(issue.Message)
        };
    }

    private static OperationFailure MapScopeFailure(FloorScopeIssue issue, SectionConfig config)
    {
        var isBaseFloorScopeIssue = issue.Kind is FloorScopeIssueKind.FloorScopeMissing or FloorScopeIssueKind.FloorScopeInvalid
            && !string.IsNullOrWhiteSpace(config.AlignmentBaseFloorName)
            && issue.Message.Contains(config.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase);

        return issue.Kind switch
        {
            FloorScopeIssueKind.TargetFloorInvalid =>
                SectionGenerationFailures.TargetFloorInvalid(issue.Message),
            FloorScopeIssueKind.LocalScopeInvalid =>
                SectionGenerationFailures.LocalScopeInvalid(issue.Message),
            _ => SectionGenerationFailures.FloorScopeInvalid(issue.Message, isBaseFloorScopeIssue)
        };
    }

    private static OperationDiagnostic MapAlignmentDiagnostic(FloorExecutionContext context)
    {
        return context.AlignmentIssue?.Kind switch
        {
            FloorAlignmentIssueKind.AlignmentPointsMissing =>
                SectionGenerationDiagnosticFactory.AlignmentPointsMissing(context.FloorName, context.IsBaseFloor),
            FloorAlignmentIssueKind.AlignmentPointsInvalid =>
                SectionGenerationDiagnosticFactory.AlignmentPointsInvalid(
                    context.FloorName,
                    context.AlignmentIssue!.Message,
                    context.IsBaseFloor),
            _ => SectionGenerationDiagnosticFactory.AlignmentPointsInvalid(
                context.FloorName,
                context.AlignmentIssue?.Message ?? "未知对齐配置问题",
                context.IsBaseFloor)
        };
    }

    private static OperationDiagnostic MapScopeDiagnostic(FloorExecutionContext context)
    {
        return context.ScopeIssue?.Kind switch
        {
            FloorScopeIssueKind.FloorScopeMissing =>
                SectionGenerationDiagnosticFactory.FloorScopeMissing(context.FloorName, context.IsBaseFloor),
            FloorScopeIssueKind.FloorScopeInvalid =>
                SectionGenerationDiagnosticFactory.FloorScopeInvalid(
                    context.FloorName,
                    context.ScopeIssue!.Message,
                    context.IsBaseFloor),
            _ => SectionGenerationDiagnosticFactory.LocalScopeInvalid(
                context.FloorName,
                context.ScopeIssue?.Message ?? "未知范围问题")
        };
    }
}
