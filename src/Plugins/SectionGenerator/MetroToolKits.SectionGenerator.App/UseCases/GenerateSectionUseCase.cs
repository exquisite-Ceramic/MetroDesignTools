using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 剖面生成用例实现（支持单层和多楼层）
/// </summary>
public sealed class GenerateSectionUseCase : IGenerateSectionUseCase
{
    private readonly IFloorConfigRepository _configRepo;
    private readonly IElementRecognizer _elementRecognizer;
    private readonly MultiFloorSectionComposer _multiComposer;
    private readonly IDrawingService _drawingService;
    private readonly ISectionSnapshotRepository _snapshotRepo;
    private readonly FloorGeometryHasher _hasher;
    private readonly ILogger<GenerateSectionUseCase> _logger;
    private readonly IUserLogger _userLogger;

    public GenerateSectionUseCase(
        IFloorConfigRepository configRepo,
        IElementRecognizer elementRecognizer,
        MultiFloorSectionComposer multiComposer,
        IDrawingService drawingService,
        ISectionSnapshotRepository snapshotRepo,
        FloorGeometryHasher hasher,
        ILogger<GenerateSectionUseCase> logger,
        IUserLogger userLogger)
    {
        _configRepo     = configRepo;
        _elementRecognizer = elementRecognizer;
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
        var diagnostics = new List<OperationDiagnostic>();
        var currentStage = PipelineStage.FloorConfigLoad;

        try
        {
            var config = _configRepo.Load();
            var floors = config.Floors;

            if (floors.Count == 0)
            {
                _logger.LogWarning("未找到楼层配置，使用默认单层");
                _userLogger.FloorConfigMissing();
                diagnostics.Add(SectionGenerationDiagnosticFactory.FloorConfigMissing());
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
            currentStage = PipelineStage.ElementRecognition;
            var totalRecognizedCount = 0;
            for (int i = 0; i < floors.Count; i++)
            {
                var floor = floors[i];
                if (floors.Count > 1)
                    _userLogger.SectionProgress(floor.Name, i + 1, floors.Count);
                else
                    _userLogger.SectionGenerating(floor.Name);

                var alignedSectionLine = FloorSectionLineTransformer.ApplyAlignment(sectionLine, floor);
                var recognition = _elementRecognizer.RecognizeElements(alignedSectionLine, request.ViewDepth);
                floorElements[floor.Name] = recognition.Elements;
                totalRecognizedCount += recognition.Elements.Count;
                diagnostics.AddRange(AddFloorContext(floor.Name, recognition.Diagnostics));

                _logger.LogDebug(
                    "楼层 {FloorName} 扫描 {ScannedCount} 个实体，命中图层 {MatchedCount} 个，相交构件 {IntersectingCount} 个，最终识别 {ElementCount} 个构件",
                    floor.Name,
                    recognition.ScannedEntityCount,
                    recognition.MatchedLayerCount,
                    recognition.IntersectingElementCount,
                    recognition.Elements.Count);

                if (recognition.Elements.Count == 0)
                {
                    diagnostics.Add(SectionGenerationDiagnosticFactory.NoRecognizedElements(
                        "GenerateSectionUseCase",
                        $"楼层 {floor.Name} 未识别到任何可剖切构件",
                        floor.Name));

                    _logger.LogWarning(
                        "楼层 {FloorName} 未识别到任何可剖切构件，命中图层 {MatchedCount} 个，相交构件 {IntersectingCount} 个",
                        floor.Name,
                        recognition.MatchedLayerCount,
                        recognition.IntersectingElementCount);

                    _userLogger.FloorSkipped(floor.Name, "未识别到任何可剖切构件");
                }
            }

            if (totalRecognizedCount == 0)
            {
                return BuildFailedResult(
                    SectionGenerationFailures.NoRecognizedElements(BuildNoRecognizedElementsDetail(diagnostics)),
                    diagnostics,
                    sw);
            }

            // 多楼层堆叠计算
            currentStage = PipelineStage.SectionComposition;
            var composeSw = Stopwatch.StartNew();
            var multiData = _multiComposer.Generate(sectionLine, viewDirection, floorElements, floors);
            composeSw.Stop();
            _logger.LogDebug("多楼层剖切计算耗时 {ElapsedMs}ms，总高度: {TotalHeight:F2}",
                composeSw.ElapsedMilliseconds, multiData.TotalHeight);

            var generatedElementCount = multiData.Floors.Sum(f => f.Elements.Count);
            if (generatedElementCount == 0)
            {
                return BuildFailedResult(
                    SectionGenerationFailures.EmptyGeometry("识别阶段存在结果，但剖面组合后没有生成任何主体构件线。"),
                    diagnostics,
                    sw);
            }

            // 绘制块
            currentStage = PipelineStage.DrawingOutput;
            var drawResult = _drawingService.DrawMultiFloorSectionBlock(
                multiData, request.InsertionPoint, floors);

            // 写入快照（含各楼层哈希）
            currentStage = PipelineStage.SnapshotPersist;
            var snapshot = BuildSnapshot(drawResult.BlockName, request, floors, floorElements, multiData);
            _snapshotRepo.Save(drawResult.BlockHandle, snapshot);
            _logger.LogDebug("写入剖面快照，楼层哈希: {Hashes}",
                string.Join(", ", snapshot.FloorSnapshots.Select(f => $"{f.FloorName}:{f.GeometryHash}")));

            sw.Stop();
            var status = diagnostics.Any(d => d.Level == DiagnosticLevel.Warning || d.Level == DiagnosticLevel.Error)
                ? OperationStatus.PartialSuccess
                : OperationStatus.Success;

            _logger.LogInformation("创建剖面块 {BlockName}，楼层数: {FloorCount}，总耗时: {ElapsedMs}ms",
                drawResult.BlockName, floors.Count, sw.ElapsedMilliseconds);

            return new GenerateSectionResult
            {
                Status      = status,
                Diagnostics = diagnostics,
                BlockName   = drawResult.BlockName,
                BlockHandle = drawResult.BlockHandle,
                FloorCount  = floors.Count,
                TotalHeight = multiData.TotalHeight,
                GeometryData = multiData.Floors.Count == 1 ? multiData.Floors[0] : null
            };
        }
        catch (ToolkitException ex)
        {
            return BuildFailedResult(ex.Failure, diagnostics, sw);
        }
        catch (Exception ex)
        {
            return BuildFailedResult(
                SectionGenerationFailures.Unexpected(nameof(GenerateSectionUseCase), currentStage, ex),
                diagnostics,
                sw);
        }
    }

    private GenerateSectionResult BuildFailedResult(
        OperationFailure failure,
        IReadOnlyList<OperationDiagnostic> diagnostics,
        Stopwatch sw)
    {
        sw.Stop();
        _logger.LogError(
            failure.InnerException,
            "剖面生成失败，Code={Code}，Stage={Stage}，Module={Module}，耗时: {ElapsedMs}ms",
            failure.Code,
            failure.Stage,
            failure.Module,
            sw.ElapsedMilliseconds);

        return new GenerateSectionResult
        {
            Status = OperationStatus.Failed,
            Failure = failure,
            FailedStage = failure.Stage,
            Diagnostics = diagnostics
        };
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
            var floorGeometry = multiData.Floors.FirstOrDefault(x => x.FloorName == f.Name);
            var sourceHandles = floorGeometry == null
                ? new List<string>()
                : floorGeometry.Elements
                    .Select(e => e.SourceHandle)
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            return new FloorSnapshot
            {
                FloorName    = f.Name,
                GeometryHash = _hasher.ComputeHash(elements),
                ElementCount = elements.Count,
                SourceElementHandles = sourceHandles
            };
        }).ToList();

