using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Extensions.Logging;
using MetroToolKits.Bootstrap;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.SectionGenerator.Plugin.Selection;
using MetroToolKits.SectionGenerator.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 楼层配置管理命令
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.FloorConfig)]
public sealed class FloorConfigCommand
{
    private readonly IFloorConfigUseCase _floorConfigUseCase;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly ILogger<FloorConfigCommand> _logger;
    private readonly IUserLogger _userLogger;

    public FloorConfigCommand(
        IFloorConfigUseCase floorConfigUseCase,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        ILogger<FloorConfigCommand> logger,
        IUserLogger userLogger)
    {
        _floorConfigUseCase = floorConfigUseCase;
        _slabTemplateCatalog = slabTemplateCatalog;
        _logger     = logger;
        _userLogger = userLogger;
    }

    public void Execute()
    {
        _userLogger.CommandStarted("FloorConfig");
        _logger.LogInformation("打开楼层配置窗口");
        var config = _floorConfigUseCase.Load();
        _userLogger.FloorConfigLoaded(config.Floors.Count, config.Floors.Select(f => f.Name).ToArray());

        while (true)
        {
            var window = new FloorConfigWindow(
                config,
                _slabTemplateCatalog.GetAllTemplates(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FloorConfigWindow>.Instance);

            Application.ShowModalWindow(window);
            config = window.CurrentConfig;

            if (window.Tag is ("PickAlignment", FloorConfig floor))
            {
                PickAlignmentPoints(config, floor);
                continue;
            }

            if (window.Tag is ("PickScope", FloorConfig scopeFloor))
            {
                PickScopeBounds(scopeFloor);
                continue;
            }

            if (window.DialogResult == true)
            {
                _floorConfigUseCase.Save(config);
                _userLogger.FloorConfigSaved(config.Floors.Count, config.Floors.Select(f => f.Name).ToArray());
            }

            break;
        }

        _logger.LogDebug("楼层配置命令结束");
    }

    private void PickAlignmentPoints(SectionConfig config, FloorConfig floor)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var ed = doc.Editor;
        var isBaseFloor = string.Equals(config.AlignmentBaseFloorName, floor.Name, StringComparison.OrdinalIgnoreCase);

        ed.WriteMessage($"\n=== 拾取楼层 [{floor.Name}] 对齐点 ===");
        _logger.LogDebug("开始拾取楼层 {FloorName} 对齐点", floor.Name);

        ed.WriteMessage(isBaseFloor
            ? "\n请在基准层平面依次拾取 3 个基准点（原点、X方向点、Y方向点）:"
            : $"\n请在楼层 [{floor.Name}] 平面依次拾取与基准层对应的 3 个点（原点、X方向点、Y方向点）:");

        var points = new List<Point3D>();
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
            points.Add(new Point3D(result.Value.X, result.Value.Y, result.Value.Z));
        }

        if (!FloorSectionLineTransformer.TryValidateAlignmentPoints(points, out var error))
        {
            ed.WriteMessage($"\n对齐点无效，请重新拾取: {error}");
            _logger.LogWarning("楼层 {FloorName} 对齐点校验失败: {Error}", floor.Name, error);
            return;
        }

        floor.AlignmentPoints = points;

        _userLogger.AlignmentPointsSet(floor.Name);
        _logger.LogInformation("楼层 {FloorName} 对齐点已设置", floor.Name);
        ed.WriteMessage($"\n楼层 [{floor.Name}] 对齐点已记录，请在配置窗口中保存。");
    }

    private void PickScopeBounds(FloorConfig floor)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var ed = doc.Editor;

        ed.WriteMessage($"\n=== 选择楼层 [{floor.Name}] 的整层图元 ===");

        var options = new PromptSelectionOptions
        {
            MessageForAdding = $"\n选择楼层 [{floor.Name}] 的全部图元，用于生成整层范围框: ",
            AllowDuplicates = false
        };

        if (!SelectionScopeBoundsService.TryPickBounds(
                doc,
                options,
                out var scopeBounds,
                out var cancelled,
                out var errorMessage))
        {
            if (!cancelled && !string.IsNullOrWhiteSpace(errorMessage))
            {
                ed.WriteMessage($"\n{errorMessage}");
                _logger.LogWarning("楼层 {FloorName} 整层范围拾取失败: {Error}", floor.Name, errorMessage);
            }

            return;
        }

        floor.ScopeBounds = scopeBounds;
        _logger.LogInformation("楼层 {FloorName} 整层范围已设置为 {ScopeBounds}", floor.Name, scopeBounds);
        ed.WriteMessage($"\n楼层 [{floor.Name}] 整层范围已记录，请在配置窗口中保存。");
    }
}
