using MetroToolKits.Foundation.Core.Logging;
using Microsoft.Extensions.Logging;
using MetroToolKits.Bootstrap;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 检测剖面更新状态命令
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.CheckSectionUpdates)]
public sealed class CheckSectionUpdatesCommand
{
    private readonly ICheckSectionUpdatesUseCase _checkUseCase;
    private readonly IUpdateSectionUseCase _updateUseCase;
    private readonly ILogger<CheckSectionUpdatesCommand> _logger;
    private readonly IUserLogger _userLogger;

    public CheckSectionUpdatesCommand(
        ICheckSectionUpdatesUseCase checkUseCase,
        IUpdateSectionUseCase updateUseCase,
        ILogger<CheckSectionUpdatesCommand> logger,
        IUserLogger userLogger)
    {
        _checkUseCase  = checkUseCase;
        _updateUseCase = updateUseCase;
        _logger        = logger;
        _userLogger    = userLogger;
    }

    public void Execute()
    {
        _userLogger.CommandStarted("CheckSectionUpdates");
        _logger.LogInformation("执行 CheckSectionUpdates 命令");

        var dialog = new SectionUpdateDialog(
            _checkUseCase, _updateUseCase,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SectionUpdateDialog>.Instance,
            _userLogger);

        // 非模态显示
        Application.ShowModelessWindow(dialog);
    }
}
