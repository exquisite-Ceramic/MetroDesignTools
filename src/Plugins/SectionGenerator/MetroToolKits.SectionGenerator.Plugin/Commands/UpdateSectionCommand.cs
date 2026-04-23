using MetroToolKits.Foundation.Core.Logging;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Hosting;
using MetroToolKits.SectionGenerator.App.UseCases;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 更新单个剖面命令
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.UpdateSection)]
public sealed class UpdateSectionCommand
{
    private readonly IUpdateSectionUseCase _updateUseCase;
    private readonly ILogger<UpdateSectionCommand> _logger;
    private readonly IUserLogger _userLogger;

    public UpdateSectionCommand(
        IUpdateSectionUseCase updateUseCase,
        ILogger<UpdateSectionCommand> logger,
        IUserLogger userLogger)
    {
        _updateUseCase = updateUseCase;
        _logger        = logger;
        _userLogger    = userLogger;
    }

    public void Execute()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var ed = doc.Editor;

        _userLogger.CommandStarted("UpdateSection");
        _logger.LogInformation("执行 UpdateSection 命令");

        // 选择剖面块
        var selResult = ed.GetEntity(new PromptEntityOptions("\n选择要更新的剖面块: ")
        {
            AllowNone = false
        });

        if (selResult.Status != PromptStatus.OK)
        {
            _userLogger.CommandCancelled("UpdateSection");
            return;
        }

        var handle = selResult.ObjectId.Handle.ToString();
        _logger.LogDebug("选择剖面块，句柄: {Handle}", handle);

        var result = _updateUseCase.Execute(new UpdateSectionRequest { BlockHandle = handle });

        if (result.Success)
        {
            _userLogger.SectionUpdated(result.NewBlockName ?? handle);
            _logger.LogInformation("剖面更新成功，新块名: {NewBlockName}", result.NewBlockName);
        }
        else
        {
            _userLogger.SectionUpdateFailed(handle, result.ErrorMessage ?? "未知错误");
            _logger.LogError("剖面更新失败: {Error}", result.ErrorMessage);
        }
    }
}
