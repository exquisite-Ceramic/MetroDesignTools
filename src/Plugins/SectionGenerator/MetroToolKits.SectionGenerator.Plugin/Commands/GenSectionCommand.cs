using MetroToolKits.Foundation.Core.Logging;
using System.Diagnostics;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.UseCases;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 生成剖面图命令
/// </summary>
public sealed class GenSectionCommand
{
    private readonly IGenerateSectionUseCase _useCase;
    private readonly ILogger<GenSectionCommand> _logger;
    private readonly IUserLogger _userLogger;

    public GenSectionCommand(
        IGenerateSectionUseCase useCase,
        ILogger<GenSectionCommand> logger,
        IUserLogger userLogger)
    {
        _useCase    = useCase;
        _logger     = logger;
        _userLogger = userLogger;
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
                _userLogger.CommandFailed("GenSection", "请选择直线实体");
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

        if (result.Success)
        {
            _userLogger.SectionCreated(result.BlockName!, result.FloorCount, sw.ElapsedMilliseconds);
            _logger.LogInformation("剖面生成完成，块名称: {BlockName}，楼层数: {FloorCount}，耗时: {ElapsedMs}ms",
                result.BlockName, result.FloorCount, sw.ElapsedMilliseconds);
        }
        else
        {
            _userLogger.CommandFailed("GenSection", "剖面生成失败，请检查构件识别结果", result.ErrorMessage);
            _logger.LogError("GenSection 失败: {Error}", result.ErrorMessage);
        }
    }
}
