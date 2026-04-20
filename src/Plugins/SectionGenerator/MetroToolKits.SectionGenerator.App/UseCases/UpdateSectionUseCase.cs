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
    private readonly IGenerateSectionUseCase _generateUseCase;
    private readonly IBlockEraseService _blockEraseService;
    private readonly ILogger<UpdateSectionUseCase> _logger;
    private readonly IUserLogger _userLogger;

    public UpdateSectionUseCase(
        ISectionSnapshotRepository snapshotRepo,
        IGenerateSectionUseCase generateUseCase,
        IBlockEraseService blockEraseService,
        ILogger<UpdateSectionUseCase> logger,
        IUserLogger userLogger)
    {
        _snapshotRepo     = snapshotRepo;
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

            // 2. 重新生成（使用快照中记录的插入点）
            var genResult = _generateUseCase.Execute(new GenerateSectionRequest
            {
                CutLineHandle  = snapshot.SourceCutLineHandle,
                CutLineStart   = snapshot.CutLineStart,
                CutLineEnd     = snapshot.CutLineEnd,
                InsertionPoint = snapshot.InsertionPoint,
                ViewDepth      = snapshot.ViewDepth
            });

            if (!genResult.Success)
            {
                _logger.LogError("重新生成剖面失败: {Error}", genResult.ErrorMessage);
                return new UpdateSectionResult { Success = false, ErrorMessage = genResult.ErrorMessage };
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
}
