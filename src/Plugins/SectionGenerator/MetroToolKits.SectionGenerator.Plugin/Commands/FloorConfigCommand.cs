using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.SectionGenerator.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 楼层配置管理命令
/// </summary>
public sealed class FloorConfigCommand
{
    private readonly IFloorConfigRepository _repo;
    private readonly ILogger<FloorConfigCommand> _logger;
    private readonly IUserLogger _userLogger;

    public FloorConfigCommand(
        IFloorConfigRepository repo,
        ILogger<FloorConfigCommand> logger,
        IUserLogger userLogger)
    {
        _repo       = repo;
        _logger     = logger;
        _userLogger = userLogger;
    }

    public void Execute()
    {
        _userLogger.CommandStarted("FloorConfig");
        _logger.LogInformation("打开楼层配置窗口");

        while (true)
        {
            var window = new FloorConfigWindow(
                _repo,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FloorConfigWindow>.Instance,
                _userLogger);

            Application.ShowModalWindow(window);

            if (window.Tag is ("PickAlignment", FloorConfig floor))
            {
                PickAlignmentPoints(floor);
                continue;
            }

            break;
        }

        _logger.LogDebug("楼层配置命令结束");
    }

    private void PickAlignmentPoints(FloorConfig floor)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var ed = doc.Editor;

        ed.WriteMessage($"\n=== 拾取楼层 [{floor.Name}] 对齐点 ===");
        _logger.LogDebug("开始拾取楼层 {FloorName} 对齐点", floor.Name);

        var srcPoints = new List<Point3D>();
        var dstPoints = new List<Point3D>();

        // 拾取源坐标系三点（平面图中）
        ed.WriteMessage("\n请在平面图中依次拾取 3 个基准点（原点、X方向点、Y方向点）:");
        for (int i = 0; i < 3; i++)
        {
            var labels = new[] { "原点", "X方向点", "Y方向点" };
            var result = ed.GetPoint(new PromptPointOptions($"\n  [{i + 1}/3] 拾取{labels[i]}: "));
            if (result.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n已取消拾取。");
                _userLogger.CommandCancelled("FloorConfig-PickAlignment");
                return;
            }
            srcPoints.Add(new Point3D(result.Value.X, result.Value.Y, result.Value.Z));
        }

        // 拾取目标坐标系三点（剖面图中）
        ed.WriteMessage("\n请在剖面图中依次拾取对应的 3 个基准点:");
        for (int i = 0; i < 3; i++)
        {
            var labels = new[] { "原点", "X方向点", "Y方向点" };
            var result = ed.GetPoint(new PromptPointOptions($"\n  [{i + 1}/3] 拾取{labels[i]}: "));
            if (result.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n已取消拾取。");
                _userLogger.CommandCancelled("FloorConfig-PickAlignment");
                return;
            }
            dstPoints.Add(new Point3D(result.Value.X, result.Value.Y, result.Value.Z));
        }

        floor.AlignmentSourcePoints = srcPoints;
        floor.AlignmentTargetPoints = dstPoints;

        _userLogger.AlignmentPointsSet(floor.Name);
        _logger.LogInformation("楼层 {FloorName} 对齐点已设置", floor.Name);
        ed.WriteMessage($"\n楼层 [{floor.Name}] 对齐点已记录，请在配置窗口中保存。");
    }
}
