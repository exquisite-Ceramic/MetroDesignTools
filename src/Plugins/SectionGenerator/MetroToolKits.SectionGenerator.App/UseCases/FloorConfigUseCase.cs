using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 楼层配置读写用例实现。
/// </summary>
public sealed class FloorConfigUseCase : IFloorConfigUseCase
{
    private readonly IFloorConfigRepository _repository;
    private readonly ILogger<FloorConfigUseCase> _logger;

    public FloorConfigUseCase(
        IFloorConfigRepository repository,
        ILogger<FloorConfigUseCase> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public SectionConfig Load()
    {
        var config = _repository.Load();
        _logger.LogDebug("已加载楼层配置，楼层数 {FloorCount}", config.Floors.Count);
        return config;
    }

    public void Save(SectionConfig config)
    {
        _repository.Save(config);
        _logger.LogInformation("已保存楼层配置，楼层数 {FloorCount}", config.Floors.Count);
    }
}
