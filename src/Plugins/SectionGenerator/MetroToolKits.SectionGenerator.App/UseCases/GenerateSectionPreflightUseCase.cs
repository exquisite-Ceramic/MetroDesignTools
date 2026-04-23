using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

public sealed class GenerateSectionPreflightUseCase : IGenerateSectionPreflightUseCase
{
    private readonly IFloorConfigRepository _floorConfigRepository;
    private readonly ICheckSectionUpdatesUseCase _checkSectionUpdatesUseCase;
    private readonly ILogger<GenerateSectionPreflightUseCase> _logger;

    public GenerateSectionPreflightUseCase(
        IFloorConfigRepository floorConfigRepository,
        ICheckSectionUpdatesUseCase checkSectionUpdatesUseCase,
        ILogger<GenerateSectionPreflightUseCase> logger)
    {
        _floorConfigRepository = floorConfigRepository;
        _checkSectionUpdatesUseCase = checkSectionUpdatesUseCase;
        _logger = logger;
    }

    public GenerateSectionPreflightResult Execute()
    {
        var configDocument = _floorConfigRepository.Load();
        var missingRequirements = SectionGenerationConfigValidator.ValidateForGeneration(configDocument.Config);
        var existingSections = BuildExistingSectionSummary();

        _logger.LogDebug(
            "生成预检完成，楼层数: {FloorCount}，缺失项: {MissingCount}，现有剖面数: {SectionCount}",
            configDocument.Config.Floors.Count,
            missingRequirements.Count,
            existingSections.TotalCount);

        return new GenerateSectionPreflightResult
        {
            ConfigDocument = configDocument,
            MissingRequirements = missingRequirements,
            ExistingSections = existingSections
        };
    }

    private ExistingSectionStatusSummary BuildExistingSectionSummary()
    {
        var checkResult = _checkSectionUpdatesUseCase.Execute();
        if (checkResult.Status == Foundation.Core.Diagnostics.OperationStatus.Failed)
        {
            return new ExistingSectionStatusSummary
            {
                ErrorMessage = checkResult.Failure?.UserMessage ?? "无法检查当前图纸中的剖面更新状态。"
            };
        }

        return new ExistingSectionStatusSummary
        {
            TotalCount = checkResult.Items.Count,
            UpToDateCount = checkResult.Items.Count(item => item.Status == SectionUpdateStatus.UpToDate),
            OutdatedCount = checkResult.Items.Count(item => item.Status == SectionUpdateStatus.Outdated),
            UnknownCount = checkResult.Items.Count(item => item.Status == SectionUpdateStatus.Unknown),
            PartialCount = checkResult.Items.Count(item => item.SkippedFloors.Count > 0),
            CheckResult = checkResult
        };
    }
}
