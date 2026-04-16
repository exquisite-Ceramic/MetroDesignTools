using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 剖面生成用例实现（单层）
/// </summary>
public sealed class GenerateSectionUseCase : IGenerateSectionUseCase
{
    private readonly IFloorConfigRepository _configRepo;
    private readonly IElementRecognizer _elementRecognizer;
    private readonly SectionComposer _composer;
    private readonly IDrawingService _drawingService;
    private readonly ILogger<GenerateSectionUseCase> _logger;

    public GenerateSectionUseCase(
        IFloorConfigRepository configRepo,
        IElementRecognizer elementRecognizer,
        SectionComposer composer,
        IDrawingService drawingService,
        ILogger<GenerateSectionUseCase> logger)
    {
        _configRepo        = configRepo;
        _elementRecognizer = elementRecognizer;
        _composer          = composer;
        _drawingService    = drawingService;
        _logger            = logger;
    }

    public GenerateSectionResult Execute(GenerateSectionRequest request)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // 1. 加载楼层配置
            var floorConfig = request.FloorConfig ?? GetDefaultFloorConfig();
            _logger.LogDebug("楼层构件获取，楼层: {FloorName}", floorConfig.Name);

            // 2. 构建剖切线
            var sectionLine  = new Line3D(request.CutLineStart, request.CutLineEnd);
            var viewDirection = ComputeViewDirection(sectionLine);

            // 3. 识别构件
            var elements = _elementRecognizer.RecognizeElements(sectionLine, request.ViewDepth);
            _logger.LogDebug("楼层 {FloorName} 识别到 {ElementCount} 个构件",
                floorConfig.Name, elements.Count);

            if (elements.Count == 0)
                _logger.LogWarning("楼层 {FloorName} 未识别到任何构件", floorConfig.Name);

            // 4. 计算剖面几何
            var composeSw = Stopwatch.StartNew();
            var geometryData = _composer.Generate(sectionLine, viewDirection, elements, floorConfig);
            composeSw.Stop();
            _logger.LogDebug("楼层 {FloorName} 剖切计算耗时 {ElapsedMs}ms",
                floorConfig.Name, composeSw.ElapsedMilliseconds);

            // 5. 绘制块
            var blockName = _drawingService.DrawSectionBlock(
                geometryData, request.InsertionPoint, floorConfig);

            sw.Stop();
            _logger.LogInformation("创建剖面块 {BlockName}，插入点: {InsertionPoint}，总耗时: {ElapsedMs}ms",
                blockName, request.InsertionPoint, sw.ElapsedMilliseconds);

            return new GenerateSectionResult
            {
                Success      = true,
                BlockName    = blockName,
                GeometryData = geometryData
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "剖面生成失败，耗时: {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return new GenerateSectionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private FloorConfig GetDefaultFloorConfig()
    {
        var config = _configRepo.Load();
        return config.Floors.FirstOrDefault() ?? new FloorConfig
        {
            Name = "F1", Height = 5200,
            BottomSlabThickness = 800, TopSlabThickness = 600, FinishThickness = 120
        };
    }

    private static Vector3D ComputeViewDirection(Line3D sectionLine)
    {
        var dir = sectionLine.Direction.Normalized;
        return new Vector3D(-dir.Y, dir.X, 0);
    }
}
