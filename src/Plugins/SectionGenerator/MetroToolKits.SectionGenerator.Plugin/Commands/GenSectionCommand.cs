using MetroToolKits.Foundation.Core.Logging;
using System.Diagnostics;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Extensions.Logging;
using MetroToolKits.Bootstrap;
using MetroToolKits.Bootstrap.Logging;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.App.UseCases;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 生成剖面图命令
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.GenSection)]
public sealed class GenSectionCommand
{
    private readonly IGenerateSectionUseCase _useCase;
    private readonly ILogger<GenSectionCommand> _logger;
    private readonly IUserLogger _userLogger;
    private readonly OperationFeedbackPresenter _feedbackPresenter;

    public GenSectionCommand(
        IGenerateSectionUseCase useCase,
        ILogger<GenSectionCommand> logger,
        IUserLogger userLogger,
        OperationFeedbackPresenter feedbackPresenter)
    {
        _useCase = useCase;
        _logger = logger;
        _userLogger = userLogger;
        _feedbackPresenter = feedbackPresenter;
    }

    public void Execute()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var ed = doc.Editor;

        _userLogger.CommandStarted("GenSection");
        _logger.LogInformation("执行 GenSection 命令");

        // 步骤1：选择剖切线
        var lineResult = ed.GetEntity(new PromptEntityOptions("\n选择剖切线（直线）: ")
        {
            AllowNone = false
        });

        if (lineResult.Status != PromptStatus.OK)
        {
            _userLogger.CommandCancelled("GenSection");
            return;
        }

        Point3D cutStart, cutEnd;
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            var ent = tr.GetObject(lineResult.ObjectId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
            if (ent is not Autodesk.AutoCAD.DatabaseServices.Line cadLine)
            {
                _feedbackPresenter.PresentFailure(
                    "GenSection",
                    SectionGenerationFailures.InvalidSectionLine(
                        $"所选对象类型为 {ent?.GetType().Name ?? "Unknown"}。"));
                tr.Abort();
                return;
            }
            cutStart = new Point3D(cadLine.StartPoint.X, cadLine.StartPoint.Y, cadLine.StartPoint.Z);
            cutEnd   = new Point3D(cadLine.EndPoint.X,   cadLine.EndPoint.Y,   cadLine.EndPoint.Z);
            var length = cutStart.DistanceTo(cutEnd);
            _logger.LogDebug("剖切线选择，Handle: {Handle}，长度: {Length:F2}", lineResult.ObjectId.Handle, length);
            tr.Commit();
        }

        // 步骤2：视图深度
        var depthOpt = new PromptDoubleOptions("\n视图深度（看线深度，mm）<3000>: ")
        {
            DefaultValue = 3000, AllowNegative = false, AllowZero = false
        };
        var depthResult = ed.GetDouble(depthOpt);
        double viewDepth = depthResult.Status == PromptStatus.OK ? depthResult.Value : 3000;

        // 步骤3：插入点
        var ptResult = ed.GetPoint(new PromptPointOptions("\n指定剖面图插入点: "));
        if (ptResult.Status != PromptStatus.OK)
        {
            _userLogger.CommandCancelled("GenSection");
            return;
        }
        var insertPt = new Point3D(ptResult.Value.X, ptResult.Value.Y, ptResult.Value.Z);

        // 步骤4：执行用例
        _userLogger.SectionGenerating("（加载楼层配置中...）");
        var sw = Stopwatch.StartNew();

        var result = _useCase.Execute(new GenerateSectionRequest
        {
            CutLineHandle  = lineResult.ObjectId.Handle.ToString(),
            CutLineStart   = cutStart,
            CutLineEnd     = cutEnd,
            ViewDepth      = viewDepth,
            InsertionPoint = insertPt
        });

        sw.Stop();

        if (result.Status == OperationStatus.Failed)
        {
            var hasIntersectionWarning = result.Diagnostics.Any(d =>
                d.Code == SectionGenerationErrorCodes.NoIntersectingElements);

            _feedbackPresenter.PresentFailure(
                "GenSection",
                result.Failure!,
                result.Diagnostics,
                useSectionLineInvalid: hasIntersectionWarning);

            return;
        }

        _userLogger.SectionCreated(result.BlockName!, result.FloorCount, sw.ElapsedMilliseconds);

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
            sw.ElapsedMilliseconds);
    }
}
