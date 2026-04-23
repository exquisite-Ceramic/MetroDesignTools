using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 构件转换/恢复用例。
/// </summary>
public sealed class ElementConversionUseCase : IElementConversionUseCase
{
    private readonly IElementConversionService _conversionService;
    private readonly IElementTypeCatalog _elementTypeCatalog;
    private readonly ILogger<ElementConversionUseCase> _logger;

    public ElementConversionUseCase(
        IElementConversionService conversionService,
        IElementTypeCatalog elementTypeCatalog,
        ILogger<ElementConversionUseCase> logger)
    {
        _conversionService = conversionService;
        _elementTypeCatalog = elementTypeCatalog;
        _logger = logger;
    }

    public ElementConversionSelectionResult DescribeSelection(ElementConversionSelectionRequest request)
    {
        if (request.EntityHandles.Count == 0)
        {
            return new ElementConversionSelectionResult
            {
                Success = false,
                ErrorMessage = "未提供待转换实体。"
            };
        }

        var layers = _conversionService.GetDistinctLayers(request.EntityHandles);
        return new ElementConversionSelectionResult
        {
            Success = true,
            Layers = layers
        };
    }

    public ElementConversionApplyResult ApplyMappings(ElementConversionApplyRequest request)
    {
        if (request.LayerMappings.Count == 0)
        {
            return new ElementConversionApplyResult
            {
                Success = false,
                ErrorMessage = "未提供图层映射。"
            };
        }

        if (!request.ApplyToEntireDrawing && request.EntityHandles.Count == 0)
        {
            return new ElementConversionApplyResult
            {
                Success = false,
                ErrorMessage = "未提供待转换实体。"
            };
        }

        var mappings = new List<ElementLayerMapping>();
        foreach (var assignment in request.LayerMappings)
        {
            var elementType = _elementTypeCatalog.GetByTypeId(assignment.TypeId);
            if (elementType == null)
            {
                return new ElementConversionApplyResult
                {
                    Success = false,
                    ErrorMessage = $"未找到构件类型 {assignment.TypeId}。"
                };
            }

            mappings.Add(new ElementLayerMapping
            {
                SourceLayerName = assignment.SourceLayerName,
                ElementType = elementType
            });
        }

        ElementConversionApplySummary summary = request.ApplyToEntireDrawing
            ? _conversionService.ApplyMappingsToEntireDrawing(mappings)
            : _conversionService.ApplyMappingsToSelection(request.EntityHandles, mappings);

        _logger.LogInformation(
            "构件转换完成，模式 {Mode}，映射图层数 {LayerCount}，转换实体数 {ConvertedCount}",
            request.ApplyToEntireDrawing ? "EntireDrawing" : "Selection",
            summary.MappedLayerCount,
            summary.ConvertedCount);

        return new ElementConversionApplyResult
        {
            Success = true,
            ConvertedCount = summary.ConvertedCount,
            MappedLayerCount = summary.MappedLayerCount
        };
    }

    public ElementConversionRevertResult Revert(ElementConversionRevertRequest request)
    {
        if (request.EntityHandles.Count == 0)
        {
            return new ElementConversionRevertResult
            {
                Success = false,
                ErrorMessage = "未提供待恢复实体。"
            };
        }

        var summary = _conversionService.Revert(request.EntityHandles);
        _logger.LogInformation(
            "构件恢复完成，恢复 {RestoredCount} 个，跳过 {SkippedCount} 个",
            summary.RestoredCount,
            summary.SkippedCount);

        return new ElementConversionRevertResult
        {
            Success = true,
            RestoredCount = summary.RestoredCount,
            SkippedCount = summary.SkippedCount
        };
    }

    public ElementConversionRevertAllResult RevertAll()
    {
        var restoredCount = _conversionService.RevertAll();
        _logger.LogInformation("已恢复全部转换构件，共 {RestoredCount} 个", restoredCount);

        return new ElementConversionRevertAllResult
        {
            Success = true,
            RestoredCount = restoredCount
        };
    }
}
