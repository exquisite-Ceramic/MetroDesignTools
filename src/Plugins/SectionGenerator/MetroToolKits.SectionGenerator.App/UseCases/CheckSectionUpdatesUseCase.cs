using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 变更检测用例 - 扫描所有剖面块，比对快照哈希，返回检测结果
/// </summary>
public sealed class CheckSectionUpdatesUseCase : ICheckSectionUpdatesUseCase
{
    private readonly ISectionSnapshotRepository _snapshotRepo;
    private readonly ISectionLineResolver _sectionLineResolver;
    private readonly IElementRecognizer _elementRecognizer;
    private readonly IFloorConfigRepository _configRepo;
    private readonly FloorGeometryHasher _hasher;
    private readonly FloorAlignmentResolver _alignmentResolver;
    private readonly ILogger<CheckSectionUpdatesUseCase> _logger;

    public CheckSectionUpdatesUseCase(
        ISectionSnapshotRepository snapshotRepo,
        ISectionLineResolver sectionLineResolver,
        IElementRecognizer elementRecognizer,
        IFloorConfigRepository configRepo,
        FloorGeometryHasher hasher,
        ILogger<CheckSectionUpdatesUseCase> logger)
    {
        _snapshotRepo      = snapshotRepo;
        _sectionLineResolver = sectionLineResolver;
        _elementRecognizer = elementRecognizer;
        _configRepo        = configRepo;
        _hasher            = hasher;
        _alignmentResolver = new FloorAlignmentResolver();
        _logger            = logger;
    }

    public CheckSectionUpdatesResult Execute()
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("执行变更检测命令");
        var diagnostics = new List<OperationDiagnostic>();

        var handles = _snapshotRepo.FindAllSectionBlockHandles();
        _logger.LogDebug("扫描剖面块，共 {Count} 个", handles.Count);

        var results = new List<SectionCheckResult>();
        var config  = _configRepo.Load();
        diagnostics.AddRange(config.RuntimeDiagnostics);

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
            var currentSourceLine = ResolveSectionLine(snapshot);
            var alignmentResolution = _alignmentResolver.Resolve(currentSourceLine, config);
            if (alignmentResolution.FatalIssue != null)
            {
                return BuildFailedResult(
                    MapAlignmentFailure(alignmentResolution.FatalIssue),
                    diagnostics,
                    sw);
            }

            var currentParticipatingFloors = new HashSet<string>(
                alignmentResolution.Floors.Values
                    .Where(alignment => alignment.CanParticipate)
                    .Select(alignment => alignment.FloorName),
                StringComparer.OrdinalIgnoreCase);
            foreach (var floorSnap in snapshot.FloorSnapshots)
            {
                var floor = config.Floors.FirstOrDefault(f => f.Name == floorSnap.FloorName);
                if (floor == null)
                {
                    outdatedFloors.Add(floorSnap.FloorName);
                    continue;
                }

                if (!alignmentResolution.Floors.TryGetValue(floor.Name, out var alignment))
                {
                    outdatedFloors.Add(floor.Name);
                    continue;
                }

                if (alignment.Issue != null)
                {
                    diagnostics.Add(MapAlignmentDiagnostic(alignment));
                }

                if (!alignment.CanParticipate || !alignment.SectionLine.HasValue)
                {
                    skippedFloors.Add(floor.Name);
                    continue;
                }
                var elements = _elementRecognizer.RecognizeElements(
                    alignment.SectionLine.Value,
                    snapshot.ViewDepth).Elements;

                var currentHash = _hasher.ComputeHash(elements);

                _logger.LogDebug("剖面块 {BlockName}，楼层 {FloorName}，旧哈希: {OldHash}，新哈希: {NewHash}",
                    snapshot.BlockName, floorSnap.FloorName, floorSnap.GeometryHash, currentHash);

                if (currentHash != floorSnap.GeometryHash)
                    outdatedFloors.Add(floorSnap.FloorName);
            }

            var snapshotFloors = new HashSet<string>(
                snapshot.FloorSnapshots.Select(f => f.FloorName),
                StringComparer.OrdinalIgnoreCase);
            if (!snapshotFloors.SetEquals(currentParticipatingFloors))
            {
                foreach (var floorName in snapshotFloors.Union(currentParticipatingFloors))
                {
                    if (!snapshotFloors.Contains(floorName) || !currentParticipatingFloors.Contains(floorName))
                    {
                        if (!outdatedFloors.Contains(floorName, StringComparer.OrdinalIgnoreCase))
                            outdatedFloors.Add(floorName);
                    }
                }
            }

            var status = outdatedFloors.Count > 0
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

    private static OperationDiagnostic MapAlignmentDiagnostic(FloorAlignmentResult alignment)
    {
        return alignment.Issue?.Kind switch
        {
            FloorAlignmentIssueKind.AlignmentPointsMissing =>
                SectionGenerationDiagnosticFactory.AlignmentPointsMissing(alignment.FloorName),
            FloorAlignmentIssueKind.AlignmentPointsInvalid =>
                SectionGenerationDiagnosticFactory.AlignmentPointsInvalid(
                    alignment.FloorName,
                    alignment.Issue!.Message),
            _ => SectionGenerationDiagnosticFactory.AlignmentPointsInvalid(
                alignment.FloorName,
                alignment.Issue?.Message ?? "未知对齐配置问题")
        };
    }
}
