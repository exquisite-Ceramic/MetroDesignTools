using System.Diagnostics;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Core.Hosting;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.SectionGenerator.Plugin.Selection;
using MetroToolKits.SectionGenerator.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 生成剖面图命令
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.GenSection)]
public sealed class GenSectionCommand
{
    private readonly IGenerateSectionUseCase _useCase;
    private readonly IGenerateSectionPreflightUseCase _preflightUseCase;
    private readonly IFloorConfigUseCase _floorConfigUseCase;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly IFloorConfigDocumentAssembler _floorConfigDocumentAssembler;
    private readonly IFloorConfigSaveRequestMapper _floorConfigSaveRequestMapper;
    private readonly ISectionOutputConfigMapper _sectionOutputConfigMapper;
    private readonly ILayerMappingWorkspaceAssembler _layerMappingWorkspaceAssembler;
    private readonly ILayerMappingApplyRequestMapper _layerMappingApplyRequestMapper;
    private readonly IWallAssemblyTemplateCatalog _wallTemplateCatalog;
    private readonly IElementConversionUseCase _elementConversionUseCase;
    private readonly ICheckSectionUpdatesUseCase _checkUseCase;
    private readonly IUpdateSectionUseCase _updateUseCase;
    private readonly ILogger<GenSectionCommand> _logger;
    private readonly IUserLogger _userLogger;
    private readonly OperationFeedbackPresenter _feedbackPresenter;

    public GenSectionCommand(
        IGenerateSectionUseCase useCase,
        IGenerateSectionPreflightUseCase preflightUseCase,
        IFloorConfigUseCase floorConfigUseCase,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IFloorConfigDocumentAssembler floorConfigDocumentAssembler,
        IFloorConfigSaveRequestMapper floorConfigSaveRequestMapper,
        ISectionOutputConfigMapper sectionOutputConfigMapper,
        ILayerMappingWorkspaceAssembler layerMappingWorkspaceAssembler,
        ILayerMappingApplyRequestMapper layerMappingApplyRequestMapper,
        IWallAssemblyTemplateCatalog wallTemplateCatalog,
        IElementConversionUseCase elementConversionUseCase,
        ICheckSectionUpdatesUseCase checkUseCase,
        IUpdateSectionUseCase updateUseCase,
        ILogger<GenSectionCommand> logger,
        IUserLogger userLogger,
        OperationFeedbackPresenter feedbackPresenter)
    {
        _useCase = useCase;
        _preflightUseCase = preflightUseCase;
        _floorConfigUseCase = floorConfigUseCase;
        _slabTemplateCatalog = slabTemplateCatalog;
        _floorConfigDocumentAssembler = floorConfigDocumentAssembler;
        _floorConfigSaveRequestMapper = floorConfigSaveRequestMapper;
        _sectionOutputConfigMapper = sectionOutputConfigMapper;
        _layerMappingWorkspaceAssembler = layerMappingWorkspaceAssembler;
        _layerMappingApplyRequestMapper = layerMappingApplyRequestMapper;
        _wallTemplateCatalog = wallTemplateCatalog;
        _elementConversionUseCase = elementConversionUseCase;
        _checkUseCase = checkUseCase;
        _updateUseCase = updateUseCase;
        _logger = logger;
        _userLogger = userLogger;
        _feedbackPresenter = feedbackPresenter;
    }

    public void Execute()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        _userLogger.CommandStarted("GenSection");
        _logger.LogInformation("执行 GenSection 向导");

        var state = new GenerateSectionWizardState();

