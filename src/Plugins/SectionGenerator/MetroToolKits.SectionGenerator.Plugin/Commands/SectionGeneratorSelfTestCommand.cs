using Autodesk.AutoCAD.ApplicationServices;
using Microsoft.Extensions.Logging;
using MetroToolKits.Bootstrap;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 宿主内自检命令，用于验证 Bootstrap、DI 和关键服务解析是否正常。
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.SectionSelfTest)]
public sealed class SectionGeneratorSelfTestCommand
{
    private readonly IFloorConfigRepository _floorConfigRepository;
    private readonly IGenerateSectionUseCase _generateSectionUseCase;
    private readonly ICheckSectionUpdatesUseCase _checkSectionUpdatesUseCase;
    private readonly IUpdateSectionUseCase _updateSectionUseCase;
    private readonly ILocateSourceElementUseCase _locateSourceElementUseCase;
    private readonly IFindRelatedSectionsUseCase _findRelatedSectionsUseCase;
    private readonly ISectionSnapshotRepository _sectionSnapshotRepository;
    private readonly ISectionReferenceRepository _sectionReferenceRepository;
    private readonly IEntityNavigationService _entityNavigationService;
    private readonly IDrawingService _drawingService;
    private readonly IUserLogger _userLogger;
    private readonly ILogger<SectionGeneratorSelfTestCommand> _logger;

    public SectionGeneratorSelfTestCommand(
        IFloorConfigRepository floorConfigRepository,
        IGenerateSectionUseCase generateSectionUseCase,
        ICheckSectionUpdatesUseCase checkSectionUpdatesUseCase,
        IUpdateSectionUseCase updateSectionUseCase,
        ILocateSourceElementUseCase locateSourceElementUseCase,
        IFindRelatedSectionsUseCase findRelatedSectionsUseCase,
        ISectionSnapshotRepository sectionSnapshotRepository,
        ISectionReferenceRepository sectionReferenceRepository,
        IEntityNavigationService entityNavigationService,
        IDrawingService drawingService,
        IUserLogger userLogger,
        ILogger<SectionGeneratorSelfTestCommand> logger)
    {
        _floorConfigRepository = floorConfigRepository;
        _generateSectionUseCase = generateSectionUseCase;
        _checkSectionUpdatesUseCase = checkSectionUpdatesUseCase;
        _updateSectionUseCase = updateSectionUseCase;
        _locateSourceElementUseCase = locateSourceElementUseCase;
        _findRelatedSectionsUseCase = findRelatedSectionsUseCase;
        _sectionSnapshotRepository = sectionSnapshotRepository;
        _sectionReferenceRepository = sectionReferenceRepository;
        _entityNavigationService = entityNavigationService;
        _drawingService = drawingService;
        _userLogger = userLogger;
        _logger = logger;
    }

    public void Execute()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var ed = doc?.Editor;

        var lines = new[]
        {
            "=== MetroToolKits SectionGenerator Self Test ===",
            "Bootstrap: READY",
            $"UserLogger: {Describe(_userLogger)}",
            $"FloorConfigRepository: {Describe(_floorConfigRepository)}",
            $"GenerateSectionUseCase: {Describe(_generateSectionUseCase)}",
            $"CheckSectionUpdatesUseCase: {Describe(_checkSectionUpdatesUseCase)}",
            $"UpdateSectionUseCase: {Describe(_updateSectionUseCase)}",
            $"LocateSourceElementUseCase: {Describe(_locateSourceElementUseCase)}",
            $"FindRelatedSectionsUseCase: {Describe(_findRelatedSectionsUseCase)}",
            $"SectionSnapshotRepository: {Describe(_sectionSnapshotRepository)}",
            $"SectionReferenceRepository: {Describe(_sectionReferenceRepository)}",
            $"EntityNavigationService: {Describe(_entityNavigationService)}",
            $"DrawingService: {Describe(_drawingService)}",
            "SECTION_SELF_TEST:OK"
        };

        foreach (var line in lines)
            ed?.WriteMessage($"\n{line}");

        _logger.LogInformation("SectionGenerator 宿主自检通过");
    }

    private static string Describe(object service)
        => service.GetType().FullName ?? service.GetType().Name;
}