        return new SectionSnapshot
        {
            BlockName             = blockName,
            SourceCutLineHandle   = request.CutLineHandle,
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

    private static IReadOnlyList<OperationDiagnostic> AddFloorContext(
        string floorName,
        IReadOnlyList<OperationDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
            return Array.Empty<OperationDiagnostic>();

        return diagnostics.Select(diagnostic =>
        {
            var metadata = new Dictionary<string, string?>(diagnostic.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["floor"] = floorName
            };

            return diagnostic with
            {
                Message = $"[{floorName}] {diagnostic.Message}",
                Metadata = metadata
            };
        }).ToArray();
    }

    private static string BuildNoRecognizedElementsDetail(IReadOnlyList<OperationDiagnostic> diagnostics)
    {
        var unsupportedTypeCount = diagnostics.Count(d => d.Code == SectionGenerationErrorCodes.UnsupportedEntityType);
        var noIntersectionCount = diagnostics.Count(d => d.Code == SectionGenerationErrorCodes.NoIntersectingElements);
        var conversionFailedCount = diagnostics.Count(d => d.Code == SectionGenerationErrorCodes.ConversionFailed);

        if (unsupportedTypeCount == 0 && noIntersectionCount == 0 && conversionFailedCount == 0)
        {
            return "扫描完成，但没有实体命中受支持的图层前缀。当前仅支持 MK_结构墙/柱/楼板。";
        }

        return $"识别阶段未产出可用构件。UnsupportedEntityType={unsupportedTypeCount}, NoIntersectingElements={noIntersectionCount}, ConversionFailed={conversionFailedCount}";
    }
}
