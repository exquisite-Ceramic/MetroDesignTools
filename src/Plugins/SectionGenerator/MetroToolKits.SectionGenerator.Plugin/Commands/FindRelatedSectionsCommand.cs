using Autodesk.AutoCAD.EditorInput;
using MetroToolKits.Bootstrap;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Plugin.UI;
using Microsoft.Extensions.Logging;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 平面构件查询关联剖面命令
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.FindRelatedSections)]
public sealed class FindRelatedSectionsCommand
{
    private readonly IFindRelatedSectionsUseCase _useCase;
    private readonly IEntityNavigationService _navigationService;
    private readonly ILogger<FindRelatedSectionsCommand> _logger;
    private readonly IUserLogger _userLogger;

    public FindRelatedSectionsCommand(
        IFindRelatedSectionsUseCase useCase,
        IEntityNavigationService navigationService,
        ILogger<FindRelatedSectionsCommand> logger,
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

        _userLogger.CommandStarted("FindRelatedSections");

        var result = doc.Editor.GetEntity(new PromptEntityOptions("\n选择平面构件: "));
        if (result.Status != PromptStatus.OK)
        {
            _userLogger.CommandCancelled("FindRelatedSections");
            return;
        }

        var sourceHandle = result.ObjectId.Handle.ToString();
        var queryResult = _useCase.Execute(new FindRelatedSectionsRequest { SourceHandle = sourceHandle });

        if (!queryResult.Success)
        {
            _userLogger.CommandFailed("FindRelatedSections", queryResult.ErrorMessage ?? "查询关联剖面失败");
            return;
        }

        if (queryResult.Sections.Count == 0)
        {
            _userLogger.NoRelatedSectionsFound(sourceHandle);
            return;
        }

        _logger.LogInformation("构件 {Handle} 关联到 {Count} 个剖面", sourceHandle, queryResult.Sections.Count);
        _userLogger.RelatedSectionsFound(sourceHandle, queryResult.Sections.Count);

        var dialog = new RelatedSectionsDialog(sourceHandle, queryResult.Sections, _navigationService);
        Application.ShowModelessWindow(dialog);
    }
}
