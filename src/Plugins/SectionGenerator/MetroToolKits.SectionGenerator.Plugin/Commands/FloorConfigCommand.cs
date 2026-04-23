using MetroToolKits.Foundation.Core.Logging;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Hosting;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 楼层配置管理命令
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.FloorConfig)]
public sealed class FloorConfigCommand
{
    private readonly IFloorConfigUseCase _floorConfigUseCase;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly ILogger<FloorConfigCommand> _logger;
    private readonly IUserLogger _userLogger;

    public FloorConfigCommand(
        IFloorConfigUseCase floorConfigUseCase,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        ILogger<FloorConfigCommand> logger,
        IUserLogger userLogger)
    {
        _floorConfigUseCase = floorConfigUseCase;
        _slabTemplateCatalog = slabTemplateCatalog;
        _logger     = logger;
        _userLogger = userLogger;
    }

    public void Execute()
    {
        _userLogger.CommandStarted("FloorConfig");
        _logger.LogInformation("打开楼层配置窗口");
        FloorConfigDialogWorkflow.Run(_floorConfigUseCase, _slabTemplateCatalog, _userLogger, _logger);
    }
}
