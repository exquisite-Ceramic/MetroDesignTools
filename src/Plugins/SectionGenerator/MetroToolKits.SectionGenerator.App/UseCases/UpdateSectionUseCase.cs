using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 单剖面更新用例 - 读取原剖切线和配置，重新生成并替换旧块
/// </summary>
public sealed class UpdateSectionUseCase : IUpdateSectionUseCase
{
    private readonly ISectionSnapshotRepository _snapshotRepo;
    private readonly ISectionGeometryRecoveryService _sectionGeometryRecoveryService;
    private readonly ISectionLineResolver _sectionLineResolver;
    private readonly IGenerateSectionUseCase _generateUseCase;
    private readonly IBlockEraseService _blockEraseService;
    private readonly ILogger<UpdateSectionUseCase> _logger;
    private readonly IUserLogger _userLogger;

    public UpdateSectionUseCase(
        ISectionSnapshotRepository snapshotRepo,
        ISectionGeometryRecoveryService sectionGeometryRecoveryService,
        ISectionLineResolver sectionLineResolver,
        IGenerateSectionUseCase generateUseCase,
        IBlockEraseService blockEraseService,
        ILogger<UpdateSectionUseCase> logger,
        IUserLogger userLogger)
    {
        _snapshotRepo     = snapshotRepo;
        _sectionGeometryRecoveryService = sectionGeometryRecoveryService;
        _sectionLineResolver = sectionLineResolver;
        _generateUseCase  = generateUseCase;
        _blockEraseService = blockEraseService;
        _logger           = logger;
        _userLogger       = userLogger;
    }

    public UpdateSectionResult Execute(UpdateSectionRequest request)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("更新剖面块 {Handle}", request.BlockHandle);

