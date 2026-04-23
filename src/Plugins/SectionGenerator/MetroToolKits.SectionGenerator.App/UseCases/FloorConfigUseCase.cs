using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.Foundation.Core.Diagnostics;

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
        var diagnostics = Validate(config.Config);
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

    private static IReadOnlyList<OperationDiagnostic> Validate(SectionConfig config)
    {
        var diagnostics = new List<OperationDiagnostic>();
        if (config.Floors.Count <= 1)
        {
            return diagnostics;
        }

        var baseFloor = config.Floors.FirstOrDefault(floor =>
            string.Equals(floor.Name, config.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase));
        if (baseFloor == null)
        {
            diagnostics.Add(CreateValidationDiagnostic("多楼层模式下必须选择基准层。"));
            return diagnostics;
        }

        if (!FloorSectionLineTransformer.TryValidateAlignmentPoints(baseFloor.AlignmentPoints, out var baseError))
        {
            diagnostics.Add(CreateValidationDiagnostic($"基准层 {baseFloor.Name} 的对齐点无效: {baseError}"));
            return diagnostics;
        }

        if (!baseFloor.ScopeBounds.HasValue)
        {
            diagnostics.Add(CreateValidationDiagnostic($"基准层 {baseFloor.Name} 缺少整层范围框。"));
            return diagnostics;
        }

        if (!baseFloor.ScopeBounds.Value.IsValid())
        {
            diagnostics.Add(CreateValidationDiagnostic($"基准层 {baseFloor.Name} 的整层范围框无效。"));
        }

        return diagnostics;
    }

    private static OperationDiagnostic CreateValidationDiagnostic(string message)
        => new()
        {
            Level = DiagnosticLevel.Error,
            Code = "SectionGenerator.FloorConfig.ValidationFailed",
            Stage = PipelineStage.FloorConfigLoad,
            Module = nameof(FloorConfigUseCase),
            Message = message,
            Suggestion = "请先在楼层配置中补齐基准层、基准点和整层范围。"
        };
}
