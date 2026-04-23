using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
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

    public LoadedSectionConfig Load()
    {
        var config = _repository.Load();
        _logger.LogDebug("已加载楼层配置，楼层数 {FloorCount}", config.Config.Floors.Count);
        return config;
    }

    public FloorConfigSaveResult Save(LoadedSectionConfig config)
    {
        var diagnostics = SectionGenerationConfigValidator.ValidateForGeneration(config.Config);
        if (diagnostics.Count > 0)
        {
            var errorMessage = diagnostics[0].Message;
            _logger.LogWarning("楼层配置校验失败: {Message}", errorMessage);
            return new FloorConfigSaveResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Diagnostics = diagnostics
            };
        }

        _repository.Save(config);
        _logger.LogInformation("已保存楼层配置，楼层数 {FloorCount}", config.Config.Floors.Count);
        return new FloorConfigSaveResult { Success = true };
    }
}
