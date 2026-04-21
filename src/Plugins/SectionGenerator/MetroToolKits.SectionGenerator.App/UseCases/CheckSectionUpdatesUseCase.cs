using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
        _logger            = logger;
    }

    public IReadOnlyList<SectionCheckResult> Execute()
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("执行变更检测命令");

        var handles = _snapshotRepo.FindAllSectionBlockHandles();
        _logger.LogDebug("扫描剖面块，共 {Count} 个", handles.Count);

        var results = new List<SectionCheckResult>();
        var config  = _configRepo.Load();

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
            var currentSourceLine = ResolveSectionLine(snapshot);
            foreach (var floorSnap in snapshot.FloorSnapshots)
            {
                var floor = config.Floors.FirstOrDefault(f => f.Name == floorSnap.FloorName);
                if (floor == null) continue;

                var alignedSectionLine = FloorSectionLineTransformer.ApplyAlignment(
                    currentSourceLine,
                    floor);

                var elements = _elementRecognizer.RecognizeElements(
                    alignedSectionLine,
                    snapshot.ViewDepth).Elements;

                var currentHash = _hasher.ComputeHash(elements);

                _logger.LogDebug("剖面块 {BlockName}，楼层 {FloorName}，旧哈希: {OldHash}，新哈希: {NewHash}",
                    snapshot.BlockName, floorSnap.FloorName, floorSnap.GeometryHash, currentHash);

                if (currentHash != floorSnap.GeometryHash)
                    outdatedFloors.Add(floorSnap.FloorName);
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
                Snapshot       = snapshot
            });
        }

        sw.Stop();
        var outdatedCount = results.Count(r => r.Status == SectionUpdateStatus.Outdated);
        _logger.LogInformation("变更检测完成，共 {Total} 个剖面，{Outdated} 个需要更新，耗时 {ElapsedMs}ms",
            results.Count, outdatedCount, sw.ElapsedMilliseconds);

        return results;
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
}
