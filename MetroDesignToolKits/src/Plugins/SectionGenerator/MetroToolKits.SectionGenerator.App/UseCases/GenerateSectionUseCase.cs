using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 剖面生成用例实现（支持单层和多楼层）
/// </summary>
public sealed class GenerateSectionUseCase : IGenerateSectionUseCase
{
    private readonly IFloorConfigRepository _configRepo;
    private readonly IElementRecognizer _elementRecognizer;
    private readonly SectionComposer _composer;
    private readonly MultiFloorSectionComposer _multiComposer;
    private readonly IDrawingService _drawingService;
    private readonly ISectionSnapshotRepository _snapshotRepo;
    private readonly FloorGeometryHasher _hasher;
    private readonly ILogger<GenerateSectionUseCase> _logger;
    private readonly IUserLogger _userLogger;

    public GenerateSectionUseCase(
        IFloorConfigRepository configRepo,
        IElementRecognizer elementRecognizer,
        SectionComposer composer,
        MultiFloorSectionComposer multiComposer,
        IDrawingService drawingService,
        ISectionSnapshotRepository snapshotRepo,
        FloorGeometryHasher hasher,
        ILogger<GenerateSectionUseCase> logger,
        IUserLogger userLogger)
    {
        _configRepo     = configRepo;
        _elementRecognizer = elementRecognizer;
        _composer       = composer;
        _multiComposer  = multiComposer;
        _drawingService = drawingService;
        _snapshotRepo   = snapshotRepo;
        _hasher         = hasher;
        _logger         = logger;
        _userLogger     = userLogger;
    }

    public GenerateSectionResult Execute(GenerateSectionRequest request)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var config = _configRepo.Load();
            var floors = config.Floors;

            if (floors.Count == 0)
            {
                _logger.LogWarning("未找到楼层配置，使用默认单层");
                _userLogger.FloorConfigMissing();
                floors = new List<FloorConfig>
                {
                    new() { Name = "F1", Height = 5200, BottomSlabThickness = 800,
                            TopSlabThickness = 600, FinishThickness = 120 }
                };
            }

            var sectionLine   = new Line3D(request.CutLineStart, request.CutLineEnd);
            var viewDirection = ComputeViewDirection(sectionLine);

            _logger.LogInformation("执行 GenSection 命令，楼层数: {FloorCount}，剖切线长度: {Length:F2}",
                floors.Count, sectionLine.Length);

            // 逐层识别构件，同时输出进度
            var floorElements = new Dictionary<string, IReadOnlyList<BuildingElement>>();
            for (int i = 0; i < floors.Count; i++)
            {
                var floor = floors[i];
                if (floors.Count > 1)
                    _userLogger.SectionProgress(floor.Name, i + 1, floors.Count);
                else
                    _userLogger.SectionGenerating(floor.Name);

                var elements = _elementRecognizer.RecognizeElements(sectionLine, request.ViewDepth);
                floorElements[floor.Name] = elements;
                _logger.LogDebug("楼层 {FloorName} 识别到 {Count} 个构件", floor.Name, elements.Count);

                if (elements.Count == 0)
                {
                    _logger.LogWarning("楼层 {FloorName} 未识别到任何构件", floor.Name);
                    _userLogger.FloorSkipped(floor.Name, "未识别到任何构件");
                }
            }

            // 多楼层堆叠计算
            var composeSw = Stopwatch.StartNew();
            var multiData = _multiComposer.Generate(sectionLine, viewDirection, floorElements, floors);
            composeSw.Stop();
            _logger.LogDebug("多楼层剖切计算耗时 {ElapsedMs}ms，总高度: {TotalHeight:F2}",
                composeSw.ElapsedMilliseconds, multiData.TotalHeight);

            // 绘制块
            var drawResult = _drawingService.DrawMultiFloorSectionBlock(
                multiData, request.InsertionPoint, floors);

            // 写入快照（含各楼层哈希）。注意：快照必须写到块参照 Handle 上，不能写到块定义名称上。
            var snapshot = BuildSnapshot(drawResult.BlockName, request, floors, floorElements, multiData);
            _snapshotRepo.Save(drawResult.BlockHandle, snapshot);
            _logger.LogDebug("写入剖面快照，块句柄: {BlockHandle}，楼层哈希: {Hashes}",
                drawResult.BlockHandle,
                string.Join(", ", snapshot.FloorSnapshots.Select(f => $"{f.FloorName}:{f.GeometryHash}")));

            sw.Stop();
            _logger.LogInformation("创建剖面块 {BlockName}，句柄 {BlockHandle}，楼层数: {FloorCount}，总耗时: {ElapsedMs}ms",
                drawResult.BlockName, drawResult.BlockHandle, floors.Count, sw.ElapsedMilliseconds);

            return new GenerateSectionResult
            {
                Success     = true,
                BlockName   = drawResult.BlockName,
                BlockHandle = drawResult.BlockHandle,
                FloorCount  = floors.Count,
                TotalHeight = multiData.TotalHeight
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "剖面生成失败，耗时: {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return new GenerateSectionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private SectionSnapshot BuildSnapshot(
        string blockName,
        GenerateSectionRequest request,
        IReadOnlyList<FloorConfig> floors,
        Dictionary<string, IReadOnlyList<BuildingElement>> floorElements,
        MultiFloorSectionData multiData)
    {
        var floorSnapshots = floors.Select(f =>
        {
            var elements = floorElements.TryGetValue(f.Name, out var list) ? list : Array.Empty<BuildingElement>();
            return new FloorSnapshot
            {
                FloorName    = f.Name,
                GeometryHash = _hasher.ComputeHash(elements),
                ElementCount = elements.Count
            };
        }).ToList();

        return new SectionSnapshot
        {
            BlockName             = blockName,
            SourceCutLineHandle   = request.SourceCutLineHandle ?? string.Empty,
            CutLineStart          = request.CutLineStart,
            CutLineEnd            = request.CutLineEnd,
            InsertionPoint        = request.InsertionPoint,
            ViewDepth             = request.ViewDepth,
            TotalHeight           = multiData.TotalHeight,
            FloorSnapshots        = floorSnapshots
        };
    }

    private static Vector3D ComputeViewDirection(Line3D sectionLine)
    {
        var dir = sectionLine.Direction.Normalized;
        return new Vector3D(-dir.Y, dir.X, 0);
    }
}