        while (true)
        {
            state.PreflightResult = _preflightUseCase.Execute();
            state.Normalize();

            var window = new GenerateSectionWizardWindow(
                state,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<GenerateSectionWizardWindow>.Instance);

            Application.ShowModalWindow(window);
            var action = window.Tag is GenerateSectionWizardAction wizardAction
                ? wizardAction
                : GenerateSectionWizardAction.None;

            switch (action)
            {
                case GenerateSectionWizardAction.None:
                    _userLogger.CommandCancelled("GenSection");
                    return;

                case GenerateSectionWizardAction.OpenFloorConfig:
                    FloorConfigDialogWorkflow.Run(
                        _floorConfigUseCase,
                        _slabTemplateCatalog,
                        _floorConfigDocumentAssembler,
                        _floorConfigSaveRequestMapper,
                        _sectionOutputConfigMapper,
                        _userLogger,
                        _logger);
                    continue;

                case GenerateSectionWizardAction.OpenLayerMapping:
                    OpenLayerMappingWindow();
                    continue;

                case GenerateSectionWizardAction.ViewUpdateDetails:
                    ShowUpdateDetails(state.PreflightResult);
                    continue;

                case GenerateSectionWizardAction.PickCutLine:
                    TryPickCutLine(doc, state);
                    continue;

                case GenerateSectionWizardAction.PickLocalScope:
                    TryPickLocalScope(doc, state);
                    continue;

                case GenerateSectionWizardAction.PickInsertionPoint:
                    TryPickInsertionPoint(doc, state);
                    continue;

                case GenerateSectionWizardAction.Generate:
                    if (TryGenerate(state))
                    {
                        return;
                    }

                    state.CurrentStep = 3;
                    continue;
            }
        }
    }

    private void ShowUpdateDetails(GenerateSectionPreflightResult preflightResult)
    {
        var checkResult = preflightResult.ExistingSections.CheckResult;
        if (checkResult == null)
        {
            return;
        }

        var dialog = new SectionUpdateDialog(
            checkResult,
            _checkUseCase,
            _updateUseCase,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SectionUpdateDialog>.Instance,
            _userLogger);
        Application.ShowModelessWindow(dialog);
    }

    private void OpenLayerMappingWindow()
    {
        var window = new LayerMappingManager(
            _layerMappingWorkspaceAssembler,
            _layerMappingApplyRequestMapper,
            _wallTemplateCatalog,
            _slabTemplateCatalog,
            _elementConversionUseCase);
        Application.ShowModalWindow(window);
    }

    private bool TryPickCutLine(Autodesk.AutoCAD.ApplicationServices.Document document, GenerateSectionWizardState state)
    {
        var editor = document.Editor;
        var lineResult = editor.GetEntity(new PromptEntityOptions("\n选择剖切线（直线）: ")
        {
            AllowNone = false
        });

        if (lineResult.Status != PromptStatus.OK)
        {
            return false;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var entity = transaction.GetObject(lineResult.ObjectId, OpenMode.ForRead);
        if (entity is not Line cadLine)
        {
            _feedbackPresenter.PresentFailure(
                "GenSection",
                SectionGenerationFailures.InvalidSectionLine(
                    $"所选对象类型为 {entity?.GetType().Name ?? "Unknown"}。"));
            transaction.Abort();
            return false;
        }

        state.CutLineHandle = lineResult.ObjectId.Handle.ToString();
        state.CutLineStart = new Point3D(cadLine.StartPoint.X, cadLine.StartPoint.Y, cadLine.StartPoint.Z);
        state.CutLineEnd = new Point3D(cadLine.EndPoint.X, cadLine.EndPoint.Y, cadLine.EndPoint.Z);
        transaction.Commit();

        _logger.LogDebug("向导已选剖切线 {Handle}", state.CutLineHandle);
        return true;
    }

    private bool TryPickLocalScope(
        Autodesk.AutoCAD.ApplicationServices.Document document,
        GenerateSectionWizardState state)
    {
        if (!state.UseLocalScope)
        {
            state.LocalScopeBounds = null;
            return false;
        }

        var localScopeFloorName = state.ResolveLocalScopeFloorName();
        var selectionOptions = new PromptSelectionOptions
        {
            MessageForAdding = string.IsNullOrWhiteSpace(localScopeFloorName)
                ? "\n选择本次局部区域图元: "
                : $"\n选择楼层 [{localScopeFloorName}] 的本次局部区域图元: ",
            AllowDuplicates = false
        };

        if (!SelectionScopeBoundsService.TryPickBounds(
                document,
                selectionOptions,
                out var bounds,
                out var cancelled,
                out var errorMessage))
        {
            if (!cancelled && !string.IsNullOrWhiteSpace(errorMessage))
            {
                _feedbackPresenter.PresentFailure(
                    "GenSection",
                    SectionGenerationFailures.LocalScopeInvalid(errorMessage));
            }

            return false;
        }

        state.LocalScopeBounds = bounds;
        return true;
    }

    private bool TryPickInsertionPoint(
        Autodesk.AutoCAD.ApplicationServices.Document document,
        GenerateSectionWizardState state)
    {
        var pointResult = document.Editor.GetPoint(new PromptPointOptions("\n指定剖面图插入点: "));
        if (pointResult.Status != PromptStatus.OK)
        {
            return false;
        }

        state.InsertionPoint = new Point3D(pointResult.Value.X, pointResult.Value.Y, pointResult.Value.Z);
        state.HasInsertionPoint = true;
        return true;
    }

    private bool TryGenerate(GenerateSectionWizardState state)
    {
        if (!state.CanGenerate)
        {
            return false;
        }

        _userLogger.SectionGenerating("（加载楼层配置中...）");
        var stopwatch = Stopwatch.StartNew();

        var result = _useCase.Execute(new GenerateSectionRequest
        {
            CutLineHandle = state.CutLineHandle ?? string.Empty,
            CutLineStart = state.CutLineStart,
            CutLineEnd = state.CutLineEnd,
            ViewDepth = state.ViewDepth,
            InsertionPoint = state.InsertionPoint,
            TargetFloorName = state.ResolveTargetFloorName(),
            LocalScopeBounds = state.UseLocalScope ? state.LocalScopeBounds : null,
            LocalScopeFloorName = state.ResolveLocalScopeFloorName()
        });

        stopwatch.Stop();

        if (result.Status == OperationStatus.Failed)
        {
            var hasIntersectionWarning = result.Diagnostics.Any(d =>
                d.Code == SectionGenerationErrorCodes.NoIntersectingElements);

            _feedbackPresenter.PresentFailure(
                "GenSection",
                result.Failure!,
                result.Diagnostics,
                useSectionLineInvalid: hasIntersectionWarning);
            return false;
        }

        _userLogger.SectionCreated(result.BlockName!, result.FloorCount, stopwatch.ElapsedMilliseconds);

        if (result.Status == OperationStatus.PartialSuccess)
        {
            var warningCount = result.Diagnostics.Count(d =>
                d.Level == DiagnosticLevel.Warning || d.Level == DiagnosticLevel.Error);

            _feedbackPresenter.PresentPartialSuccess(
                "GenSection",
                $"剖面已生成，但存在 {warningCount} 条告警，详见日志",
                result.Diagnostics);
        }
        else
        {
            _feedbackPresenter.LogDiagnostics("GenSection", result.Diagnostics);
        }

        _logger.LogInformation(
            "剖面生成完成，状态: {Status}，块名称: {BlockName}，楼层数: {FloorCount}，耗时: {ElapsedMs}ms",
            result.Status,
            result.BlockName,
            result.FloorCount,
            stopwatch.ElapsedMilliseconds);

        return true;
    }
}
