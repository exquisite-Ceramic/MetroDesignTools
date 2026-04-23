using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MetroToolKits.Foundation.Core.Hosting;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

/// <summary>
/// 区域引导转换命令 - 框选区域后逐层分配构件类型
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.ConvertRegion)]
public class ConvertRegionElementsCommand
{
    private readonly IElementTypeCatalog _typeCatalog;
    private readonly IElementConversionUseCase _conversionUseCase;

    public ConvertRegionElementsCommand(
        IElementTypeCatalog typeCatalog,
        IElementConversionUseCase conversionUseCase)
    {
        _typeCatalog = typeCatalog;
        _conversionUseCase = conversionUseCase;
    }

    public void Execute()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        var ed = doc.Editor;
        var db = doc.Database;

        // 加载构件类型
        var types = _typeCatalog.GetEnabledTypes();
        if (types.Count == 0)
        {
            ed.WriteMessage("\n未找到构件类型配置，请检查 ElementTypes.json 文件。");
            return;
        }

        // 1. 框选范围
        var selectionResult = ed.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = "\n选择要转换的区域（框选）: "
        });

        if (selectionResult.Status != PromptStatus.OK)
        {
            ed.WriteMessage("\n已取消选择。");
            return;
        }

        var selection = selectionResult.Value;
        ed.WriteMessage($"\n已选择 {selection.Count} 个实体。");
        var selectedHandles = selection.GetObjectIds()
            .Select(id => id.Handle.ToString())
            .ToArray();

        // 2. 提取区域内图层列表
        var description = _conversionUseCase.DescribeSelection(new ElementConversionSelectionRequest
        {
            EntityHandles = selectedHandles
        });
        if (!description.Success || description.Layers.Count == 0)
        {
            ed.WriteMessage($"\n{description.ErrorMessage ?? "所选区域内未找到图层。"}");
            return;
        }
        var layers = description.Layers;

        ed.WriteMessage($"\n发现 {layers.Count} 个图层:");
        foreach (var layer in layers)
        {
            ed.WriteMessage($"\n  - {layer}");
        }

        // 3. 逐层高亮预览并等待用户输入快捷字母
        var layerMappings = new Dictionary<string, string>();

        using var lockDoc = doc.LockDocument();
        using var tr = db.TransactionManager.StartTransaction();

        try
        {
            foreach (var layerName in layers)
            {
                // 高亮当前图层上的实体
                HighlightLayerEntities(tr, db, selection, layerName);

                // 显示提示
                var typeHints = string.Join(" | ", types.Select(t => $"{t.TypeName}({t.ShortcutKey})"));
                ed.WriteMessage($"\n\n当前图层: {layerName}");
                ed.WriteMessage($"\n可用类型: {typeHints}");
                ed.WriteMessage("\n输入快捷字母分配类型，ESC 跳过，Q 退出: ");

                // 获取用户输入
                var keyResult = GetKeyInput(ed);

                if (keyResult == null)
                {
                    // ESC - 跳过当前图层
                    ed.WriteMessage(" 已跳过");
                    UnhighlightLayerEntities(tr, db, selection, layerName);
                    continue;
                }

                if (keyResult.Equals("Q", StringComparison.OrdinalIgnoreCase))
                {
                    // Q - 退出
                    ed.WriteMessage(" 已退出");
                    break;
                }

                // 查找匹配的类型
                var matchedType = types.FirstOrDefault(t =>
                    t.ShortcutKey.Equals(keyResult, StringComparison.OrdinalIgnoreCase));

                if (matchedType != null)
                {
                    layerMappings[layerName] = matchedType.TypeId;
                    ed.WriteMessage($" → {matchedType.TypeName}");
                }
                else
                {
                    ed.WriteMessage(" 无效输入，已跳过");
                }

                // 取消高亮
                UnhighlightLayerEntities(tr, db, selection, layerName);
            }

            // 4. 应用转换
            if (layerMappings.Count > 0)
            {
                var confirmResult = ed.GetKeywords(
                    $"\n\n已分配 {layerMappings.Count} 个图层映射。是否应用？",
                    new[] { "是", "否" });

                if (confirmResult.Status == PromptStatus.OK && confirmResult.StringResult == "是")
                {
                    tr.Abort();
                    var result = _conversionUseCase.ApplyMappings(new ElementConversionApplyRequest
                    {
                        ApplyToEntireDrawing = false,
                        EntityHandles = selectedHandles,
                        LayerMappings = layerMappings
                            .Select(static mapping => new LayerTypeAssignment
                            {
                                SourceLayerName = mapping.Key,
                                TypeId = mapping.Value
                            })
                            .ToList()
                    });

                    if (result.Success)
                    {
                        ed.WriteMessage($"\n转换完成，共转换 {result.ConvertedCount} 个实体。");
                    }
                    else
                    {
                        ed.WriteMessage($"\n转换失败：{result.ErrorMessage}");
                    }
                }
                else
                {
                    tr.Abort();
                    ed.WriteMessage("\n已取消转换。");
                }
            }
            else
            {
                tr.Abort();
                ed.WriteMessage("\n未分配任何映射，已退出。");
            }
        }
        catch
        {
            tr.Abort();
            throw;
        }
    }

    private void HighlightLayerEntities(Transaction tr, Database db, SelectionSet selection, string layerName)
    {
        foreach (var objId in selection.GetObjectIds())
        {
            var entity = (Entity)tr.GetObject(objId, OpenMode.ForRead);
            if (entity.Layer == layerName)
            {
                entity.UpgradeOpen();
                entity.Highlight();
            }
        }
    }

    private void UnhighlightLayerEntities(Transaction tr, Database db, SelectionSet selection, string layerName)
    {
        foreach (var objId in selection.GetObjectIds())
        {
            var entity = (Entity)tr.GetObject(objId, OpenMode.ForRead);
            if (entity.Layer == layerName)
            {
                entity.UpgradeOpen();
                entity.Unhighlight();
            }
        }
    }

    private string? GetKeyInput(Editor ed)
    {
        // 使用 GetString 获取单字符输入
        var options = new PromptStringOptions("")
        {
            AllowSpaces = false
        };

        var result = ed.GetString(options);
        if (result.Status != PromptStatus.OK) return null;

        return result.StringResult?.Trim().ToUpper();
    }
}
