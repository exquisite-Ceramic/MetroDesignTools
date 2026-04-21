using MetroToolKits.Foundation.Core.Logging;
using Microsoft.Extensions.Logging;
using MetroToolKits.Bootstrap;
using MetroToolKits.Bootstrap.Logging;
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
    private readonly OperationFeedbackPresenter _feedbackPresenter;

    public CheckSectionUpdatesCommand(
        ICheckSectionUpdatesUseCase checkUseCase,
        IUpdateSectionUseCase updateUseCase,
        ILogger<CheckSectionUpdatesCommand> logger,
        IUserLogger userLogger,
        OperationFeedbackPresenter feedbackPresenter)
    {
        _checkUseCase  = checkUseCase;
        _updateUseCase = updateUseCase;
        _logger        = logger;
        _userLogger    = userLogger;
        _feedbackPresenter = feedbackPresenter;
    }

    public void Execute()
    {
        _userLogger.CommandStarted("CheckSectionUpdates");
        _logger.LogInformation("执行 CheckSectionUpdates 命令");

        var result = _checkUseCase.Execute();
        if (result.Status == Foundation.Core.Diagnostics.OperationStatus.Failed)
        {
            _feedbackPresenter.PresentFailure(
                "CheckSectionUpdates",
                result.Failure!,
                result.Diagnostics);
            return;
        }

        var dialog = new SectionUpdateDialog(
            result,
            _checkUseCase,
            _updateUseCase,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SectionUpdateDialog>.Instance,
            _userLogger);

        // 非模态显示
        Application.ShowModelessWindow(dialog);
    }
}