        try
        {
            // 1. 读取快照（含原剖切线句柄和插入点）
            var snapshot = _snapshotRepo.Load(request.BlockHandle);
            if (snapshot == null)
            {
                _logger.LogWarning("剖面块 {Handle} 无快照，无法更新", request.BlockHandle);
                return new UpdateSectionResult
                {
                    Success      = false,
                    ErrorMessage = "剖面快照不存在，请手动重新生成"
                };
            }

            if (string.IsNullOrEmpty(snapshot.SourceCutLineHandle))
            {
                _logger.LogWarning("剖面块 {BlockName} 的原始剖切线句柄为空", snapshot.BlockName);
                _userLogger.SectionUpdateFailed(snapshot.BlockName, "原始剖切线已不存在，请手动重新生成");
                return new UpdateSectionResult
                {
                    Success      = false,
                    ErrorMessage = "原始剖切线句柄丢失"
                };
            }

            var currentSectionLine = _sectionLineResolver.ResolveCurrentLine(snapshot.SourceCutLineHandle);
            if (!currentSectionLine.HasValue)
            {
                _logger.LogWarning("剖面块 {BlockName} 的原始剖切线 {Handle} 已不存在或不再是直线", snapshot.BlockName, snapshot.SourceCutLineHandle);
                _userLogger.SectionUpdateFailed(snapshot.BlockName, "原始剖切线已不存在，请手动重新生成");
                return new UpdateSectionResult
                {
                    Success = false,
                    ErrorMessage = "原始剖切线已不存在或不是直线"
                };
            }

            var snapshotDirection = ResolveSnapshotDirection(snapshot);
            var geometryAnchorX = ResolveGeometryAnchor(request.BlockHandle, snapshot);
            var alignedSectionLine = AlignToSnapshotDirection(currentSectionLine.Value, snapshotDirection);
            EnsureSnapshotCompatibilityFields(request.BlockHandle, snapshot, geometryAnchorX, snapshotDirection);

            // 2. 重新生成（使用快照中记录的插入点）
            var includedFloorNames = SectionSnapshotFloorScope.ResolveExecutionFloorNames(snapshot);

            var genResult = _generateUseCase.Execute(new GenerateSectionRequest
            {
                CutLineHandle = snapshot.SourceCutLineHandle,
                CutLineStart = alignedSectionLine.Start,
                CutLineEnd = alignedSectionLine.End,
                InsertionPoint = snapshot.InsertionPoint,
                GeometryAnchorX = geometryAnchorX,
                ViewDepth = snapshot.ViewDepth,
                TargetFloorName = snapshot.TargetFloorName,
                LocalScopeBounds = snapshot.LocalScopeBounds,
                LocalScopeFloorName = snapshot.LocalScopeFloorName,
                IncludedFloorNames = includedFloorNames,
                RequireCompleteIncludedFloors = true
            });

            if (!genResult.Success)
            {
                _logger.LogError("重新生成剖面失败: {Error}", genResult.ErrorMessage);
                return new UpdateSectionResult { Success = false, ErrorMessage = genResult.ErrorMessage };
            }

            if (genResult.Status == Foundation.Core.Diagnostics.OperationStatus.PartialSuccess)
            {
                _logger.LogWarning(
                    "剖面块 {BlockName} 更新时存在 {DiagnosticCount} 条告警",
                    snapshot.BlockName,
                    genResult.Diagnostics.Count);
            }

            // 3. 删除旧块
            _blockEraseService.EraseBlock(request.BlockHandle);

            sw.Stop();
            _userLogger.SectionUpdated(snapshot.BlockName);
            _logger.LogInformation("剖面块 {OldName} 已更新为 {NewName}，耗时 {ElapsedMs}ms",
                snapshot.BlockName, genResult.BlockName, sw.ElapsedMilliseconds);

            return new UpdateSectionResult { Success = true, NewBlockName = genResult.BlockName };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "更新剖面块 {Handle} 失败", request.BlockHandle);
            return new UpdateSectionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private void EnsureSnapshotCompatibilityFields(
        string blockHandle,
        SectionSnapshot snapshot,
        double geometryAnchorX,
        Vector3D snapshotDirection)
    {
        var shouldSave = false;
        if (!snapshot.GeometryAnchorX.HasValue)
        {
            snapshot.GeometryAnchorX = geometryAnchorX;
            shouldSave = true;
        }

        if (!snapshot.SectionDirection.HasValue || ResolveVector(snapshot.SectionDirection.Value).Length <= 1e-9)
        {
            snapshot.SectionDirection = new Point3D(snapshotDirection.X, snapshotDirection.Y, snapshotDirection.Z);
            shouldSave = true;
        }

        if (shouldSave)
        {
            _snapshotRepo.Save(blockHandle, snapshot);
        }
    }

    private double ResolveGeometryAnchor(string blockHandle, SectionSnapshot snapshot)
    {
        if (snapshot.GeometryAnchorX.HasValue)
        {
            return snapshot.GeometryAnchorX.Value;
        }

        var resolvedAnchor = _sectionGeometryRecoveryService.ResolveBlockGeometryAnchorX(blockHandle);
        return resolvedAnchor ?? 0;
    }

    private static Vector3D ResolveSnapshotDirection(SectionSnapshot snapshot)
    {
        if (snapshot.SectionDirection.HasValue && ResolveVector(snapshot.SectionDirection.Value).Length > 1e-9)
        {
            return ResolveVector(snapshot.SectionDirection.Value);
        }

        return new Vector3D(
            snapshot.CutLineEnd.X - snapshot.CutLineStart.X,
            snapshot.CutLineEnd.Y - snapshot.CutLineStart.Y,
            0);
    }

    private static Vector3D ResolveVector(Point3D direction)
        => new(direction.X, direction.Y, direction.Z);

    private static Line3D AlignToSnapshotDirection(Line3D currentLine, Vector3D snapshotDirection)
    {
        var currentDirection = new Vector3D(
            currentLine.End.X - currentLine.Start.X,
            currentLine.End.Y - currentLine.Start.Y,
            0);

        if (snapshotDirection.Length <= 1e-9 || currentDirection.Length <= 1e-9)
        {
            return currentLine;
        }

        return Vector3D.Dot(snapshotDirection.Normalized, currentDirection.Normalized) < 0
            ? new Line3D(currentLine.End, currentLine.Start)
            : currentLine;
    }
}
