using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.SectionGenerator.Plugin.Selection;
using MetroToolKits.SectionGenerator.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

internal static class FloorConfigDialogWorkflow
{
    public static void Run(
        IFloorConfigUseCase floorConfigUseCase,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IFloorConfigDocumentAssembler floorConfigDocumentAssembler,
        IFloorConfigSaveRequestMapper floorConfigSaveRequestMapper,
        IUserLogger userLogger,
        ILogger logger)
    {
        var configDocument = floorConfigUseCase.Load();
        userLogger.FloorConfigLoaded(configDocument.Config.Floors.Count, configDocument.Config.Floors.Select(f => f.Name).ToArray());

        while (true)
        {
            var window = new FloorConfigWindow(
                configDocument,
                slabTemplateCatalog,
                floorConfigDocumentAssembler,
                floorConfigSaveRequestMapper,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FloorConfigWindow>.Instance);

            Application.ShowModalWindow(window);
            configDocument = window.CurrentDocument;

            if (window.Tag is ValueTuple<string, FloorConfig> tag)
            {
                if (tag.Item1 == "PickAlignment")
                {
                    PickAlignmentPoints(configDocument.Config, tag.Item2, logger, userLogger);
                    continue;
                }

                if (tag.Item1 == "PickScope")
                {
                    PickScopeBounds(tag.Item2, logger);
                    continue;
                }
            }

            if (window.DialogResult == true)
            {
                var saveResult = floorConfigUseCase.Save(configDocument);
                if (!saveResult.Success)
                {
                    var message = saveResult.ErrorMessage ?? "楼层配置保存失败。";
                    MessageBox.Show(message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    continue;
                }

                userLogger.FloorConfigSaved(
                    configDocument.Config.Floors.Count,
                    configDocument.Config.Floors.Select(f => f.Name).ToArray());
            }

            break;
        }

        logger.LogDebug("楼层配置工作流结束");
    }

    private static void PickAlignmentPoints(SectionConfig config, FloorConfig floor, ILogger logger, IUserLogger userLogger)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        var ed = doc.Editor;
        var isBaseFloor = string.Equals(config.AlignmentBaseFloorName, floor.Name, StringComparison.OrdinalIgnoreCase);

        ed.WriteMessage($"\n=== 拾取楼层 [{floor.Name}] 对齐点 ===");
        logger.LogDebug("开始拾取楼层 {FloorName} 对齐点", floor.Name);

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
                userLogger.CommandCancelled("FloorConfig-PickAlignment");
                return;
            }

            points.Add(new Point3D(result.Value.X, result.Value.Y, result.Value.Z));
        }

        if (!FloorSectionLineTransformer.TryValidateAlignmentPoints(points, out var error))
        {
            ed.WriteMessage($"\n对齐点无效，请重新拾取: {error}");
            logger.LogWarning("楼层 {FloorName} 对齐点校验失败: {Error}", floor.Name, error);
            return;
        }

        floor.AlignmentPoints = points;
        userLogger.AlignmentPointsSet(floor.Name);
        logger.LogInformation("楼层 {FloorName} 对齐点已设置", floor.Name);
        ed.WriteMessage($"\n楼层 [{floor.Name}] 对齐点已记录，请在配置窗口中保存。");
    }

    private static void PickScopeBounds(FloorConfig floor, ILogger logger)
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
                logger.LogWarning("楼层 {FloorName} 整层范围拾取失败: {Error}", floor.Name, errorMessage);
            }

            return;
        }

        floor.ScopeBounds = scopeBounds;
        logger.LogInformation("楼层 {FloorName} 整层范围已设置为 {ScopeBounds}", floor.Name, scopeBounds);
        ed.WriteMessage($"\n楼层 [{floor.Name}] 整层范围已记录，请在配置窗口中保存。");
    }
}
