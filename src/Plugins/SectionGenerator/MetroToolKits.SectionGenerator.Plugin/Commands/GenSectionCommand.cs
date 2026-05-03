using System.Diagnostics;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Core.Hosting;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.SectionGenerator.Plugin.Selection;
using MetroToolKits.SectionGenerator.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

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
    private readonly IConvertedElementMetadataInspector _metadataInspector;
    private readonly IConvertedElementMetadataRepairService _metadataRepairService;
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
        IConvertedElementMetadataInspector metadataInspector,
        IConvertedElementMetadataRepairService metadataRepairService,
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
        _metadataInspector = metadataInspector;
        _metadataRepairService = metadataRepairService;
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

        if (!EnsureWallTemplateMetadataBeforeGenerate())
        {
            _userLogger.CommandCancelled("GenSection");
            return true;
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
            var failure = result.Failure!;
            var userMessage = failure.UserMessage.Contains("SectionPreflight", StringComparison.OrdinalIgnoreCase)
                ? failure.UserMessage
                : $"{failure.UserMessage} 建议先运行 SectionPreflight 查看完整问题清单。";

            _feedbackPresenter.PresentFailure(
                "GenSection",
                failure with { UserMessage = userMessage },
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

    private bool EnsureWallTemplateMetadataBeforeGenerate()
    {
        var inspection = _metadataInspector.InspectMissingWallTemplateMetadata();
        if (!inspection.HasIssues)
        {
            return true;
        }

        if (TryGetRecommendedTemplateGroups(inspection.Issues, out var recommendedGroups))
        {
            if (!ConfirmRecommendedTemplateRepair(recommendedGroups))
            {
                return false;
            }

            if (!RepairByTemplateGroups(recommendedGroups))
            {
                return false;
            }
        }
        else
        {
            var selectedTemplate = SelectTemplateForUnresolvedIssues(inspection.Issues);
            if (selectedTemplate == null)
            {
                return false;
            }

            var group = new WallTemplateRepairGroup(
                "手动选择",
                selectedTemplate.TemplateId,
                selectedTemplate.TemplateName,
                inspection.Issues.Select(static issue => issue.Handle).ToList());

            if (!RepairByTemplateGroups(new[] { group }))
            {
                return false;
            }
        }

        var reinspection = _metadataInspector.InspectMissingWallTemplateMetadata();
        if (!reinspection.HasIssues)
        {
            return true;
        }

        ShowRepairIncompleteMessage(reinspection);
        return false;
    }

    private static bool TryGetRecommendedTemplateGroups(
        IReadOnlyList<ConvertedElementMetadataIssue> issues,
        out IReadOnlyList<WallTemplateRepairGroup> groups)
    {
        groups = Array.Empty<WallTemplateRepairGroup>();
        if (issues.Count == 0 ||
            issues.Any(static issue => string.IsNullOrWhiteSpace(issue.RecommendedTemplateId)))
        {
            return false;
        }

        groups = issues
            .GroupBy(
                static issue => new
                {
                    issue.LayerName,
                    issue.RecommendedTemplateId,
                    issue.RecommendedTemplateName
                })
            .Select(static group => new WallTemplateRepairGroup(
                group.Key.LayerName,
                group.Key.RecommendedTemplateId!,
                group.Key.RecommendedTemplateName ?? group.Key.RecommendedTemplateId!,
                group.Select(static issue => issue.Handle).ToList()))
            .ToList();

        return true;
    }

    private static bool ConfirmRecommendedTemplateRepair(IReadOnlyList<WallTemplateRepairGroup> groups)
    {
        var count = groups.Sum(static group => group.Handles.Count);
        var message = new StringBuilder();
        if (groups.Count == 1)
        {
            var group = groups[0];
            message.AppendLine(
                $"检测到 {count} 个位于 {group.LayerName} 的墙体缺少墙体模板信息，可能由复制已转换墙体产生。");
            message.AppendLine($"推荐绑定到同图层已有模板：{group.TemplateName}。");
        }
        else
        {
            message.AppendLine(
                $"检测到 {count} 个墙体缺少墙体模板信息，可能由复制已转换墙体产生。");
            message.AppendLine("推荐按同图层已有模板绑定：");
            foreach (var group in groups)
            {
                message.AppendLine($"- {group.LayerName}：{group.Handles.Count} 个，模板 {group.TemplateName}");
            }
        }

        message.AppendLine();
        message.Append("是否补写模板信息并继续生成剖面？");

        var dialog = new WallTemplateRepairConfirmationDialog(
            message.ToString(),
            "检测到复制墙体缺少模板信息");
        Application.ShowModalWindow(dialog);
        return dialog.Confirmed;
    }

    private static void ShowWarning(string message, string title)
    {
        MessageBox.Show(
            message.ToString(),
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private WallAssemblyTemplate? SelectTemplateForUnresolvedIssues(
        IReadOnlyList<ConvertedElementMetadataIssue> issues)
    {
        var templates = _wallTemplateCatalog.GetAllTemplates();
        if (templates.Count == 0)
        {
            ShowWarning(
                "检测到墙体缺少模板信息，但当前没有可用墙体模板。请先创建墙体模板，或重新执行构件转换。",
                "检测到复制墙体缺少模板信息");
            return null;
        }

        var dialog = new WallTemplateBindingDialog(issues, templates);
        Application.ShowModalWindow(dialog);
        return dialog.SelectedTemplate;
    }

    private bool RepairByTemplateGroups(IEnumerable<WallTemplateRepairGroup> groups)
    {
        var failures = new List<ConvertedElementMetadataRepairFailure>();
        var warnings = new List<string>();

        foreach (var group in groups)
        {
            var result = _metadataRepairService.RepairMissingWallTemplateMetadata(
                new ConvertedElementMetadataRepairRequest
                {
                    Handles = group.Handles,
                    TemplateId = group.TemplateId,
                    ConvertedType = "Wall"
                });

            failures.AddRange(result.Failures);
            warnings.AddRange(result.Warnings);
        }

        if (failures.Count == 0)
        {
            foreach (var warning in warnings.Take(5))
            {
                _logger.LogWarning("墙体模板元数据补写警告: {Warning}", warning);
            }

            return true;
        }

        ShowRepairFailedMessage(failures);
        return false;
    }

    private static void ShowRepairFailedMessage(IReadOnlyList<ConvertedElementMetadataRepairFailure> failures)
    {
        var message = new StringBuilder();
        message.AppendLine($"墙体模板信息补写失败，失败数量：{failures.Count}。");
        message.AppendLine("未继续生成剖面。");
        message.AppendLine();
        message.AppendLine("前几个失败对象：");
        foreach (var failure in failures.Take(5))
        {
            message.AppendLine($"- {failure.Handle}：{failure.Message}");
        }

        ShowWarning(
            message.ToString(),
            "墙体模板信息补写失败");
    }

    private static void ShowRepairIncompleteMessage(ConvertedElementMetadataInspectionResult inspection)
    {
        var message = new StringBuilder();
        message.AppendLine($"修复后仍检测到 {inspection.Issues.Count} 个墙体缺少模板信息。");
        message.AppendLine("请先选择要绑定的墙体模板，或重新执行构件转换。");
        message.AppendLine("未继续生成剖面。");
        message.AppendLine();
        message.AppendLine("前几个对象：");
        foreach (var issue in inspection.Issues.Take(5))
        {
            message.AppendLine($"- {issue.Handle}（{issue.LayerName}）");
        }

        ShowWarning(
            message.ToString(),
            "墙体模板信息仍不完整");
    }

    private sealed record WallTemplateRepairGroup(
        string LayerName,
        string TemplateId,
        string TemplateName,
        IReadOnlyList<string> Handles);
}
