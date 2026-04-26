using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Contracts.Floors;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 楼层配置读写用例实现。
/// </summary>
public sealed class FloorConfigUseCase : IFloorConfigUseCase
{
    private readonly IFloorConfigRepository _repository;
    private readonly IFloorConfigSaveRequestMapper _floorConfigSaveRequestMapper;
    private readonly ISectionOutputConfigMapper _sectionOutputConfigMapper;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly ILogger<FloorConfigUseCase> _logger;

    public FloorConfigUseCase(
        IFloorConfigRepository repository,
        IFloorConfigSaveRequestMapper floorConfigSaveRequestMapper,
        ISectionOutputConfigMapper sectionOutputConfigMapper,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        ILogger<FloorConfigUseCase> logger)
    {
        _repository = repository;
        _floorConfigSaveRequestMapper = floorConfigSaveRequestMapper;
        _sectionOutputConfigMapper = sectionOutputConfigMapper;
        _slabTemplateCatalog = slabTemplateCatalog;
        _logger = logger;
    }

    public LoadedSectionConfig Load()
    {
        var config = _repository.Load();
        _logger.LogDebug("已加载楼层配置，楼层数 {FloorCount}", config.Config.Floors.Count);
        return config;
    }

    public FloorConfigSaveResult Save(SaveFloorConfigDocumentRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var document = _repository.Load();
            _floorConfigSaveRequestMapper.ApplyToDocument(request.FloorConfig, document);
            _sectionOutputConfigMapper.ApplyToDocument(request.OutputConfig, document);

            var diagnostics = FloorConfigSaveValidator.Validate(document.Config, _slabTemplateCatalog)
                .Concat(SectionOutputConfigSaveValidator.Validate(document.OutputConfig))
                .ToArray();

            if (diagnostics.Length > 0)
            {
                var errorMessage = diagnostics[0].Message;
                _logger.LogWarning("楼层配置保存校验失败: {Message}", errorMessage);
                return new FloorConfigSaveResult
                {
                    Success = false,
                    ErrorMessage = errorMessage,
                    Diagnostics = diagnostics
                };
            }

            _repository.Save(document);
            _logger.LogInformation("已保存楼层配置，楼层数 {FloorCount}", document.Config.Floors.Count);
            return new FloorConfigSaveResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存楼层配置时发生异常");
            return new FloorConfigSaveResult
            {
                Success = false,
                ErrorMessage = $"楼层配置保存失败: {ex.Message}"
            };
        }
    }
}
