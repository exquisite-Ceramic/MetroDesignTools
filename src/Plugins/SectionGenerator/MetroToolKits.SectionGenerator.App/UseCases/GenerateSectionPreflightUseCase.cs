using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

public sealed class GenerateSectionPreflightUseCase : IGenerateSectionPreflightUseCase
{
    private readonly IFloorConfigRepository _floorConfigRepository;
    private readonly ICheckSectionUpdatesUseCase _checkSectionUpdatesUseCase;
    private readonly IGenerationReadinessInspector _generationReadinessInspector;
    private readonly ILogger<GenerateSectionPreflightUseCase> _logger;

    public GenerateSectionPreflightUseCase(
        IFloorConfigRepository floorConfigRepository,
        ICheckSectionUpdatesUseCase checkSectionUpdatesUseCase,
        IGenerationReadinessInspector generationReadinessInspector,
        ILogger<GenerateSectionPreflightUseCase> logger)
    {
        _floorConfigRepository = floorConfigRepository;
        _checkSectionUpdatesUseCase = checkSectionUpdatesUseCase;
        _generationReadinessInspector = generationReadinessInspector;
        _logger = logger;
    }

    public GenerateSectionPreflightResult Execute()
    {
        var configDocument = NormalizeLoadedConfig(_floorConfigRepository.Load());
        var missingRequirements = SectionGenerationConfigValidator.ValidateForGeneration(configDocument.Config);
        var existingSections = BuildExistingSectionSummary();
        var readiness = _generationReadinessInspector.Inspect() ?? new GenerationReadinessSummary();

        _logger.LogDebug(
            "生成预检完成，楼层数: {FloorCount}，缺失项: {MissingCount}，现有剖面数: {SectionCount}，可识别构件数: {ElementCount}",
            configDocument.Config.Floors.Count,
            missingRequirements.Count,
            existingSections.TotalCount,
            readiness.TotalRecognizableElementCount);

        return new GenerateSectionPreflightResult
        {
            ConfigDocument = configDocument,
            MissingRequirements = missingRequirements,
            ExistingSections = existingSections,
            Readiness = readiness
        };
    }

    private static LoadedSectionConfig NormalizeLoadedConfig(LoadedSectionConfig? configDocument)
    {
        configDocument ??= new LoadedSectionConfig();
        configDocument.Config ??= new SectionConfig();
        configDocument.Config.Floors ??= new List<FloorConfig>();
        configDocument.OutputConfig ??= new SectionOutputConfig();
        configDocument.RuntimeDiagnostics ??= new List<OperationDiagnostic>();
        configDocument.RuntimeState ??= new SectionConfigRuntimeState();
        return configDocument;
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
