using Autodesk.AutoCAD.EditorInput;
using MetroToolKits.Bootstrap;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using Microsoft.Extensions.Logging;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 剖面实体定位源构件命令
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.LocateSourceElement)]
public sealed class LocateSourceElementCommand
{
    private readonly ILocateSourceElementUseCase _useCase;
    private readonly IEntityNavigationService _navigationService;
    private readonly ILogger<LocateSourceElementCommand> _logger;
    private readonly IUserLogger _userLogger;

    public LocateSourceElementCommand(
        ILocateSourceElementUseCase useCase,
        IEntityNavigationService navigationService,
        ILogger<LocateSourceElementCommand> logger,
        IUserLogger userLogger)
    {
        _useCase = useCase;
        _navigationService = navigationService;
        _logger = logger;
        _userLogger = userLogger;
    }

    public void Execute()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        _userLogger.CommandStarted("LocateSourceElement");

        var result = doc.Editor.GetNestedEntity(
            new PromptNestedEntityOptions("\n选择剖面块内的剖面实体: "));

        if (result.Status != PromptStatus.OK)
        {
            _userLogger.CommandCancelled("LocateSourceElement");
            return;
        }

        var locateResult = _useCase.Execute(new LocateSourceElementRequest
        {
            SectionEntityHandle = result.ObjectId.Handle.ToString()
        });

        if (!locateResult.Success)
        {
            _userLogger.SourceElementLocationFailed(locateResult.ErrorMessage ?? "未找到源构件引用");
            return;
        }

        var navigationResult = _navigationService.FocusEntity(locateResult.SourceHandle);
        if (!navigationResult.Success)
        {
            _logger.LogWarning("定位源构件失败，剖面实体 {SectionHandle}，源句柄 {SourceHandle}，原因: {Reason}",
                result.ObjectId.Handle, locateResult.SourceHandle, navigationResult.ErrorMessage);
            _userLogger.SourceElementLocationFailed(navigationResult.ErrorMessage ?? "源构件定位失败");
            return;
        }

        _userLogger.SourceElementLocated(locateResult.ElementType, locateResult.SourceHandle);
    }
}
